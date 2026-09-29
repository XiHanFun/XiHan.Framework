// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Domain.Entities.Abstracts;

namespace XiHan.Framework.Data.SqlSugar.Extensions;

/// <summary>
/// 租户过滤器的显式清除入口
/// </summary>
/// <remarks>
/// 全局租户过滤按两个类型登记：读共享的 <see cref="IMultiTenantEntity"/> 与严格隔离的 <see cref="IStrictMultiTenantEntity"/>。
/// SqlSugar 的 <c>ClearFilter&lt;T&gt;</c> 只清与 <c>T</c> 完全相同类型登记的过滤器，
/// 只清 <see cref="IMultiTenantEntity"/> 时严格隔离实体仍被收紧在当前作用域，跨租户读取会静默缺数据；
/// 且 <c>ClearFilter</c> 是赋值不是合并，与软删过滤须在同一次调用里一起清。
/// 跨租户读取统一经本入口，不要直接写 <c>ClearFilter&lt;IMultiTenantEntity&gt;()</c>。
/// </remarks>
public static class TenantFilterExtensions
{
    private static readonly Type[] TenantFilterTypes = [typeof(IMultiTenantEntity), typeof(IStrictMultiTenantEntity)];

    private static readonly Type[] TenantAndSoftDeleteFilterTypes = [.. TenantFilterTypes, typeof(ISoftDelete)];

    /// <summary>
    /// 清除租户过滤（读共享与严格隔离一并清除），软删过滤保持生效
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="queryable">查询</param>
    /// <returns>跨全部租户的查询</returns>
    public static ISugarQueryable<T> ClearTenantFilter<T>(this ISugarQueryable<T> queryable)
    {
        ArgumentNullException.ThrowIfNull(queryable);
        return queryable.ClearFilter(TenantFilterTypes);
    }

    /// <summary>
    /// 同时清除租户过滤与软删过滤（恢复、清除等需要看到已删行的跨租户场景）
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="queryable">查询</param>
    /// <returns>跨全部租户且含软删行的查询</returns>
    public static ISugarQueryable<T> ClearTenantAndSoftDeleteFilter<T>(this ISugarQueryable<T> queryable)
    {
        ArgumentNullException.ThrowIfNull(queryable);
        return queryable.ClearFilter(TenantAndSoftDeleteFilterTypes);
    }
}
