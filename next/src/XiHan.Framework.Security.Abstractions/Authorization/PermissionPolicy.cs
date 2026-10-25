// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions.Permissions;

namespace XiHan.Framework.Security.Abstractions.Authorization;

/// <summary>
/// 策略：主体持有某个权限（经 <see cref="IPermissionChecker"/>）。匿名主体先按未认证拒绝。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class PermissionPolicy : IPolicy
{
    /// <summary>
    /// 策略名前缀 <c>permission:</c>。
    /// </summary>
    public const string Prefix = "permission:";

    /// <summary>
    /// 权限检查器；判定主体是否持有 <see cref="PermissionCode"/>。
    /// </summary>
    private readonly IPermissionChecker _checker;

    /// <summary>
    /// 创建权限策略。
    /// </summary>
    public PermissionPolicy(string permissionCode, IPermissionChecker checker)
    {
        PermissionCode = !string.IsNullOrWhiteSpace(permissionCode) ? permissionCode : throw new ArgumentException("The permission code must not be empty.", nameof(permissionCode));
        _checker = checker ?? throw new ArgumentNullException(nameof(checker));
    }

    /// <summary>
    /// 权限码。
    /// </summary>
    public string PermissionCode { get; }

    /// <inheritdoc />
    public string Name => NameFor(PermissionCode);

    /// <summary>
    /// 权限码对应的策略名。
    /// </summary>
    public static string NameFor(string permissionCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionCode);
        return Prefix + permissionCode;
    }

    /// <summary>
    /// 从策略名取权限码；不是权限策略返回 <see langword="null"/>。
    /// </summary>
    public static string? CodeOf(string policyName)
        // 前缀之后必须还有内容："permission:" 本身不是权限策略
        => policyName is not null && policyName.StartsWith(Prefix, StringComparison.Ordinal) && policyName.Length > Prefix.Length
            ? policyName[Prefix.Length..]
            : null;

    /// <inheritdoc />
    public async ValueTask<AuthorizationResult> EvaluateAsync(AuthorizationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        // 先判未认证：匿名主体得到 NotAuthenticated 而不是 PermissionMissing，调用方可以区分未登录与越权
        if (!request.Security.Principal.IsAuthenticated)
        {
            return AuthorizationResult.Deny(Error.Denied(SecurityErrors.NotAuthenticated, SecurityMessages.AuthenticationRequired));
        }

        return await _checker.HasPermissionAsync(request.Security, PermissionCode, cancellationToken).ConfigureAwait(false)
            ? AuthorizationResult.Allow()
            : AuthorizationResult.Deny(Error.Denied(SecurityErrors.PermissionMissing, SecurityMessages.PermissionMissing, MessageArguments.Of(("permission", PermissionCode))));
    }
}
