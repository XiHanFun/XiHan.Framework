// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Http.OpenApi;

/// <summary>
/// 发布 OpenAPI 文档端点：从操作登记表、权限码注册表、已装配的认证处理器与宿主的全部端点按需生成并缓存（输入由 <see cref="OpenApiDocumentSource.FromServices"/> 组装）；
/// 装配了本地化时按 <c>Accept-Language</c> 协商出的区域分别生成并缓存（操作的 <c>summary</c> 来自消息目录，响应带 <c>Content-Language</c>）。端点不经过内核管道。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class OpenApiEndpointRouteBuilderExtensions
{
    /// <summary>
    /// 默认的文档路径。
    /// </summary>
    public const string DefaultPattern = "/openapi.json";

    /// <summary>
    /// 映射 OpenAPI 3.1 文档端点。标题、版本与 <c>servers</c> 由 <paramref name="options"/> 指定，缺省（或为 <see langword="null"/>）时标题取应用名、版本取框架版本、不写 <c>servers</c>。
    /// </summary>
    public static IEndpointConventionBuilder MapXiHanOpenApi(this IEndpointRouteBuilder endpoints, OpenApiOptions? options = null, string pattern = DefaultPattern)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        options ??= new OpenApiOptions();
        var services = endpoints.ServiceProvider;
        var dataSources = endpoints.DataSources;
        var localizer = services.GetService<IMessageLocalizer>();
        // 文档在第一次请求时按区域生成并缓存：此时全部特性已装配、登记表已定型、宿主的端点都已映射。
        // 权限码注册表由内核在装配后以单例登记（AppBuilder.Build），特性在配置阶段登记的权限定义都已在其中
        var documents = new ConcurrentDictionary<string, Lazy<byte[]>>(StringComparer.Ordinal);
        byte[] Generate(string locale) => OpenApiDocumentBuilder.Build(OpenApiDocumentSource.FromServices(services, options, dataSources.SelectMany(static source => source.Endpoints), locale));

        return PipelineBypassMetadata.BypassXiHanPipeline(endpoints.MapGet(pattern, async httpContext =>
        {
            // 没有装配本地化时只有一份文档（没有摘要）；装配了时按协商出的区域各一份，缺省区域是本地化选项的默认区域
            var locale = localizer is null ? string.Empty : localizer.Options.Negotiate(HttpInvocation.AcceptedLocales(httpContext));
            var document = documents.GetOrAdd(locale, static (key, generate) => new Lazy<byte[]>(() => generate(key)), (Func<string, byte[]>)Generate).Value;
            httpContext.Response.ContentType = "application/json; charset=utf-8";
            if (locale.Length > 0)
            {
                httpContext.Response.Headers.ContentLanguage = locale;
            }

            await httpContext.Response.Body.WriteAsync(document, httpContext.RequestAborted).ConfigureAwait(false);
        }));
    }
}
