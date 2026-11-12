// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Permissions;

/// <summary>
/// 一条权限授予
/// </summary>
/// <remarks>
/// 范围由 <see cref="TenantId"/> 决定（RFC-0008「RBAC 授予」，ADR-0197）：<see langword="null"/> 是平台授予，只对不带租户的调用生效；<see cref="AnyTenant"/> 是每个租户的授予，对带任何租户的调用生效、对不带租户的调用不生效；租户 ID 只对该租户生效。平台与租户两边的授予互不渗透
/// </remarks>
/// <param name="Subject">主体</param>
/// <param name="Permission">权限码，或 <see cref="All"/></param>
/// <param name="TenantId">范围：<see langword="null"/> 为平台，<see cref="AnyTenant"/> 为每个租户，否则为该租户</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record PermissionGrant(GrantSubject Subject, string Permission, string? TenantId)
{
    /// <summary>
    /// 平台授予
    /// </summary>
    /// <remarks>
    /// 只对不带租户的调用生效；要在租户里生效用 <see cref="AnyTenant"/> 或租户 ID（ADR-0197）
    /// </remarks>
    /// <param name="subject">主体</param>
    /// <param name="permission">权限码，或 <see cref="All"/></param>
    public PermissionGrant(GrantSubject subject, string permission)
        : this(subject, permission, null)
    {
    }

    /// <summary>
    /// 通配：全部权限
    /// </summary>
    public const string All = "*";

    /// <summary>
    /// 范围：每个租户
    /// </summary>
    /// <remarks>
    /// 对带任何租户的调用生效，对不带租户的调用不生效。<c>*</c> 不合租户 ID 文法，不会与哪个租户撞名（ADR-0197）
    /// </remarks>
    public const string AnyTenant = "*";

    /// <summary>
    /// 一次调用看得到的范围
    /// </summary>
    /// <remarks>
    /// 不带租户（<see langword="null"/> 或空）的调用只看平台授予（<see langword="null"/>）；带租户的调用看每个租户的授予（<see cref="AnyTenant"/>）与该租户的授予。存储按它取有效授予（ADR-0197）
    /// </remarks>
    /// <param name="tenantId">调用的租户</param>
    /// <exception cref="ArgumentException"><paramref name="tenantId"/> 是 <see cref="AnyTenant"/>：调用总是属于平台或某一个租户</exception>
    public static IReadOnlyList<string?> VisibleScopes(string? tenantId)
    {
        if (string.IsNullOrEmpty(tenantId))
        {
            return _platformScopes;
        }

        return tenantId != AnyTenant
            ? [AnyTenant, tenantId]
            : throw new ArgumentException($"The tenant of a call cannot be '{AnyTenant}'; it is the scope of grants that apply in every tenant.", nameof(tenantId));
    }

    /// <summary>
    /// 不带租户的调用看得到的范围
    /// </summary>
    private static readonly string?[] _platformScopes = [null];

    /// <summary>
    /// 主体
    /// </summary>
    public GrantSubject Subject { get; } = Subject != default ? Subject : throw new ArgumentException("The subject must not be the default value.", nameof(Subject));

    /// <summary>
    /// 权限码
    /// </summary>
    public string Permission { get; } = !string.IsNullOrWhiteSpace(Permission) ? Permission : throw new ArgumentException("The permission code must not be empty.", nameof(Permission));

    /// <summary>
    /// 范围
    /// </summary>
    /// <remarks>
    /// 不是 <see langword="null"/> 时不能是空白：空白既不是平台，也不是哪个租户
    /// </remarks>
    public string? TenantId { get; } = TenantId is null || !string.IsNullOrWhiteSpace(TenantId) ? TenantId : throw new ArgumentException("The tenant of a grant must not be blank; use null for a platform grant or '*' for every tenant.", nameof(TenantId));
}
