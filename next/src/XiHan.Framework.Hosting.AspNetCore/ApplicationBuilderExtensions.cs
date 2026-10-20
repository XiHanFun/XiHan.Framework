// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.AspNetCore;

/// <summary>
/// ASP.NET Core 管道与端点的接入点。<c>AddXiHan()</c> 由内核的 <c>IHostApplicationBuilder</c> 扩展提供。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// 让其余 ASP.NET Core 管道在内核管道内执行：请求头翻译为调用上下文，失败结果在响应未开始时以 Problem Details 回写；
    /// 应用未就绪时返回 503。带 <see cref="PipelineBypassMetadata"/> 的端点（含探针）不经过内核管道。放在路由之后。
    /// </summary>
    public static IApplicationBuilder UseXiHan(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var xihan = app.ApplicationServices.GetRequiredService<App>();

        return app.Use(async (httpContext, next) =>
        {
            if (httpContext.GetEndpoint()?.Metadata.GetMetadata<PipelineBypassMetadata>() is not null)
            {
                await next(httpContext).ConfigureAwait(false);
                return;
            }

            if (!xihan.State.IsServing())
            {
                await WriteProblemAsync(httpContext, Error.Unavailable(KernelErrors.NotServing, $"应用处于 {xihan.State}，暂不接收请求。"), traceId: string.Empty).ConfigureAwait(false);
                return;
            }

            var context = HttpInvocation.CreateContext(httpContext);
            var outcome = await xihan.InvokeAsync(context, async _ => await next(httpContext).ConfigureAwait(false)).ConfigureAwait(false);

            if (outcome.IsFailure && !httpContext.Response.HasStarted)
            {
                await WriteProblemAsync(httpContext, outcome.Error!, context.TraceId).ConfigureAwait(false);
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

    private static Task WriteProblemAsync(HttpContext httpContext, Error error, string traceId)
    {
        var problem = ProblemResponse.FromError(error, httpContext.Request.Path.ToString(), traceId);
        httpContext.Response.StatusCode = problem.Status;
        return httpContext.Response.WriteAsJsonAsync(problem, HostingJsonContext.Default.ProblemResponse, "application/problem+json", httpContext.RequestAborted);
    }
}
