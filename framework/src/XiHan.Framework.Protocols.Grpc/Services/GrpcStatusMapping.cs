// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Grpc.Core;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Grpc.Services;

/// <summary>
/// 错误码的 KIND 段 → gRPC 状态码
/// </summary>
internal static class GrpcStatusMapping
{
    /// <summary>
    /// 映射
    /// </summary>
    /// <remarks>
    /// VALIDATION → InvalidArgument，NOTFOUND → NotFound，CONFLICT → Aborted，DENIED → PermissionDenied（未认证与凭据无效 → Unauthenticated），UNAVAILABLE → Unavailable，TIMEOUT → DeadlineExceeded，CANCELLED → Cancelled，其他 → Internal
    /// </remarks>
    public static StatusCode StatusCodeFor(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error.Kind switch
        {
            "VALIDATION" => StatusCode.InvalidArgument,
            "NOTFOUND" => StatusCode.NotFound,
            "CONFLICT" => StatusCode.Aborted,
            // 认证失败（未认证、凭据无效）的判定与 HTTP 的 401 同一处
            "DENIED" => ProblemResponse.IsAuthenticationFailure(error) ? StatusCode.Unauthenticated : StatusCode.PermissionDenied,
            "UNAVAILABLE" => StatusCode.Unavailable,
            "TIMEOUT" => StatusCode.DeadlineExceeded,
            "CANCELLED" => StatusCode.Cancelled,
            _ => StatusCode.Internal,
        };
    }
}
