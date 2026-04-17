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

    /// <inheritdoc />
    public async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        cancellationToken.ThrowIfCancellationRequested();

        // 分表写入依赖 CreatedTime 路由，为空时先兜底一个当前时间
        EnsureCreatedTime(entity);
        await DbClient.Insertable(entity).SplitTable().ExecuteCommandAsync();
        return entity;
    }

    /// <inheritdoc />
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
            EnsureCreatedTime(entity);
        }

        await DbClient.Insertable(array).SplitTable().ExecuteCommandAsync();
        return array;
    }

    #endregion

    #region 按主键查询

    /// <inheritdoc />
    public async Task<TEntity?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (begin, end) = ResolveLocateRange(id);
        return await DbClient.Queryable<TEntity>()
            .SplitTable(begin, end)
            .Where(entity => entity.BasicId == id)
            .FirstAsync(cancellationToken);
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
    public async Task<bool> DeleteByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (begin, end) = ResolveLocateRange(id);
        var affectedRows = await DbClient.Deleteable<TEntity>()
            .Where(entity => entity.BasicId == id)
            .EnableQueryFilter()
            .SplitTable(tables => tables.Where(t => t.Date >= begin && t.Date <= end).ToList())
            .ExecuteCommandAsync();

        return affectedRows > 0;
    }

    /// <inheritdoc />
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
            .SplitTable(tables => tables.Where(t => t.Date >= begin && t.Date <= end).ToList())
            .ExecuteCommandAsync();
    }

    #endregion

    #region 辅助

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
    private static void EnsureCreatedTime(TEntity entity)
    {
        if (entity.CreatedTime == default)
        {
            entity.CreatedTime = DateTimeOffset.UtcNow;
        }
    }

    #endregion
}
