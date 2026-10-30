// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Hosting.AspNetCore.Health;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Validation;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Security.Abstractions.Authentication;
using XiHan.Framework.Security.Abstractions.Authorization;

namespace XiHan.Framework.Hosting.AspNetCore;

/// <summary>
/// ASP.NET Core 管道与端点的接入点。
/// </summary>
/// <remarks>
/// <c>AddXiHan()</c> 由内核的 <c>IHostApplicationBuilder</c> 扩展提供。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// 让其余 ASP.NET Core 管道在内核管道内执行。
    /// </summary>
    /// <remarks>
    /// 请求头翻译为调用上下文，端点元数据上的 <see cref="AuthorizationRequirement"/> 与 <see cref="OperationDescriptor"/> 进入上下文（带 <see cref="OperationPayloadBinder"/> 时先绑定载荷），失败结果在响应未开始时以 Problem Details 回写（装配了本地化时 <c>detail</c> 按调用方区域翻译）；应用未就绪时返回 503。带 <see cref="PipelineBypassMetadata"/> 的端点（含探针）不经过内核管道。放在路由之后。
    /// </remarks>
    public static IApplicationBuilder UseXiHan(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var xihan = app.ApplicationServices.GetRequiredService<App>();

        return app.Use(async (httpContext, next) =>
        {
            // 标注了绕过元数据的端点（探针、静态资源等）直接放行，应用未就绪时也要能访问
            if (httpContext.GetEndpoint()?.Metadata.GetMetadata<PipelineBypassMetadata>() is not null)
            {
                await next(httpContext).ConfigureAwait(false);
                return;
            }

            // 未处于服务状态时还没有调用上下文，trace-id 留空
            if (!xihan.State.IsServing())
            {
                await WriteProblemAsync(httpContext, Error.Unavailable(KernelErrors.NotServing, KernelMessages.NotServing, MessageArguments.Of(("state", xihan.State.ToString()))), traceId: string.Empty, context: null).ConfigureAwait(false);
                return;
            }

            var context = HttpInvocation.CreateContext(httpContext);
            if (httpContext.GetEndpoint()?.Metadata.GetMetadata<AuthorizationRequirement>() is { } requirement)
            {
                context.RequireAuthorization(requirement);
            }

            // 操作端点把描述与载荷绑定挂在元数据上：进入管道前把描述放进上下文（Authorize 阶段按契约判定），并先绑定载荷（Validate 阶段按 Schema 校验并解码），端点内只剩执行；
            // 绑定失败（请求体不是 JSON 对象等）不进管道，直接写 Problem Details
            var endpoint = httpContext.GetEndpoint();
            if (endpoint?.Metadata.GetMetadata<OperationDescriptor>() is { } operation)
            {
                context.SetOperation(operation);
                if (endpoint.Metadata.GetMetadata<OperationPayloadBinder>() is { } binder)
                {
                    var bound = await binder(httpContext, context.CancellationToken).ConfigureAwait(false);
                    if (!bound.TryGetValue(out var payload))
                    {
                        await WriteProblemAsync(httpContext, bound.Error!, context.TraceId, context).ConfigureAwait(false);
                        return;
                    }

                    context.SetOperationPayload(payload);
                }
            }

            // 其余 ASP.NET Core 管道作为内核管道的 Handle 阶段执行；端点通过上下文设置结果，异常由内核翻译为失败结果
            var outcome = await xihan.InvokeAsync(context, async _ => await next(httpContext).ConfigureAwait(false)).ConfigureAwait(false);

            // 响应已开始写出时无法再改状态码与正文，只能保留端点已写的内容
            if (outcome.IsFailure && !httpContext.Response.HasStarted)
            {
                await WriteProblemAsync(httpContext, outcome.Error!, context.TraceId, context).ConfigureAwait(false);
            }
        });
    }

    /// <summary>
    /// 映射探针端点。
    /// </summary>
    /// <remarks>
    /// <c>{prefix}/startup</c>、<c>{prefix}/ready</c>、<c>{prefix}/live</c>。通过返回 200，否则 503；探针不经过内核管道。
    /// </remarks>
    public static IEndpointRouteBuilder MapXiHanHealth(this IEndpointRouteBuilder endpoints, string prefix = "/health")
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(prefix);
        var xihan = endpoints.ServiceProvider.GetRequiredService<App>();

        PipelineBypassMetadata.BypassXiHanPipeline(endpoints.MapGet(prefix + "/startup", httpContext => WriteHealthAsync(httpContext, xihan, static h => h.Started)));
        PipelineBypassMetadata.BypassXiHanPipeline(endpoints.MapGet(prefix + "/ready", httpContext => WriteHealthAsync(httpContext, xihan, static h => h.Ready)));
        PipelineBypassMetadata.BypassXiHanPipeline(endpoints.MapGet(prefix + "/live", httpContext => WriteHealthAsync(httpContext, xihan, static h => h.Live)));
        return endpoints;
    }

    /// <summary>
    /// 取应用健康快照，按探针谓词写出 200 或 503 以及 <see cref="HealthResponse"/> 正文。
    /// </summary>
    private static Task WriteHealthAsync(HttpContext httpContext, App app, Func<HealthSnapshot, bool> probe)
    {
        var snapshot = app.Health();
        var pass = probe(snapshot);
        var response = new HealthResponse(
            pass ? "pass" : "fail",
            snapshot.State.ToString(),
            snapshot.Features.Select(static f => new FeatureHealth(f.Manifest.Id, f.State.ToString(), f.Error?.Code)).ToList(),
            snapshot.Contributions.Select(static c => new FeatureHealth(c.Name, c.State.ToString(), c.Error?.Code)
            {
                Description = c.Description,
                CheckedAt = c.CheckedAt,
                DurationMs = c.Duration?.TotalMilliseconds,
                Data = c.Data.Count > 0 ? c.Data : null,
            }).ToList());

        httpContext.Response.StatusCode = pass ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable;
        return httpContext.Response.WriteAsJsonAsync(response, HostingJsonContext.Default.HealthResponse, cancellationToken: httpContext.RequestAborted);
    }

    /// <summary>
    /// 把错误写成 RFC 9457 Problem Details 响应，状态码由错误码的 KIND 段映射。
    /// </summary>
    /// <remarks>
    /// 装配了本地化时 <c>detail</c> 与字段级 <c>errors</c> 按调用方区域翻译并带 <c>Content-Language</c>。
    /// </remarks>
    private static async Task WriteProblemAsync(HttpContext httpContext, Error error, string traceId, InvocationContext? context)
    {
        string? detail = null;
        Func<SchemaViolation, string>? translate = null;

        // 本地化是可选能力：没有装配时 detail 就是代码里的英文消息，字段级错误就是违规的原因文本
        if (httpContext.RequestServices.GetService<IMessageLocalizer>() is { } localizer)
        {
            // 有调用上下文时区域已在 Ingress 协商进 Baggage；未就绪的 503 还没有上下文，直接从 Accept-Language 协商
            var locale = context is not null ? localizer.Options.Resolve(context) : localizer.Options.Negotiate(HttpInvocation.AcceptedLocales(httpContext));
            // Content-Language 标的是文案实际命中的区域（可能是回退链上的区域），不是请求的区域；自由文本错误没有区域可标
            if (localizer.Resolve(error, locale).TryGetValue(out var localized))
            {
                detail = localized.Text;
                httpContext.Response.Headers.ContentLanguage = localized.Locale;
            }

            // 字段级错误与 detail 走同一通道：有消息键的违规按同一区域翻译，没有键（应用自校验的文案）或目录没有时用原因文本
            translate = violation => violation.MessageKey is { } key && localizer.Resolve(key, locale, violation.Arguments).TryGetValue(out var text) ? text.Text : violation.Reason;
        }

        var problem = ProblemResponse.FromError(error, httpContext.Request.Path.ToString(), traceId, detail, translate);
        httpContext.Response.StatusCode = problem.Status;
        httpContext.Response.ContentType = "application/problem+json";
        // 401 带质询：凭据无效时质询被拒的方案（Bearer 带 error="invalid_token"），没有凭据时质询全部已装配的方案
        if (problem.Status == StatusCodes.Status401Unauthorized)
        {
            var presented = context?.GetCredential() ?? Maybe.None<AuthenticationCredential>();
            var schemes = httpContext.RequestServices.GetServices<IAuthenticationHandler>().Select(static h => h.Scheme);
            foreach (var challenge in AuthenticationChallenge.For(error, presented, schemes))
            {
                httpContext.Response.Headers.Append(HeaderNames.WWWAuthenticate, challenge);
            }
        }
        // 手写序列化：details 的值是运行期才知道类型的标量，不经源生成上下文的 object 分发
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            problem.WriteTo(writer);
        }

        await httpContext.Response.Body.WriteAsync(buffer.WrittenMemory, httpContext.RequestAborted).ConfigureAwait(false);
    }
}
