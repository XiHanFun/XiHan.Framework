// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Http.OpenApi;

/// <summary>
/// 一份已发布的 OpenAPI 文档：名字、标题与地址。
/// </summary>
/// <remarks>
/// 由 <c>GetXiHanOpenApiDocuments</c> 列出，供宿主登记到文档界面（如 Scalar 的 <c>AddDocument(name, title, url)</c>）。标题按缺省区域。
/// </remarks>
/// <param name="Name">文档名：全量文档是 <c>all</c>，分组文档是组名。</param>
/// <param name="Title">标题。</param>
/// <param name="Url">文档地址（路由模式，如 <c>/openapi.json</c>、<c>/openapi/access.json</c>）。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record OpenApiDocumentLink(string Name, string Title, string Url);
