// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Http.OpenApi;

/// <summary>
/// 发布 OpenAPI 文档端点
/// </summary>
/// <remarks>
/// 文档从操作登记表、权限码注册表、已装配的认证处理器与宿主的全部端点按需生成并缓存（输入由 <see cref="OpenApiDocumentSource.FromServices"/> 组装）；
/// 装配了本地化时按 <c>Accept-Language</c> 协商出的区域分别生成并缓存（文案来自消息目录，响应带 <c>Content-Language</c>）
/// 有分组时另为每组发一份文档（ADR-0052）。登记表或分组来源（<see cref="IOpenApiGroupSource"/>）变了即丢掉缓存，下一次请求重新生成（ADR-0251）。端点不经过内核管道
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class OpenApiEndpointRouteBuilderExtensions
{
    /// <summary>
    /// 默认的文档路径
    /// </summary>
    public const string DefaultPattern = "/openapi.json";

    /// <summary>
    /// 全量文档的名字（<see cref="OpenApiDocumentLink.Name"/>）
    /// </summary>
    /// <remarks>
    /// 分组不能用它
    /// </remarks>
    public const string FullDocumentName = "all";

    /// <summary>
    /// 分组文档路由里组名参数的名字
    /// </summary>
    private const string GroupRouteValue = "group";

    /// <summary>
    /// 映射 OpenAPI 3.1 文档端点：全量文档在 <paramref name="pattern"/>，每组一份在 <see cref="GroupPattern"/>
    /// </summary>
    /// <remarks>
    /// 标题、版本、<c>servers</c> 与分组由 <paramref name="options"/> 指定，缺省（或为 <see langword="null"/>）时标题取应用名、版本取框架版本、不写 <c>servers</c>、没有分组
    /// 容器里的 <see cref="IOpenApiGroupSource"/> 给出运行期的分组；分组文档是一条带组名参数的路由，按请求时的分组答复，不是当时的组名为 404（ADR-0251）
    /// </remarks>
    /// <returns>全量文档端点的约定构建器</returns>
    public static IEndpointConventionBuilder MapXiHanOpenApi(this IEndpointRouteBuilder endpoints, OpenApiOptions? options = null, string pattern = DefaultPattern)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        options ??= new OpenApiOptions();
        var services = endpoints.ServiceProvider;
        var dataSources = endpoints.DataSources;
        var localizer = services.GetService<IMessageLocalizer>();
        // 文档在请求时按组与区域生成并缓存：此时全部特性已装配、宿主的端点都已映射
        // 权限码注册表由内核在装配后以单例登记（AppBuilder.Build），特性在配置阶段登记的权限定义都已在其中
        // 登记表或分组来源变了（插件运行期加载与卸载）即整体换新，下一次请求按新内容生成
        var documents = new OpenApiDocumentCache();
        var registry = services.GetRequiredService<OperationRegistry>();
        var groupSources = services.GetServices<IOpenApiGroupSource>().ToList();
        _ = ChangeToken.OnChange(() => OpenApiDocumentCache.ChangeOf(registry, groupSources), documents.Clear);
        byte[]? Generate((string? Group, string Locale) key)
        {
            var source = OpenApiDocumentSource.FromServices(services, options, dataSources.SelectMany(static source => source.Endpoints), key.Locale.Length > 0 ? key.Locale : null, key.Group);
            return key.Group is null || source.Groups.Any(group => string.Equals(group.Name, key.Group, StringComparison.Ordinal)) ? OpenApiDocumentBuilder.Build(source) : null;
        }

        async Task ServeAsync(HttpContext httpContext, string? group)
        {
            // 没有装配本地化时每组只有一份文档（没有目录文案）；装配了时按协商出的区域各一份，缺省区域是本地化选项的默认区域
            var locale = localizer is null ? string.Empty : localizer.Options.Negotiate(HttpInvocation.AcceptedLocales(httpContext));
            if (documents.GetOrAdd((group, locale), Generate) is not { } document)
            {
                httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            httpContext.Response.ContentType = "application/json; charset=utf-8";
            if (locale.Length > 0)
            {
                httpContext.Response.Headers.ContentLanguage = locale;
            }

            await httpContext.Response.Body.WriteAsync(document, httpContext.RequestAborted).ConfigureAwait(false);
        }

        PipelineBypassMetadata.BypassXiHanPipeline(endpoints.MapGet(GroupPattern(pattern, "{" + GroupRouteValue + "}"), httpContext => ServeAsync(httpContext, httpContext.Request.RouteValues[GroupRouteValue] as string)));
        return PipelineBypassMetadata.BypassXiHanPipeline(endpoints.MapGet(pattern, httpContext => ServeAsync(httpContext, null)));
    }

    /// <summary>
    /// 列出 <see cref="MapXiHanOpenApi"/> 发布的全部文档：全量文档在前，各组按排序、名字在后
    /// </summary>
    /// <remarks>
    /// 标题按本地化的默认区域：全量文档取 <see cref="OpenApiOptions.Title"/>（缺省应用名），分组取 <c>openapi.groups.&lt;组名&gt;.name</c>（没有取组名）
    /// 宿主据此把文档登记到文档界面，如 Scalar 的 <c>options.AddDocument(link.Name, link.Title, link.Url)</c>。参数须与 <see cref="MapXiHanOpenApi"/> 的一致
    /// 列的是调用时的分组（含 <see cref="IOpenApiGroupSource"/> 此刻给出的）：之后来源新增的分组不会补进已经登记的界面，全量文档里照样有它们
    /// </remarks>
    public static IReadOnlyList<OpenApiDocumentLink> GetXiHanOpenApiDocuments(this IEndpointRouteBuilder endpoints, OpenApiOptions? options = null, string pattern = DefaultPattern)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        options ??= new OpenApiOptions();
        var services = endpoints.ServiceProvider;
        var localizer = services.GetService<IMessageLocalizer>();
        string? Text(string key) => localizer is not null && MessageTemplate.IsValidKey(key) && localizer.Find(key, localizer.Options.DefaultLocale).TryGetValue(out var text) ? text : null;

        var links = new List<OpenApiDocumentLink>
        {
            new(FullDocumentName, options.Title ?? services.GetService<IHostEnvironment>()?.ApplicationName ?? FrameworkInfo.Name, pattern),
        };
        foreach (var group in OpenApiDocumentSource.GroupsOf(services, options).OrderBy(static g => g.Order).ThenBy(static g => g.Name, StringComparer.Ordinal))
        {
            links.Add(new OpenApiDocumentLink(group.Name, Text("openapi.groups." + group.Name.Replace('-', '_') + ".name") ?? group.Name, GroupPattern(pattern, group.Name)));
        }

        return links;
    }

    /// <summary>
    /// 分组文档的路径
    /// </summary>
    /// <remarks>
    /// <paramref name="pattern"/> 去掉 <c>.json</c> 后接 <c>/&lt;组名&gt;.json</c>（缺省 <c>/openapi/&lt;组名&gt;.json</c>）
    /// </remarks>
    public static string GroupPattern(string pattern, string group)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        ArgumentException.ThrowIfNullOrWhiteSpace(group);
        var stem = pattern.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? pattern[..^".json".Length] : pattern;
        return stem.TrimEnd('/') + "/" + group + ".json";
    }
}
