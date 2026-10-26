// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Security.Abstractions.Authorization;

namespace XiHan.Framework.Protocols.Http.OpenApi;

/// <summary>
/// 从宿主的端点里挑出进文档的普通端点：有路由模板、不是操作端点（没有 <see cref="OperationDescriptor"/> 元数据）、有 HTTP 方法元数据、
/// 带 <see cref="IAcceptsMetadata"/> 或 <see cref="IProducesResponseTypeMetadata"/> 之一、没有标 <see cref="IExcludeFromDescriptionMetadata"/>。
/// Hub、健康探针、重定向、静态资源都没有这两种描述元数据，自然不进，不另做名单。
/// </summary>
internal static partial class EndpointReader
{
    /// <summary>
    /// 读出进文档的端点，每个 HTTP 方法一条，按模板与方法排序。
    /// </summary>
    public static IReadOnlyList<EndpointDescription> Read(IEnumerable<Endpoint> endpoints)
    {
        var descriptions = new List<EndpointDescription>();
        foreach (var endpoint in endpoints)
        {
            if (endpoint is not RouteEndpoint route
                || route.Metadata.GetMetadata<OperationDescriptor>() is not null
                || route.Metadata.GetMetadata<IExcludeFromDescriptionMetadata>() is { ExcludeFromDescription: true })
            {
                continue;
            }

            var methods = route.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods;
            if (methods is not { Count: > 0 })
            {
                continue;
            }

            var accepts = route.Metadata.GetMetadata<IAcceptsMetadata>();
            var produces = route.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>();
            if (accepts is null && produces.Count == 0)
            {
                continue;
            }

            var template = Normalize(route.RoutePattern.RawText);
            var display = route.DisplayName ?? template;
            foreach (var method in methods)
            {
                descriptions.Add(new EndpointDescription(
                    template,
                    method.ToUpperInvariant(),
                    display,
                    route.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName,
                    route.Metadata.GetMetadata<IEndpointSummaryMetadata>()?.Summary,
                    route.Metadata.GetMetadata<IEndpointDescriptionMetadata>()?.Description,
                    route.Metadata.GetMetadata<ITagsMetadata>()?.Tags ?? [],
                    route.Metadata.GetMetadata<AuthorizationRequirement>(),
                    accepts,
                    produces,
                    route.Metadata.GetMetadata<OperationSchemaReference>()));
            }
        }

        return descriptions
            .OrderBy(static d => d.Template, StringComparer.Ordinal)
            .ThenBy(static d => d.Method, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// 把 ASP.NET Core 路由模板改成 OpenAPI 形式：补上开头的斜杠，<c>{id:int}</c>、<c>{id?}</c>、<c>{*path}</c> 都只留 <c>{name}</c>。
    /// </summary>
    private static string Normalize(string? rawText)
    {
        var template = string.IsNullOrEmpty(rawText) ? "/" : rawText;
        if (!template.StartsWith('/'))
        {
            template = "/" + template;
        }

        return RouteParameter().Replace(template, static m => "{" + m.Groups["name"].Value + "}");
    }

    /// <summary>
    /// 路由模板里的参数：可带 <c>*</c> / <c>**</c> 前缀、约束与可选标记。
    /// </summary>
    [GeneratedRegex(@"\{\**(?<name>[A-Za-z_][A-Za-z0-9_]*)[^}]*\}")]
    private static partial Regex RouteParameter();
}
