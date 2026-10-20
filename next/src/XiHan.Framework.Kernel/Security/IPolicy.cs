// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Security;

/// <summary>
/// 一次授权请求：谁（安全上下文）对什么（资源）做什么（动作）。
/// </summary>
/// <param name="Security">安全上下文。</param>
/// <param name="Action">动作，通常是操作 ID。</param>
/// <param name="Resource">资源名；无具体资源时为 <see langword="null"/>。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record AuthorizationRequest(SecurityContext Security, string Action, ResourceName? Resource = null)
{
    /// <summary>
    /// 安全上下文。
    /// </summary>
    public SecurityContext Security { get; } = Security ?? throw new ArgumentNullException(nameof(Security));

    /// <summary>
    /// 动作。
    /// </summary>
    public string Action { get; } = string.IsNullOrWhiteSpace(Action) ? throw new ArgumentException("动作不能为空。", nameof(Action)) : Action;
}

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

/// <summary>
/// 授权策略原语。安全子系统的 RBAC / ABAC 引擎实现它；内核只定义判定接口。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IPolicy
{
    /// <summary>
    /// 策略名。
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 判定。
    /// </summary>
    ValueTask<AuthorizationResult> EvaluateAsync(AuthorizationRequest request, CancellationToken cancellationToken);
}
