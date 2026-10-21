// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Security;

/// <summary>
/// 授权判定结果。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public readonly record struct AuthorizationResult
{
    private AuthorizationResult(Error? reason) => Reason = reason;

    /// <summary>
    /// 是否允许。<c>default</c> 为拒绝。
    /// </summary>
    public bool IsAllowed { get; private init; }

    /// <summary>
    /// 拒绝原因；允许时为 <see langword="null"/>。
    /// </summary>
    public Error? Reason { get; }

    /// <summary>
    /// 允许。
    /// </summary>
    public static AuthorizationResult Allow() => new(null) { IsAllowed = true };

    /// <summary>
    /// 拒绝。
    /// </summary>
    public static AuthorizationResult Deny(Error reason)
    {
        ArgumentNullException.ThrowIfNull(reason);
        return new(reason);
    }

    /// <summary>
    /// 转换为 <see cref="Result"/>：拒绝时携带原因，未给出原因时使用 <see cref="KernelErrors"/> 的通用拒绝码。
    /// </summary>
    public Result ToResult() => IsAllowed
        ? Result.Success()
        : Result.Failure(Reason ?? Error.Denied("SECURITY_DENIED_002", "策略拒绝。"));
}
