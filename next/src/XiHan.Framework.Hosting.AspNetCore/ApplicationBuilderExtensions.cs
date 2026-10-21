// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;

namespace XiHan.Framework.Hosting.AspNetCore;

/// <summary>
/// ASP.NET Core 管道与端点的接入点。<c>AddXiHan()</c> 由内核的 <c>IHostApplicationBuilder</c> 扩展提供。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// 让其余 ASP.NET Core 管道在内核管道内执行：请求头翻译为调用上下文，端点元数据上的 <see cref="AuthorizationRequirement"/> 进入上下文，
    /// 失败结果在响应未开始时以 Problem Details 回写（装配了本地化时 <c>detail</c> 按调用方区域翻译）；应用未就绪时返回 503。
    /// 带 <see cref="PipelineBypassMetadata"/> 的端点（含探针）不经过内核管道。放在路由之后。
    /// </summary>
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
    /// 映射探针端点：<c>{prefix}/startup</c>、<c>{prefix}/ready</c>、<c>{prefix}/live</c>。通过返回 200，否则 503；探针不经过内核管道。
    /// </summary>
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
            snapshot.Features.Select(static f => new FeatureHealth(f.Manifest.Id, f.State.ToString(), f.Error?.Code)).ToList());

        httpContext.Response.StatusCode = pass ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable;
        return httpContext.Response.WriteAsJsonAsync(response, HostingJsonContext.Default.HealthResponse, cancellationToken: httpContext.RequestAborted);
    }

    /// <summary>
    /// 把错误写成 RFC 9457 Problem Details 响应，状态码由错误码的 KIND 段映射；装配了本地化时 <c>detail</c> 按调用方区域翻译并带 <c>Content-Language</c>。
    /// </summary>
    private static Task WriteProblemAsync(HttpContext httpContext, Error error, string traceId, InvocationContext? context)
    {
        string? detail = null;

        // 本地化是可选能力：没有装配时 detail 就是代码里的英文消息
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
        }

        var problem = ProblemResponse.FromError(error, httpContext.Request.Path.ToString(), traceId, detail);
        httpContext.Response.StatusCode = problem.Status;
        return httpContext.Response.WriteAsJsonAsync(problem, HostingJsonContext.Default.ProblemResponse, "application/problem+json", httpContext.RequestAborted);
    }
}
