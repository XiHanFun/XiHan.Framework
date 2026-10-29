// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Grpc.Core;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Grpc.Services;

/// <summary>
/// 错误码的 KIND 段 → gRPC 状态码。
/// </summary>
internal static class GrpcStatusMapping
{
    /// <summary>
    /// 未认证的错误码：与 HTTP 载体一样，<c>SECURITY_DENIED_001</c> 是"未认证"。
    /// </summary>
    private const string NotAuthenticated = "SECURITY_DENIED_001";

    /// <summary>
    /// 凭据无效（过期、签名不符、格式错）的错误码：与 HTTP 的 401 对应，也是认证失败。
    /// </summary>
    private const string InvalidCredential = "SECURITY_DENIED_003";

    /// <summary>
    /// 映射。
    /// </summary>
    /// <remarks>
    /// VALIDATION → InvalidArgument，NOTFOUND → NotFound，CONFLICT → Aborted，DENIED → PermissionDenied（未认证与凭据无效 → Unauthenticated），UNAVAILABLE → Unavailable，TIMEOUT → DeadlineExceeded，CANCELLED → Cancelled，其他 → Internal。
    /// </remarks>
    public static StatusCode StatusCodeFor(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error.Kind switch
        {
            "VALIDATION" => StatusCode.InvalidArgument,
            "NOTFOUND" => StatusCode.NotFound,
            "CONFLICT" => StatusCode.Aborted,
            "DENIED" => string.Equals(error.Code, NotAuthenticated, StringComparison.Ordinal) || string.Equals(error.Code, InvalidCredential, StringComparison.Ordinal)
                ? StatusCode.Unauthenticated
                : StatusCode.PermissionDenied,
            "UNAVAILABLE" => StatusCode.Unavailable,
            "TIMEOUT" => StatusCode.DeadlineExceeded,
            "CANCELLED" => StatusCode.Cancelled,
            _ => StatusCode.Internal,
        };
    }
}
