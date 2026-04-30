#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ISplitRepositoryBase
// Guid:4f1b9d82-e1a4-4c8a-9b3f-0c1d7a2e5b6d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/17 10:40:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Linq.Expressions;
using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Shared.Paging.Dtos;

namespace XiHan.Framework.Domain.Repositories;

/// <summary>
/// 分表仓储基类接口
/// </summary>
/// <remarks>
/// 分表实体的查询/写入必须经过这里，语义与普通仓储严格区分：
/// <list type="bullet">
///   <item>按主键查询：通过雪花 ID 反推时间定位到单个分片，避免全分片扫描。</item>
///   <item>列表查询：必须提供时间范围（<c>beginTime</c>/<c>endTime</c>），SqlSugar 按范围枚举分片。</item>
///   <item>全量扫描：必须显式调用 <c>ScanAllAsync</c>，用于离线数据分析等场景。</item>
/// </list>
/// 常规仓储（IRepositoryBase）不要和分表实体配对使用，否则只会命中默认表，导致数据丢失或查询不准。
/// 分表实体主键固定为 <see cref="long"/>（雪花 ID，可反推时间）。
/// </remarks>
/// <typeparam name="TEntity">实体类型（必须实现 <see cref="ISplitTableEntity"/>）</typeparam>
public interface ISplitRepositoryBase<TEntity>
    where TEntity : class, IEntityBase<long>, ISplitTableEntity, new()
{
    #region 写入

    /// <summary>
    /// 添加实体（自动路由到对应分片）
    /// </summary>
    Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量添加实体（按分片自动路由）
    /// </summary>
    Task<IReadOnlyList<TEntity>> AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    #endregion

    #region 更新

    /// <summary>
    /// 更新实体（自动路由到对应分片）
    /// </summary>
    Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量更新实体（按分片自动路由）
    /// </summary>
    Task<IReadOnlyList<TEntity>> UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    #endregion

    #region 按主键查询（雪花 ID 反推时间定位分片）

    /// <summary>
    /// 根据主键获取实体（通过 ID 反推时间定位分片）
    /// </summary>
    Task<TEntity?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据主键集合获取实体（最小化扫描分片数量）
    /// </summary>
    Task<IReadOnlyList<TEntity>> GetByIdsAsync(IEnumerable<long> ids, CancellationToken cancellationToken = default);

    #endregion

    #region 按时间范围查询

    /// <summary>
    /// 根据时间范围获取实体列表（必填时间范围，防止误触发全分片扫描）
    /// </summary>
    Task<IReadOnlyList<TEntity>> GetListByTimeRangeAsync(
        DateTimeOffset beginTime,
        DateTimeOffset endTime,
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据时间范围分页查询
    /// </summary>
    Task<PageResultDtoBase<TEntity>> GetPagedByTimeRangeAsync(
        int pageIndex,
        int pageSize,
        DateTimeOffset beginTime,
        DateTimeOffset endTime,
        Expression<Func<TEntity, bool>>? predicate = null,
        Expression<Func<TEntity, object>>? orderBy = null,
        bool isAscending = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据时间范围统计数量
    /// </summary>
    Task<long> CountByTimeRangeAsync(
        DateTimeOffset beginTime,
        DateTimeOffset endTime,
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default);

    #endregion

    #region 全分片扫描（谨慎使用）

    /// <summary>
    /// 全分片扫描获取所有数据（仅用于离线分析等场景，生产环境慎用）
    /// </summary>
    Task<IReadOnlyList<TEntity>> ScanAllAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 全分片扫描统计
    /// </summary>
    Task<long> ScanCountAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default);

    #endregion

    #region 删除

    /// <summary>
    /// 根据主键删除（通过 ID 反推时间定位分片）
    /// </summary>
    Task<bool> DeleteByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按实体删除（自动路由到对应分片）
    /// </summary>
    Task<bool> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量按实体删除（按分片自动路由）
    /// </summary>
    Task<bool> DeleteRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据时间范围 + 条件删除（必填时间范围，避免误全量删除）
    /// </summary>
    Task<int> DeleteByTimeRangeAsync(
        DateTimeOffset beginTime,
        DateTimeOffset endTime,
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    #endregion
}
