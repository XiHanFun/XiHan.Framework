// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Auditing.Abstractions.Trail;

/// <summary>
/// 查询审计时的主体过滤
/// </summary>
/// <remarks>
/// 三种之一：不按主体过滤（<see cref="Any"/>）、只要匿名调用的事件（<see cref="Anonymous"/>）、只要某个主体的事件（<see cref="Of"/>）。不用 <see langword="null"/> 表示「不过滤」，因为事件的 <see langword="null"/> 主体就是匿名（RFC-0001「缺失用 null」，ADR-0169）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record AuditPrincipalFilter
{
    /// <summary>
    /// 创建
    /// </summary>
    private AuditPrincipalFilter(bool isAny, string? principalId)
    {
        IsAny = isAny;
        PrincipalId = principalId;
    }

    /// <summary>
    /// 不按主体过滤
    /// </summary>
    public static AuditPrincipalFilter Any { get; } = new(isAny: true, principalId: null);

    /// <summary>
    /// 只要匿名调用的事件
    /// </summary>
    public static AuditPrincipalFilter Anonymous { get; } = new(isAny: false, principalId: null);

    /// <summary>
    /// 是否不按主体过滤
    /// </summary>
    public bool IsAny { get; }

    /// <summary>
    /// 要的主体
    /// </summary>
    /// <remarks>
    /// <see cref="IsAny"/> 为假时，<see langword="null"/> 表示匿名
    /// </remarks>
    public string? PrincipalId { get; }

    /// <summary>
    /// 只要某个主体的事件
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="principalId"/> 为空或空白</exception>
    public static AuditPrincipalFilter Of(string principalId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(principalId);
        return new(isAny: false, principalId);
    }

    /// <summary>
    /// 事件的主体是否满足过滤
    /// </summary>
    public bool Matches(string? principalId) => IsAny || string.Equals(PrincipalId, principalId, StringComparison.Ordinal);
}
