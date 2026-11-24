// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Permissions;

/// <summary>
/// 一条权限授予
/// </summary>
/// <remarks>
/// 范围由 <see cref="TenantId"/> 决定（RFC-0008「RBAC 授予」，ADR-0197）：<see langword="null"/> 是平台授予，只对不带租户的调用生效；<see cref="AnyTenant"/> 是每个租户的授予，对带任何租户的调用生效、对不带租户的调用不生效；租户 ID 只对该租户生效。平台与租户两边的授予互不渗透。主体与权限码是必填的初始化成员，范围是可选的初始化成员，没有位置构造与解构：以后加字段只加可选的初始化成员，不破坏已有的构造处（ADR-0142、ADR-0288）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record PermissionGrant
{
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
    /// <exception cref="ArgumentException">是缺省值</exception>
    public required GrantSubject Subject
    {
        get;
        init => field = value != default ? value : throw new ArgumentException("The subject must not be the default value.", nameof(value));
    }

    /// <summary>
    /// 权限码，或 <see cref="All"/>
    /// </summary>
    /// <exception cref="ArgumentException">为空或空白</exception>
    public required string Permission
    {
        get;
        init => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("The permission code must not be empty.", nameof(value));
    }

    /// <summary>
    /// 范围
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> 为平台，<see cref="AnyTenant"/> 为每个租户，否则为该租户。不给就是平台授予：只对不带租户的调用生效，要在租户里生效用 <see cref="AnyTenant"/> 或租户 ID（ADR-0197）。不是 <see langword="null"/> 时不能是空白：空白既不是平台，也不是哪个租户
    /// </remarks>
    /// <exception cref="ArgumentException">是空白</exception>
    public string? TenantId
    {
        get;
        init => field = value is null || !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("The tenant of a grant must not be blank; use null for a platform grant or '*' for every tenant.", nameof(value));
    }
}
