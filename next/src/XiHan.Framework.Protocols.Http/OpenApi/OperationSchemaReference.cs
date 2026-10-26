// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Http.OpenApi;

/// <summary>
/// 普通端点的元数据：它的 JSON 请求体与 JSON 响应正文各"同某个已登记操作的请求 / 响应 Schema"。
/// 普通端点不做第二套 Schema 推导，JSON 类型只能引用已登记操作的根 Schema：显式声明用这个元数据（<c>WithXiHanOperationSchema</c>），
/// 没有声明时按 <c>Accepts&lt;T&gt;</c> / <c>Produces&lt;T&gt;</c> 的类型与已登记操作的请求 / 响应类型自动匹配。
/// </summary>
/// <param name="RequestOperationId">请求体同哪个操作的请求 Schema；为空按类型匹配。</param>
/// <param name="ResponseOperationId">响应正文同哪个操作的响应 Schema；为空按类型匹配。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record OperationSchemaReference(string? RequestOperationId = null, string? ResponseOperationId = null)
{
    /// <summary>
    /// 请求体引用的操作 ID。
    /// </summary>
    public string? RequestOperationId { get; } = Validate(RequestOperationId, nameof(RequestOperationId));

    /// <summary>
    /// 响应正文引用的操作 ID。
    /// </summary>
    public string? ResponseOperationId { get; } = RequestOperationId is null && ResponseOperationId is null
        ? throw new ArgumentException("At least one of the request or response operation IDs must be given.", nameof(ResponseOperationId))
        : Validate(ResponseOperationId, nameof(ResponseOperationId));

    /// <summary>
    /// 操作 ID 为空放行，否则须符合文法。
    /// </summary>
    private static string? Validate(string? operationId, string paramName)
        => operationId is null || OperationId.IsValid(operationId) ? operationId : throw new ArgumentException($"'{operationId}' is not a valid operation ID.", paramName);
}
