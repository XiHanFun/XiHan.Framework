// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;

namespace XiHan.Framework.Persistence.EntityFrameworkCore;

/// <summary>
/// 多租户实体的全局查询过滤器读的租户
/// </summary>
/// <remarks>
/// 工作单元开始时把它的租户记到自己的 DbContext 上，过滤器按每次查询的上下文取值（ADR-0224）。没有记过的上下文（应用自己建的、不在工作单元里的）不过滤
/// </remarks>
internal static class TenantQueryFilters
{
    /// <summary>
    /// DbContext → 工作单元的租户
    /// </summary>
    /// <remarks>
    /// 弱引用：上下文被回收即一并回收，不随作用域累积
    /// </remarks>
    private static readonly ConditionalWeakTable<DbContext, Slot> _tenants = [];

    /// <summary>
    /// 记下上下文所在工作单元的租户
    /// </summary>
    public static void Bind(DbContext context, string? tenantId) => _tenants.AddOrUpdate(context, new Slot(tenantId));

    /// <summary>
    /// 过滤器是否作用于这个上下文
    /// </summary>
    /// <remarks>
    /// 只在查询时由过滤器调用：参数是执行查询的上下文
    /// </remarks>
    public static bool Applies(DbContext context) => _tenants.TryGetValue(context, out _);

    /// <summary>
    /// 上下文所在工作单元的租户
    /// </summary>
    /// <remarks>
    /// 没有记过或不带租户时为空：多租户实体的租户从不为空，空租户看不到任何行，与仓储同一规则
    /// </remarks>
    public static string? TenantOf(DbContext context) => _tenants.TryGetValue(context, out var slot) ? slot.TenantId : null;

    /// <summary>
    /// 一个上下文记下的租户
    /// </summary>
    /// <param name="TenantId">工作单元的租户</param>
    private sealed record Slot(string? TenantId);
}
