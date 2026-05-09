// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Security;

#pragma warning disable CA1711

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

#pragma warning restore CA1711

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
