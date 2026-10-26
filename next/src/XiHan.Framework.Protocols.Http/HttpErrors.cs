// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Http;

/// <summary>
/// HTTP 协议适配器的错误码；登记在 <c>spec/conventions/error-codes.md</c>。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class HttpErrors
{
    /// <summary>两个操作推导出同一条 HTTP 路由（方法与模板都相同），或普通端点与操作共用同一条路由；用 <c>XiHan:Protocols:Http:Routes</c> 覆盖其中一个。</summary>
    public const string DuplicateRoute = "PROTOCOLS_CONFLICT_002";

    /// <summary>普通端点的 JSON 请求 / 响应类型既没有声明同哪个操作的 Schema，也不与任何已登记操作的请求 / 响应类型匹配；生成 OpenAPI 文档失败。</summary>
    public const string OpenApiSchemaUnresolved = "PROTOCOLS_VALIDATION_010";
}
