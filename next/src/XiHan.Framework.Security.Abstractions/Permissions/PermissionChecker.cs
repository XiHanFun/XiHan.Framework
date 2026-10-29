// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Permissions;

/// <summary>
/// RBAC 检查器。
/// </summary>
/// <remarks>
/// 用户本身的授予与其角色（来自 <see cref="SecurityOptions.RoleClaim"/> 声明）的授予之并集，存储都没命中时再并上各 <see cref="IPermissionGrantSource"/> 的额外授予；<see cref="PermissionGrant.All"/> 匹配任何权限。授予命中之后再经全部 <see cref="IPermissionFilter"/> 收窄（逐个 AND，任一拒绝即不持有）；匿名主体与授予未命中时不调用过滤器。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class PermissionChecker : IPermissionChecker
{
    /// <summary>
    /// 授予存储。
    /// </summary>
    /// <remarks>
    /// 每次判定按主体与租户查询。
    /// </remarks>
    private readonly IPermissionGrantStore _store;

    /// <summary>
    /// 安全选项。
    /// </summary>
    /// <remarks>
    /// 提供承载角色的声明类型。
    /// </remarks>
    private readonly SecurityOptions _options;

    /// <summary>
    /// 收窄过滤器，按登记顺序。
    /// </summary>
    /// <remarks>
    /// 没有登记时为空。
    /// </remarks>
    private readonly IPermissionFilter[] _filters;

    /// <summary>
    /// 存储之外的授予来源，按登记顺序。
    /// </summary>
    /// <remarks>
    /// 没有登记时为空。
    /// </remarks>
    private readonly IPermissionGrantSource[] _sources;

    /// <summary>
    /// 创建检查器。
    /// </summary>
    /// <remarks>
    /// 容器解析时 <paramref name="filters"/> 与 <paramref name="sources"/> 是全部登记的 <see cref="IPermissionFilter"/> 与 <see cref="IPermissionGrantSource"/>（没有就是空集合）。
    /// </remarks>
    /// <param name="store">授予存储。</param>
    /// <param name="options">安全选项。</param>
    /// <param name="filters">授予命中之后的收窄过滤器；<see langword="null"/> 视为没有。</param>
    /// <param name="sources">存储之外的授予来源；<see langword="null"/> 视为没有。</param>
    public PermissionChecker(IPermissionGrantStore store, SecurityOptions options, IEnumerable<IPermissionFilter>? filters = null, IEnumerable<IPermissionGrantSource>? sources = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _filters = filters is null ? [] : [.. filters];
        if (Array.Exists(_filters, static f => f is null))
        {
            throw new ArgumentException("Permission filters must not contain null.", nameof(filters));
        }

        _sources = sources is null ? [] : [.. sources];
        if (Array.Exists(_sources, static s => s is null))
        {
            throw new ArgumentException("Permission grant sources must not contain null.", nameof(sources));
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> HasPermissionAsync(SecurityContext security, string permission, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(security);
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        // 匿名主体没有任何权限，也不访问存储、不调用过滤器
        if (!security.Principal.IsAuthenticated)
        {
            return false;
        }

        if (!await IsGrantedAsync(security, permission, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        // 授予命中之后才收窄：过滤器只能拿走权限、不能凭空给出；全部放行才算持有
        foreach (var filter in _filters)
        {
            if (!await filter.AllowsAsync(security, permission, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 授予是否命中。
    /// </summary>
    /// <remarks>
    /// 先查用户本身的授予，再逐个角色查，存储都没命中时再逐个查授予来源；任一命中即返回，避免多余的存储与来源访问。
    /// </remarks>
    private async ValueTask<bool> IsGrantedAsync(SecurityContext security, string permission, CancellationToken cancellationToken)
    {
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

        // 存储之外的授予（角色继承、委托等）只能加：命中后与存储授予一样经全部过滤器收窄
        foreach (var source in _sources)
        {
            if (Matches(await source.GetGrantsAsync(security, cancellationToken).ConfigureAwait(false), permission))
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
