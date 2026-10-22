// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Http;

/// <summary>
/// 发布 OpenAPI 文档端点：从操作登记表按需生成一次并缓存；不经过内核管道。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class OpenApiEndpointRouteBuilderExtensions
{
    /// <summary>
    /// 映射 OpenAPI 3.1 文档端点；标题取应用名，版本取框架版本。
    /// </summary>
    public static IEndpointConventionBuilder MapXiHanOpenApi(this IEndpointRouteBuilder endpoints, string pattern = "/openapi.json")
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        var services = endpoints.ServiceProvider;
        // 文档在第一次请求时生成：此时全部特性已装配、登记表已定型
        var document = new Lazy<byte[]>(() => OpenApiDocumentBuilder.Build(
            services.GetRequiredService<OperationRegistry>(),
            services.GetService<HttpProtocolOptions>() ?? new HttpProtocolOptions(),
            services.GetService<IHostEnvironment>()?.ApplicationName ?? FrameworkInfo.Name,
            FrameworkInfo.Version));

        return PipelineBypassMetadata.BypassXiHanPipeline(endpoints.MapGet(pattern, async httpContext =>
        {
            httpContext.Response.ContentType = "application/json; charset=utf-8";
            await httpContext.Response.Body.WriteAsync(document.Value, httpContext.RequestAborted).ConfigureAwait(false);
        }));
    }
}
