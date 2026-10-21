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
    private readonly IPermissionGrantStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly SecurityOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc />
    public async ValueTask<bool> HasPermissionAsync(SecurityContext security, string permission, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(security);
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        if (!security.Principal.IsAuthenticated)
        {
            return false;
        }

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
