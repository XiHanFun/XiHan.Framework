// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Permissions;

/// <summary>
/// 列出授予时的租户过滤
/// </summary>
/// <remarks>
/// 三种之一：不按租户过滤（<see cref="Any"/>）、只要全局授予（<see cref="Global"/>）、只要某个租户的专属授予（<see cref="Of"/>）。不用 <see langword="null"/> 表示「不过滤」，因为契约里 <see langword="null"/> 租户就是全局（RFC-0001「缺失用 null」，ADR-0158）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record GrantTenantFilter
{
    /// <summary>
    /// 创建
    /// </summary>
    private GrantTenantFilter(bool isAny, string? tenantId)
    {
        IsAny = isAny;
        TenantId = tenantId;
    }

    /// <summary>
    /// 不按租户过滤
    /// </summary>
    public static GrantTenantFilter Any { get; } = new(isAny: true, tenantId: null);

    /// <summary>
    /// 只要全局授予
    /// </summary>
    public static GrantTenantFilter Global { get; } = new(isAny: false, tenantId: null);

    /// <summary>
    /// 是否不按租户过滤
    /// </summary>
    public bool IsAny { get; }

    /// <summary>
    /// 要的租户
    /// </summary>
    /// <remarks>
    /// <see cref="IsAny"/> 为假时，<see langword="null"/> 表示全局
    /// </remarks>
    public string? TenantId { get; }

    /// <summary>
    /// 只要某个租户的专属授予
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="tenantId"/> 为空或空白</exception>
    public static GrantTenantFilter Of(string tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        return new(isAny: false, tenantId);
    }

    /// <summary>
    /// 授予的租户是否满足过滤
    /// </summary>
    public bool Matches(string? tenantId) => IsAny || string.Equals(TenantId, tenantId, StringComparison.Ordinal);
}
