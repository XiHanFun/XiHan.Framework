// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions;

/// <summary>
/// 授予的主体：用户或角色。
/// </summary>
/// <param name="Kind"><see cref="User"/> 或 <see cref="Role"/>。</param>
/// <param name="Id">用户 ID 或角色名；区分大小写。</param>
[ApiLevel(Stability.Stable, "5.0")]
public readonly record struct GrantSubject(string Kind, string Id)
{
    /// <summary>用户。</summary>
    public const string User = "user";

    /// <summary>角色。</summary>
    public const string Role = "role";

    /// <summary>
    /// 种类。
    /// </summary>
    public string Kind { get; } = Kind is User or Role ? Kind : throw new ArgumentException($"主体种类必须是 {User} 或 {Role}，实际 '{Kind}'。", nameof(Kind));

    /// <summary>
    /// ID。
    /// </summary>
    public string Id { get; } = !string.IsNullOrWhiteSpace(Id) ? Id : throw new ArgumentException("主体 ID 不能为空。", nameof(Id));

    /// <summary>
    /// 用户主体。
    /// </summary>
    public static GrantSubject ForUser(string userId) => new(User, userId);

    /// <summary>
    /// 角色主体。
    /// </summary>
    public static GrantSubject ForRole(string role) => new(Role, role);

    /// <inheritdoc />
    public override string ToString() => $"{Kind}:{Id}";
}

/// <summary>
/// 一条权限授予。<see cref="TenantId"/> 为 <see langword="null"/> 时对所有租户生效，否则只对该租户生效。
/// </summary>
/// <param name="Subject">主体。</param>
/// <param name="Permission">权限码，或 <see cref="All"/>。</param>
/// <param name="TenantId">租户；<see langword="null"/> 为全局。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record PermissionGrant(GrantSubject Subject, string Permission, string? TenantId = null)
{
    /// <summary>
    /// 通配：全部权限。
    /// </summary>
    public const string All = "*";

    /// <summary>
    /// 权限码。
    /// </summary>
    public string Permission { get; } = !string.IsNullOrWhiteSpace(Permission) ? Permission : throw new ArgumentException("权限码不能为空。", nameof(Permission));
}

/// <summary>
/// 权限授予存储，Provider 实现它。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IPermissionGrantStore
{
    /// <summary>
    /// 取主体在租户下的有效授予：全局授予加该租户的授予；<paramref name="tenantId"/> 为 <see langword="null"/> 时只有全局授予。
    /// </summary>
    ValueTask<IReadOnlyCollection<string>> GetGrantsAsync(GrantSubject subject, string? tenantId, CancellationToken cancellationToken);
}

/// <summary>
/// 判定主体是否持有权限。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IPermissionChecker
{
    /// <summary>
    /// 判定。
    /// </summary>
    ValueTask<bool> HasPermissionAsync(SecurityContext security, string permission, CancellationToken cancellationToken);
}

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
