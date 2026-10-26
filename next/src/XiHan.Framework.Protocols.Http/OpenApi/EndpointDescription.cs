// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.AspNetCore.Http.Metadata;
using XiHan.Framework.Security.Abstractions.Authorization;

namespace XiHan.Framework.Protocols.Http.OpenApi;

/// <summary>
/// 一个进文档的普通端点的一个方法：OpenAPI 形式的路径模板、方法与从端点元数据读出的描述信息。
/// </summary>
/// <param name="Template">OpenAPI 形式的路径模板（约束、可选标记与通配前缀都去掉）。</param>
/// <param name="Method">大写的 HTTP 方法。</param>
/// <param name="DisplayName">端点显示名，用于错误说明。</param>
/// <param name="Name">端点名（<c>IEndpointNameMetadata</c>），作 <c>operationId</c>。</param>
/// <param name="Summary">摘要（<c>IEndpointSummaryMetadata</c>）。</param>
/// <param name="Description">说明（<c>IEndpointDescriptionMetadata</c>）。</param>
/// <param name="Tags">标签（<c>ITagsMetadata</c>）。</param>
/// <param name="Requirement">授权要求（<c>RequireXiHanPermission</c> / <c>RequireXiHanPolicy</c> / <c>RequireXiHanAuthentication</c> 的元数据）。</param>
/// <param name="Accepts">请求体元数据。</param>
/// <param name="Produces">响应元数据，按声明顺序。</param>
/// <param name="SchemaReference">显式声明的操作 Schema 引用。</param>
internal sealed record EndpointDescription(
    string Template,
    string Method,
    string DisplayName,
    string? Name,
    string? Summary,
    string? Description,
    IReadOnlyList<string> Tags,
    AuthorizationRequirement? Requirement,
    IAcceptsMetadata? Accepts,
    IReadOnlyList<IProducesResponseTypeMetadata> Produces,
    OperationSchemaReference? SchemaReference);
