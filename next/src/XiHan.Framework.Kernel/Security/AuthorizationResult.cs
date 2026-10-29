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
    /// <summary>
    /// 创建结果。
    /// </summary>
    /// <remarks>
    /// <see cref="IsAllowed"/> 由工厂方法通过初始化器设置，保证 <c>default</c> 实例是拒绝。
    /// </remarks>
    /// <param name="reason">拒绝原因。</param>
    private AuthorizationResult(Error? reason) => Reason = reason;

    /// <summary>
    /// 是否允许。
    /// </summary>
    /// <remarks>
    /// <c>default</c> 为拒绝。
    /// </remarks>
    public bool IsAllowed { get; private init; }

    /// <summary>
    /// 拒绝原因。
    /// </summary>
    /// <remarks>
    /// 允许时为 <see langword="null"/>。
    /// </remarks>
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
        // 只有 default 实例才会没有原因：它表示"从未判定"，按拒绝处理以免放行。
        : Result.Failure(Reason ?? Error.Denied("SECURITY_DENIED_002", KernelMessages.DeniedByPolicy));
}
