#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SqlSugarSplitRepository
// Guid:a5c2d7f9-1b48-4e3a-9c5a-8f0d1e2b6a3c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/17 11:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using SqlSugar;
using System.Linq.Expressions;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.Data.SqlSugar.SplitTables;
using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Repositories;
using XiHan.Framework.Domain.Shared.Paging.Dtos;
using XiHan.Framework.Domain.Shared.Paging.Models;

namespace XiHan.Framework.Data.SqlSugar.Repository;

/// <summary>
/// SqlSugar 分表仓储实现
/// </summary>
/// <remarks>
/// 仅负责分表实体的持久化：
/// <list type="bullet">
///   <item>写入：依赖 SqlSugar 的 <c>.SplitTable()</c> 按实体 <c>CreatedTime</c> 自动路由到分片。</item>
///   <item>按 ID 查询：通过 <see cref="ISplitTableLocator"/> 反推时间，给出一个窄窗口避免全分片扫描。</item>
///   <item>范围查询：用户强制传入时间范围，SqlSugar 按范围枚举分片。</item>
///   <item>全分片扫描：显式 <c>ScanAllAsync</c> 才执行，防止误扫描。</item>
/// </list>
/// 租户过滤/软删过滤通过 <c>SqlSugarScope.QueryFilter</c> 全局 AOP 生效，无需业务侧处理。
/// </remarks>
/// <typeparam name="TEntity">分表实体类型（主键固定为 long 雪花 ID）</typeparam>
public class SqlSugarSplitRepository<TEntity> : ISplitRepositoryBase<TEntity>
    where TEntity : class, IEntityBase<long>, ISplitTableEntity, new()
{
    /// <summary>
    /// 审计开关业务对象：传入实体 Type 供 AOP 辨识该条 Diff 属于哪个实体类型
    /// </summary>
    private static readonly Type AuditBusinessData = typeof(TEntity);

    /// <summary>
    /// ID 反推时间后定位分片的窗口半径（考虑分片边界时钟差，双向各扩 1 分钟）
    /// </summary>
    private static readonly TimeSpan LocateWindow = TimeSpan.FromMinutes(1);

    private readonly ISqlSugarClientResolver _clientResolver;
    private readonly ISplitTableLocator _locator;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="clientResolver">SqlSugar 客户端解析器</param>
    /// <param name="locator">分表定位器</param>
    public SqlSugarSplitRepository(ISqlSugarClientResolver clientResolver, ISplitTableLocator locator)
    {
        _clientResolver = clientResolver;
        _locator = locator;
    }

    /// <summary>
    /// 当前租户对应的 SqlSugar 客户端
    /// </summary>
    protected ISqlSugarClient DbClient => _clientResolver.GetCurrentClient();

    #region 写入

    /// <summary>
    /// 添加实体（自动路由到对应分片）
    /// </summary>
    public async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        cancellationToken.ThrowIfCancellationRequested();

        EnsureCreatedTimeForInsert(entity);
        await DbClient.Insertable(entity)
            .SplitTable()
            .ExecuteCommandAsync();
        return entity;
    }

    /// <summary>
    /// 批量添加实体（按分片自动路由）
    /// </summary>
    public async Task<IReadOnlyList<TEntity>> AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);

        var array = entities.ToArray();
        if (array.Length == 0)
        {
            return [];
        }

        cancellationToken.ThrowIfCancellationRequested();

        foreach (var entity in array)
        {
            EnsureCreatedTimeForInsert(entity);
        }

        await DbClient.Insertable(array)
            .SplitTable()
            .ExecuteCommandAsync();
        return array;
    }

    #endregion

    #region 更新

    /// <summary>
    /// 更新实体（自动路由到对应分片）
    /// </summary>
    public async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        cancellationToken.ThrowIfCancellationRequested();

        EnsureCreatedTimeForExisting(entity);
        await DbClient.Updateable(entity)
            .SplitTable()
            .ExecuteCommandAsync();
        return entity;
    }

    /// <summary>
    /// 批量更新实体（按分片自动路由）
    /// </summary>
    public async Task<IReadOnlyList<TEntity>> UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);

        var array = entities.ToArray();
        if (array.Length == 0)
        {
            return [];
        }

        cancellationToken.ThrowIfCancellationRequested();

        foreach (var entity in array)
        {
            EnsureCreatedTimeForExisting(entity);
        }

        await DbClient.Updateable(array)
            .SplitTable()
            .ExecuteCommandAsync();
        return array;
    }

    #endregion

    #region 按主键查询

    /// <summary>
    /// 根据主键获取实体（通过 ID 反推时间定位分片）
    /// </summary>
    public async Task<TEntity?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (begin, end) = ResolveLocateRange(id);
        return await DbClient.Queryable<TEntity>()
            .SplitTable(begin, end)
            .Where(entity => entity.BasicId == id)
            .FirstAsync(cancellationToken);
    }

    /// <summary>
    /// 根据主键集合获取实体（最小化扫描分片数量）
    /// </summary>
    public async Task<IReadOnlyList<TEntity>> GetByIdsAsync(IEnumerable<long> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var idArray = ids.ToArray();
        if (idArray.Length == 0)
        {
            return [];
        }

        cancellationToken.ThrowIfCancellationRequested();

        var range = _locator.ExtractTimeRange(idArray)
            ?? throw new InvalidOperationException("无法从分布式 ID 反推时间范围。");

        var begin = range.Begin.Subtract(LocateWindow);
        var end = range.End.Add(LocateWindow);

        return await DbClient.Queryable<TEntity>()
            .SplitTable(begin, end)
            .Where(entity => idArray.Contains(entity.BasicId))
            .ToListAsync(cancellationToken);
    }

    #endregion

    #region 按时间范围查询

    /// <summary>
    /// 根据时间范围获取实体列表（必填时间范围，防止误触发全分片扫描）
    /// </summary>
    public async Task<IReadOnlyList<TEntity>> GetListByTimeRangeAsync(
        DateTimeOffset beginTime,
        DateTimeOffset endTime,
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        EnsureValidRange(beginTime, endTime);
        cancellationToken.ThrowIfCancellationRequested();

        var queryable = BuildRangeQueryable(beginTime, endTime, predicate);
        return await queryable.ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 根据时间范围分页查询
    /// </summary>
    public async Task<PageResultDtoBase<TEntity>> GetPagedByTimeRangeAsync(
        int pageIndex,
        int pageSize,
        DateTimeOffset beginTime,
        DateTimeOffset endTime,
        Expression<Func<TEntity, bool>>? predicate = null,
        Expression<Func<TEntity, object>>? orderBy = null,
        bool isAscending = false,
        CancellationToken cancellationToken = default)
    {
        EnsureValidRange(beginTime, endTime);
        cancellationToken.ThrowIfCancellationRequested();

        var queryable = BuildRangeQueryable(beginTime, endTime, predicate);
        if (orderBy is not null)
        {
            queryable = isAscending ? queryable.OrderBy(orderBy) : queryable.OrderByDescending(orderBy);
        }
        else
        {
            queryable = queryable.OrderBy(entity => entity.CreatedTime, OrderByType.Desc);
        }

        RefAsync<int> totalCount = 0;
        var items = await queryable.ToPageListAsync(pageIndex, pageSize, totalCount, cancellationToken);
        return new PageResultDtoBase<TEntity>(items, new PageResultMetadata(pageIndex, pageSize, totalCount));
    }

    /// <summary>
    /// 根据时间范围统计数量
    /// </summary>
    public async Task<long> CountByTimeRangeAsync(
        DateTimeOffset beginTime,
        DateTimeOffset endTime,
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        EnsureValidRange(beginTime, endTime);
        cancellationToken.ThrowIfCancellationRequested();

        return await BuildRangeQueryable(beginTime, endTime, predicate).CountAsync(cancellationToken);
    }

    #endregion

    #region 全分片扫描

    /// <summary>
    /// 全分片扫描获取所有数据（仅用于离线分析等场景，生产环境慎用）
    /// </summary>
    public async Task<IReadOnlyList<TEntity>> ScanAllAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var queryable = DbClient.Queryable<TEntity>().SplitTable();
        if (predicate is not null)
        {
            queryable = queryable.Where(predicate);
        }

        return await queryable.ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 全分片扫描统计
    /// </summary>
    public async Task<long> ScanCountAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var queryable = DbClient.Queryable<TEntity>().SplitTable();
        if (predicate is not null)
        {
            queryable = queryable.Where(predicate);
        }

        return await queryable.CountAsync(cancellationToken);
    }

    #endregion

    #region 删除

    /// <summary>
    /// 根据主键删除（通过 ID 反推时间定位分片）
    /// </summary>
    public async Task<bool> DeleteByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (begin, end) = ResolveLocateRange(id);
        var affectedRows = await DbClient.Deleteable<TEntity>()
            .Where(entity => entity.BasicId == id)
            .EnableQueryFilter()
            .EnableDiffLogEvent(AuditBusinessData)
            .SplitTable(tables => tables.Where(t => t.Date >= begin && t.Date <= end).ToList())
            .ExecuteCommandAsync();

        return affectedRows > 0;
    }

    /// <summary>
    /// 按实体删除（自动路由到对应分片）
    /// </summary>
    public async Task<bool> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        cancellationToken.ThrowIfCancellationRequested();

        EnsureCreatedTimeForExisting(entity);
        var affectedRows = await DbClient.Deleteable(entity)
            .EnableDiffLogEvent(AuditBusinessData)
            .SplitTable()
            .ExecuteCommandAsync();

        return affectedRows > 0;
    }

    /// <summary>
    /// 批量按实体删除（按分片自动路由）
    /// </summary>
    public async Task<bool> DeleteRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);

        var array = entities.ToArray();
        if (array.Length == 0)
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();

        foreach (var entity in array)
        {
            EnsureCreatedTimeForExisting(entity);
        }

        var affectedRows = await DbClient.Deleteable<TEntity>(array.ToList())
            .SplitTable()
            .ExecuteCommandAsync();

        return affectedRows > 0;
    }

    /// <summary>
    /// 根据时间范围 + 条件删除（必填时间范围，避免误全量删除）
    /// </summary>
    public async Task<int> DeleteByTimeRangeAsync(
        DateTimeOffset beginTime,
        DateTimeOffset endTime,
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        EnsureValidRange(beginTime, endTime);
        cancellationToken.ThrowIfCancellationRequested();

        var begin = beginTime.UtcDateTime;
        var end = endTime.UtcDateTime;

        return await DbClient.Deleteable<TEntity>()
            .Where(predicate)
            .EnableQueryFilter()
            .EnableDiffLogEvent(AuditBusinessData)
            .SplitTable(tables => tables.Where(t => t.Date >= begin && t.Date <= end).ToList())
            .ExecuteCommandAsync();
    }

    #endregion

    #region 辅助

    /// <summary>
    /// 校验时间范围
    /// </summary>
    private static void EnsureValidRange(DateTimeOffset beginTime, DateTimeOffset endTime)
    {
        if (beginTime > endTime)
        {
            throw new ArgumentException("分表查询的起始时间不能晚于结束时间。", nameof(beginTime));
        }
    }

    /// <summary>
    /// 确保实体的 CreatedTime 不为默认值（影响分片路由）
    /// </summary>
    private static void EnsureCreatedTimeForInsert(TEntity entity)
    {
        if (entity.CreatedTime == default)
        {
            entity.CreatedTime = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// 确保已有实体具备 CreatedTime；缺失时从雪花 ID 反推，供更新/删除分片路由使用。
    /// </summary>
    private void EnsureCreatedTimeForExisting(TEntity entity)
    {
        if (entity.CreatedTime != default)
        {
            return;
        }

        entity.CreatedTime = entity.BasicId == 0
            ? DateTimeOffset.UtcNow
            : new DateTimeOffset(DateTime.SpecifyKind(_locator.ExtractTime(entity.BasicId), DateTimeKind.Utc));
    }

    /// <summary>
    /// 构建基于时间范围的查询对象（应用 QueryFilter、SplitTable、可选谓词）
    /// </summary>
    private ISugarQueryable<TEntity> BuildRangeQueryable(
        DateTimeOffset beginTime,
        DateTimeOffset endTime,
        Expression<Func<TEntity, bool>>? predicate)
    {
        var queryable = DbClient.Queryable<TEntity>().SplitTable(beginTime.UtcDateTime, endTime.UtcDateTime);
        if (predicate is not null)
        {
            queryable = queryable.Where(predicate);
        }

        return queryable;
    }

    /// <summary>
    /// 根据 ID 解析定位窗口（ID 反推时间 ± <see cref="LocateWindow"/>）
    /// </summary>
    private (DateTime Begin, DateTime End) ResolveLocateRange(long id)
    {
        var time = DateTime.SpecifyKind(_locator.ExtractTime(id), DateTimeKind.Utc);
        return (time.Subtract(LocateWindow), time.Add(LocateWindow));
    }

    #endregion
}
