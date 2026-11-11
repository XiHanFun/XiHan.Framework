// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Http.Routing;

/// <summary>
/// 把登记表里暴露给 HTTP 的操作映射为端点
/// </summary>
/// <remarks>
/// 路由按操作 ID 推导或由选项覆盖，端点元数据携带 <see cref="OperationDescriptor"/> 与 <see cref="OperationPayloadBinder"/>（<c>UseXiHan()</c> 据此在进入管道前把描述放入上下文并把路由值、请求体与查询串绑定为 XGI 载荷，Authorize 阶段按契约判定、Validate 阶段按 Schema 校验并解码），端点内只在当前调用里执行；失败结果留在上下文，由 <c>UseXiHan()</c> 写成 Problem Details。必须放在 <c>UseXiHan()</c> 之后
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public static class OperationEndpointRouteBuilderExtensions
{
    /// <summary>
    /// 映射全部暴露给 HTTP 且非 <see cref="Stability.Internal"/> 的操作
    /// </summary>
    /// <remarks>
    /// 操作端点映射在一个空前缀的路由组里，路由模板不变；加在返回值上的约定（授权、限流、CORS、标签、过滤器）作用于每个操作端点，与其他 <c>MapXiHan*</c> 一样（ADR-0144）。授权约定与各操作契约的权限叠加判定，不顶替它（ADR-0174）
    /// </remarks>
    /// <returns>这组操作端点的约定构建器</returns>
    /// <exception cref="FrameworkException">两个操作推导出同一条路由（<see cref="HttpErrors.DuplicateRoute"/>）</exception>
    public static IEndpointConventionBuilder MapXiHanOperations(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var registry = endpoints.ServiceProvider.GetRequiredService<OperationRegistry>();
        var options = endpoints.ServiceProvider.GetService<HttpProtocolOptions>() ?? new HttpProtocolOptions();
        // 空前缀的组只用来承载约定：应用对返回值加的约定在端点构建时才应用，所以映射之后再加也作用于每个操作端点
        var group = endpoints.MapGroup(string.Empty);
        // 同一方法 + 模板只能有一个操作：无 id 的 get 与 list 都是 GET /{resource}，这种冲突在启动时报出来而不是留给路由匹配时的 500
        var mapped = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var descriptor in registry.ExposedTo(Expose.Http))
        {
            // 内部稳定性的操作只供进程内调用，不对外发布
            if (descriptor.Contract.Stability == Stability.Internal)
            {
                continue;
            }

            var route = options.RouteFor(descriptor);
            var key = route.ToString();
            if (!mapped.TryAdd(key, descriptor.Id))
            {
                throw new FrameworkException(HttpErrors.DuplicateRoute, $"Operations '{mapped[key]}' and '{descriptor.Id}' derive the same HTTP route '{key}'. Override one of them via 'XiHan:Protocols:Http:Routes'.");
            }

            // 绑定委托与描述一起挂在元数据上：UseXiHan() 把它包成载荷来源，Validate 阶段（Authorize 之后）才读请求体（ADR-0176）
            OperationPayloadBinder binder = (httpContext, cancellationToken) => OperationRequestBinder.BindAsync(httpContext, descriptor, cancellationToken);
            // 写出方式也挂在元数据上：端点执行成功时用它，Idempotency 阶段回放（端点没有执行）时 UseXiHan() 用它写出同样的状态码与正文
            OperationResponseWriter writer = WriteResponseAsync;
            group
                .MapMethods(route.Template, [route.Method], httpContext => HandleAsync(httpContext, descriptor))
                .WithMetadata(descriptor)
                .WithMetadata(binder)
                .WithMetadata(writer)
                .WithName(descriptor.Id);
        }

        return group;
    }

    /// <summary>
    /// 端点处理
    /// </summary>
    /// <remarks>
    /// 用 Validate 阶段读好的载荷在当前调用里执行操作（已解码时直接执行）；关掉内置中间件、没有 Validate 阶段时在这里读。成功写 JSON 响应，失败把结果留给宿主
    /// </remarks>
    private static async Task HandleAsync(HttpContext httpContext, OperationDescriptor descriptor)
    {
        if (!httpContext.FindXiHanContext().TryGetValue(out var context))
        {
            throw new FrameworkException(KernelErrors.InvalidAppState, "Operation endpoints require UseXiHan() to run after routing and before the endpoint executes; the invocation context is missing.");
        }

        // 通常 Validate 阶段已经读过；没有 Validate 阶段时在这里读（ADR-0176）
        var resolved = await context.ResolveOperationPayloadAsync().ConfigureAwait(false);
        if (!resolved.TryGetValue(out var found))
        {
            // 结果留在上下文里，UseXiHan() 写成 Problem Details
            context.SetOutcome(resolved.Error!);
            return;
        }

        if (!found.TryGetValue(out var bytes))
        {
            throw new FrameworkException(KernelErrors.InvalidAppState, $"Operation endpoint '{descriptor.Id}' has no payload source; UseXiHan() must run after routing and before the endpoint executes, so the payload binder is attached to the invocation.");
        }

        var invoker = httpContext.RequestServices.GetRequiredService<IOperationInvoker>();
        var response = await invoker.ExecuteAsync(context, new OperationRequest(descriptor.Id, bytes)).ConfigureAwait(false);
        if (!response.IsSuccess)
        {
            // 结果已在上下文里，UseXiHan() 在响应未开始时写成 Problem Details
            return;
        }

        await WriteResponseAsync(httpContext, descriptor, response.Payload, context.CancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 写出成功响应
    /// </summary>
    /// <remarks>
    /// 状态码按约定（<c>create</c> 为 201，其余 200），正文是响应的 JSON
    /// </remarks>
    private static async Task WriteResponseAsync(HttpContext httpContext, OperationDescriptor descriptor, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = HttpRouteConvention.SuccessStatusCode(descriptor.Contract);
        httpContext.Response.ContentType = "application/json; charset=utf-8";
        await httpContext.Response.Body.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
    }
}
