// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Security;

/// <summary>
/// 安全策略原语。定义基于策略的授权模型。
/// </summary>
[ApiLevel(Stability.Preview, "1.0")]
public interface IPolicy
{
    /// <summary>
    /// 策略名称。
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 评估此策略是否满足。
    /// </summary>
    Task<AuthorizationResult> EvaluateAsync(SecurityContext context);
}

/// <summary>
/// 权限检查结果。
/// </summary>
public sealed class AuthorizationResult
{
    private AuthorizationResult(bool succeeded, string? failureReason = null)
    {
        Succeeded = succeeded;
        FailureReason = failureReason;
    }

    /// <summary>
    /// 权限检查是否通过。
    /// </summary>
    public bool Succeeded { get; }

    /// <summary>
    /// 失败原因。
    /// </summary>
    public string? FailureReason { get; }

    /// <summary>
    /// 创建一个成功的授权结果。
    /// </summary>
    public static AuthorizationResult Success() => new(true);

    /// <summary>
    /// 创建一个失败的授权结果。
    /// </summary>
    public static AuthorizationResult Failure(string reason) => new(false, reason);
}
