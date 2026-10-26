// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Http.Routing;

/// <summary>
/// 把登记表里暴露给 HTTP 的操作映射为端点：路由按操作 ID 推导或由选项覆盖，端点元数据携带 <see cref="OperationDescriptor"/> 与 <see cref="OperationPayloadBinder"/>
/// （<c>UseXiHan()</c> 据此在进入管道前把描述放入上下文并把路由值、请求体与查询串绑定为 XGI 载荷，Authorize 阶段按契约判定、Validate 阶段按 Schema 校验并解码），
/// 端点内只在当前调用里执行；失败结果留在上下文，由 <c>UseXiHan()</c> 写成 Problem Details。必须放在 <c>UseXiHan()</c> 之后。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class OperationEndpointRouteBuilderExtensions
{
    /// <summary>
    /// 映射全部暴露给 HTTP 且非 <see cref="Stability.Internal"/> 的操作，返回映射的端点数。
    /// </summary>
    public static int MapXiHanOperations(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var registry = endpoints.ServiceProvider.GetRequiredService<OperationRegistry>();
        var options = endpoints.ServiceProvider.GetService<HttpProtocolOptions>() ?? new HttpProtocolOptions();
        var count = 0;
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

            // 绑定委托与描述一起挂在元数据上：UseXiHan() 在进入管道前调用它，Validate 阶段才能看到载荷
            OperationPayloadBinder binder = (httpContext, cancellationToken) => OperationRequestBinder.BindAsync(httpContext, descriptor, cancellationToken);
            endpoints
                .MapMethods(route.Template, [route.Method], httpContext => HandleAsync(httpContext, descriptor))
                .WithMetadata(descriptor)
                .WithMetadata(binder)
                .WithName(descriptor.Id);
            count++;
        }

        return count;
    }

    /// <summary>
    /// 端点处理：用 <c>UseXiHan()</c> 进管道前绑定好的载荷在当前调用里执行操作（Validate 阶段已解码时直接执行），成功写 JSON 响应，失败把结果留给宿主。
    /// </summary>
    private static async Task HandleAsync(HttpContext httpContext, OperationDescriptor descriptor)
    {
        if (!httpContext.GetXiHanContext().TryGetValue(out var context))
        {
            throw new FrameworkException(KernelErrors.InvalidAppState, "Operation endpoints require UseXiHan() to run before routing; the invocation context is missing.");
        }

        if (!context.GetOperationPayload().TryGetValue(out var bytes))
        {
            throw new FrameworkException(KernelErrors.InvalidAppState, $"Operation endpoint '{descriptor.Id}' has no bound payload; UseXiHan() must run before routing so the payload is bound before the pipeline.");
        }

        var invoker = httpContext.RequestServices.GetRequiredService<IOperationInvoker>();
        var response = await invoker.ExecuteAsync(context, new OperationRequest(descriptor.Id, bytes)).ConfigureAwait(false);
        if (!response.IsSuccess)
        {
            // 结果已在上下文里，UseXiHan() 在响应未开始时写成 Problem Details
            return;
        }

        httpContext.Response.StatusCode = HttpRouteConvention.SuccessStatusCode(descriptor.Contract);
        httpContext.Response.ContentType = "application/json; charset=utf-8";
        await httpContext.Response.Body.WriteAsync(response.Payload, context.CancellationToken).ConfigureAwait(false);
    }
}
