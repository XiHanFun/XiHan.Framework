// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Security;

/// <summary>
/// 权限原语。定义权限的基本信息。
/// </summary>
[ApiLevel(Stability.Stable, "1.0")]
public interface IPermission
{
    /// <summary>
    /// 权限码，如 "order:read"、"user:create"。
    /// </summary>
    string Code { get; }

    /// <summary>
    /// 权限显示名称。
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// 权限描述。
    /// </summary>
    string? Description { get; }
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

    public bool Succeeded { get; }
    public string? FailureReason { get; }

    public static AuthorizationResult Success() => new(true);

    public static AuthorizationResult Failure(string reason) => new(false, reason);
}
