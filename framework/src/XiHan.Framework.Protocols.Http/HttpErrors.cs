// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Http;

/// <summary>
/// HTTP 协议适配器的错误码
/// </summary>
/// <remarks>
/// 登记在 <c>spec/conventions/error-codes.md</c>
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public static class HttpErrors
{
    /// <summary>
    /// 两个操作推导出同一条 HTTP 路由（方法与模板都相同）
    /// </summary>
    /// <remarks>
    /// 或普通端点与操作共用同一条路由。用 <c>XiHan:Protocols:Http:Routes</c> 覆盖其中一个
    /// </remarks>
    public const string DuplicateRoute = "PROTOCOLS_CONFLICT_002";

    /// <summary>
    /// 普通端点的 JSON 请求 / 响应类型既没有声明同哪个操作的 Schema
    /// </summary>
    /// <remarks>
    /// 也不与任何已登记操作的请求 / 响应类型匹配。生成 OpenAPI 文档失败
    /// </remarks>
    public const string OpenApiSchemaUnresolved = "PROTOCOLS_VALIDATION_010";

    /// <summary>
    /// 操作的请求体超过上限
    /// </summary>
    /// <remarks>
    /// 上限是 <see cref="HttpProtocolOptions.MaxRequestBodyBytes"/>；<c>Content-Length</c> 超过时不读请求体，没有长度时读到超过即停（ADR-0176）
    /// </remarks>
    public const string RequestBodyTooLarge = "PROTOCOLS_VALIDATION_011";

    /// <summary>
    /// 操作的请求体不是 JSON
    /// </summary>
    /// <remarks>
    /// 有请求体时 <c>Content-Type</c> 必须是 <c>application/json</c> 或 <c>+json</c> 结尾的类型；浏览器跨站的简单请求（<c>text/plain</c>、表单）因此不能调用操作（ADR-0176）
    /// </remarks>
    public const string RequestBodyNotJson = "PROTOCOLS_VALIDATION_012";
}
