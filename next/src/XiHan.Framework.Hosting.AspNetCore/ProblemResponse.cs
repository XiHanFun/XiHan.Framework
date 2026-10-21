// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.AspNetCore.Http;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.AspNetCore;

/// <summary>
/// RFC 9457 Problem Details 的响应体。<c>code</c> 与 <c>traceId</c> 是扩展成员。
/// </summary>
/// <param name="Type">问题类型 URI：<c>urn:xihan:error:&lt;错误码&gt;</c>。</param>
/// <param name="Title">错误码的 KIND 段。</param>
/// <param name="Status">HTTP 状态码。</param>
/// <param name="Detail">错误说明；装配了本地化时是调用方区域的文案，否则是 <see cref="Error.Message"/>。</param>
/// <param name="Instance">请求路径。</param>
/// <param name="Code">错误码。</param>
/// <param name="TraceId">调用的 trace-id。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record ProblemResponse(string Type, string Title, int Status, string Detail, string Instance, string Code, string TraceId)
{
    /// <summary>
    /// 从错误构造响应体；<paramref name="detail"/> 给出时作为本地化后的说明，否则用 <see cref="Error.Message"/>。
    /// </summary>
    public static ProblemResponse FromError(Error error, string instance, string traceId, string? detail = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(traceId);
        return new ProblemResponse($"urn:xihan:error:{error.Code}", error.Kind, StatusCodeFor(error), detail ?? error.Message, instance, error.Code, traceId);
    }

    /// <summary>
    /// 错误码 KIND 段到 HTTP 状态码的映射。
    /// </summary>
    public static int StatusCodeFor(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error.Kind switch
        {
            "VALIDATION" => StatusCodes.Status400BadRequest,
            "NOTFOUND" => StatusCodes.Status404NotFound,
            "CONFLICT" => StatusCodes.Status409Conflict,
            // SECURITY_DENIED_001 是“未认证”，按语义给 401；其余 DENIED 都是已认证但无权限，给 403
            "DENIED" => string.Equals(error.Code, "SECURITY_DENIED_001", StringComparison.Ordinal) ? StatusCodes.Status401Unauthorized : StatusCodes.Status403Forbidden,
            "UNAVAILABLE" => StatusCodes.Status503ServiceUnavailable,
            "TIMEOUT" => StatusCodes.Status504GatewayTimeout,
            "CANCELLED" => StatusCodes.Status408RequestTimeout,
            // 未知 KIND（含 UNEXPECTED 与内核 HOSTING 等）一律视为服务端错误
            _ => StatusCodes.Status500InternalServerError,
        };
    }
}
