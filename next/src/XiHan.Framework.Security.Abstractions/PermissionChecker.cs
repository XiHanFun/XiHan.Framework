// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions;

/// <summary>
/// RBAC 检查器：用户本身的授予与其角色（来自 <see cref="SecurityOptions.RoleClaim"/> 声明）的授予之并集；<see cref="PermissionGrant.All"/> 匹配任何权限。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class PermissionChecker(IPermissionGrantStore store, SecurityOptions options) : IPermissionChecker
{
    /// <summary>
    /// 授予存储；每次判定按主体与租户查询。
    /// </summary>
    private readonly IPermissionGrantStore _store = store ?? throw new ArgumentNullException(nameof(store));

    /// <summary>
    /// 安全选项；提供承载角色的声明类型。
    /// </summary>
    private readonly SecurityOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc />
    public async ValueTask<bool> HasPermissionAsync(SecurityContext security, string permission, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(security);
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        // 匿名主体没有任何权限，也不访问存储
        if (!security.Principal.IsAuthenticated)
        {
            return false;
        }

        // 先查用户本身的授予，再逐个角色查；任一命中即放行，避免多余的存储访问
        var tenantId = security.Tenant?.Id;
        if (Matches(await _store.GetGrantsAsync(GrantSubject.ForUser(security.Principal.Id), tenantId, cancellationToken).ConfigureAwait(false), permission))
        {
            return true;
        }

        foreach (var role in security.Principal.ClaimValues(_options.RoleClaim))
        {
            if (Matches(await _store.GetGrantsAsync(GrantSubject.ForRole(role), tenantId, cancellationToken).ConfigureAwait(false), permission))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 授予集合是否命中：通配 <see cref="PermissionGrant.All"/> 或与权限码精确相等（区分大小写）。
    /// </summary>
    private static bool Matches(IReadOnlyCollection<string> grants, string permission)
    {
        foreach (var grant in grants)
        {
            if (string.Equals(grant, PermissionGrant.All, StringComparison.Ordinal) || string.Equals(grant, permission, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
