#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SqlSugarReadOnlyRepository
// Guid:5f3a1f88-6d0e-4ab3-8f41-3a6d9c2b8e74
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/25 05:52:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using SqlSugar;
using System.Linq.Expressions;
using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Repositories;
using XiHan.Framework.Domain.Shared.Paging.Dtos;
using XiHan.Framework.Domain.Shared.Paging.Enums;
using XiHan.Framework.Domain.Shared.Paging.Handlers;
using XiHan.Framework.Domain.Specifications.Abstracts;

namespace XiHan.Framework.Data.SqlSugar.Repository;

/// <summary>
/// SqlSugar 仓储基类
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TKey">主键类型</typeparam>
public class SqlSugarReadOnlyRepository<TEntity, TKey> : IReadOnlyRepositoryBase<TEntity, TKey>
    where TEntity : class, IEntityBase<TKey>, new()
    where TKey : IEquatable<TKey>
{
    private readonly ISqlSugarDbContext _dbContext;
    private readonly ISqlSugarClient _dbClient;
    private readonly ISimpleClient<TEntity> _simpleClient;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="dbContext">SqlSugar 数据库上下文</param>
    public SqlSugarReadOnlyRepository(ISqlSugarDbContext dbContext)
    {
        _dbContext = dbContext;
        _dbClient = _dbContext.GetClient();
        _simpleClient = _dbClient.GetSimpleClient<TEntity>();
    }

    #region 查询

    /// <summary>
    /// 根据主键获取实体
    /// </summary>
    /// <param name="id">主键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实体，如果不存在则返回 <c>null</c></returns>
    public async Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await _simpleClient.GetByIdAsync(id, cancellationToken);
    }

    /// <summary>
    /// 根据主键集合获取实体
    /// </summary>
    /// <param name="ids">主键集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>只读实体集合</returns>
    public async Task<IReadOnlyList<TEntity>> GetByIdsAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        // 内部实现使用 List<T> 以提高性能
        var idList = ids.ToArray();
        if (idList.Length == 0)
        {
            return [];
        }

        cancellationToken.ThrowIfCancellationRequested();

        // SqlSugar 内部返回 List<T>，符合"内部实现用具体类型"原则
        var result = await _simpleClient.GetListAsync(it => idList.Contains(it.BasicId), cancellationToken);
        return result;
    }

    /// <summary>
    /// 根据条件获取实体
    /// </summary>
    /// <param name="predicate">条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实体，如果不存在则返回 <c>null</c></returns>
    public async Task<TEntity?> GetFirstAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        cancellationToken.ThrowIfCancellationRequested();

        return await _simpleClient.GetFirstAsync(predicate, cancellationToken);
    }

    /// <summary>
    /// 根据规约获取实体
    /// </summary>
    /// <param name="specification">规约</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实体，如果不存在则返回 <c>null</c></returns>
    public async Task<TEntity?> GetFirstAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        var query = ApplySpecification(_dbClient.Queryable<TEntity>(), specification);
        cancellationToken.ThrowIfCancellationRequested();

        return await query.FirstAsync(cancellationToken);
    }

    /// <summary>
    /// 获取所有实体
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>只读实体集合</returns>
    public async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // SqlSugar 内部返回 List<T>，符合"内部实现用具体类型"原则
        return await _dbClient.Queryable<TEntity>()
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 根据条件获取所有实体
    /// </summary>
    /// <param name="predicate">条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>只读实体集合</returns>
    public async Task<IReadOnlyList<TEntity>> GetListAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        cancellationToken.ThrowIfCancellationRequested();

        return await _dbClient.Queryable<TEntity>()
            .Where(predicate)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 根据条件和排序获取所有实体
    /// </summary>
    /// <param name="predicate">条件</param>
    /// <param name="orderBy">排序</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>只读实体集合</returns>
    public async Task<IReadOnlyList<TEntity>> GetListAsync(Expression<Func<TEntity, bool>> predicate, Expression<Func<TEntity, object>> orderBy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(orderBy);
        cancellationToken.ThrowIfCancellationRequested();

        return await _dbClient.Queryable<TEntity>()
            .Where(predicate)
            .OrderBy(orderBy)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 根据规约获取所有实体
    /// </summary>
    /// <param name="specification">规约</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>只读实体集合</returns>
    public async Task<IReadOnlyList<TEntity>> GetListAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        var query = ApplySpecification(_dbClient.Queryable<TEntity>(), specification);
        cancellationToken.ThrowIfCancellationRequested();

        return await query.ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 获取实体总数
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实体总数</returns>
    public async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await _dbClient.Queryable<TEntity>()
            .CountAsync(cancellationToken);
    }

    /// <summary>
    /// 根据条件获取实体总数
    /// </summary>
    /// <param name="predicate">条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实体总数</returns>
    public async Task<long> CountAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        cancellationToken.ThrowIfCancellationRequested();

        return await _dbClient.Queryable<TEntity>()
            .Where(predicate)
            .CountAsync(cancellationToken);
    }

    /// <summary>
    /// 根据规约获取实体总数
    /// </summary>
    /// <param name="specification">规约</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实体总数</returns>
    public async Task<long> CountAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        var query = ApplySpecification(_dbClient.Queryable<TEntity>(), specification);
        cancellationToken.ThrowIfCancellationRequested();

        return await query.CountAsync(cancellationToken);
    }

    /// <summary>
    /// 根据条件判断是否存在实体
    /// </summary>
    /// <param name="predicate">条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否存在实体</returns>
    public async Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        cancellationToken.ThrowIfCancellationRequested();

        return await _dbClient.Queryable<TEntity>()
            .Where(predicate)
            .AnyAsync(cancellationToken);
    }

    /// <summary>
    /// 根据规约判断是否存在实体
    /// </summary>
    /// <param name="specification">规约</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否存在实体</returns>
    public async Task<bool> AnyAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        var query = ApplySpecification(_dbClient.Queryable<TEntity>(), specification);
        cancellationToken.ThrowIfCancellationRequested();

        return await query.AnyAsync(cancellationToken);
    }

    #endregion 查询

    #region 规约支持

    /// <summary>
    /// 应用规约
    /// </summary>
    /// <param name="query">查询表达式</param>
    /// <param name="specification">规约</param>
    /// <returns>应用规约后的查询表达式</returns>
    private static ISugarQueryable<TEntity> ApplySpecification(ISugarQueryable<TEntity> query, ISpecification<TEntity> specification)
    {
        ArgumentNullException.ThrowIfNull(specification);

        return query.Where(specification.ToExpression());
    }

    #endregion 规约支持

    #region 分页查询

    /// <summary>
    /// 获取分页数据
    /// </summary>
    /// <param name="pageIndex">页码（从1开始）</param>
    /// <param name="pageSize">每页大小</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分页结果</returns>
    public async Task<PageResponse<TEntity>> GetPagedAsync(int pageIndex, int pageSize, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var query = _dbClient.Queryable<TEntity>();

        // 计算总数
        var totalCount = await query.CountAsync(cancellationToken);

        // 应用分页
        var pageInfo = new PageInfo(pageIndex, pageSize);
        var items = await query
            .Skip(pageInfo.Skip)
            .Take(pageInfo.Take)
            .ToListAsync(cancellationToken);

        return new PageResponse<TEntity>(items, new PageData(pageIndex, pageSize, totalCount));
    }

    /// <summary>
    /// 根据条件获取分页数据
    /// </summary>
    /// <param name="pageIndex">页码（从1开始）</param>
    /// <param name="pageSize">每页大小</param>
    /// <param name="predicate">条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分页结果</returns>
    public async Task<PageResponse<TEntity>> GetPagedAsync(int pageIndex, int pageSize, Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        cancellationToken.ThrowIfCancellationRequested();

        var query = _dbClient.Queryable<TEntity>().Where(predicate);

        // 计算总数
        var totalCount = await query.CountAsync(cancellationToken);

        // 应用分页
        var pageInfo = new PageInfo(pageIndex, pageSize);
        var items = await query
            .Skip(pageInfo.Skip)
            .Take(pageInfo.Take)
            .ToListAsync(cancellationToken);

        return new PageResponse<TEntity>(items, new PageData(pageIndex, pageSize, totalCount));
    }

    /// <summary>
    /// 根据条件获取分页数据（支持排序）
    /// </summary>
    /// <param name="pageIndex">页码（从1开始）</param>
    /// <param name="pageSize">每页大小</param>
    /// <param name="predicate">条件</param>
    /// <param name="orderBy">排序</param>
    /// <param name="isAscending">是否升序</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分页结果</returns>
    public async Task<PageResponse<TEntity>> GetPagedAsync(int pageIndex, int pageSize, Expression<Func<TEntity, bool>>? predicate, Expression<Func<TEntity, object>> orderBy, bool isAscending = true, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(orderBy);
        cancellationToken.ThrowIfCancellationRequested();

        var query = _dbClient.Queryable<TEntity>();

        // 应用条件
        if (predicate != null)
        {
            query = query.Where(predicate);
        }
        // 应用排序
        query = isAscending ? query.OrderBy(orderBy) : query.OrderByDescending(orderBy);

        // 计算总数
        var totalCount = await query.CountAsync(cancellationToken);

        // 应用分页
        var pageInfo = new PageInfo(pageIndex, pageSize);
        var items = await query
            .Skip(pageInfo.Skip)
            .Take(pageInfo.Take)
            .ToListAsync(cancellationToken);

        return new PageResponse<TEntity>(items, new PageData(pageIndex, pageSize, totalCount));
    }

    /// <summary>
    /// 根据规约获取分页数据
    /// </summary>
    /// <param name="pageIndex">页码（从1开始）</param>
    /// <param name="pageSize">每页大小</param>
    /// <param name="specification">规约</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分页结果</returns>
    public async Task<PageResponse<TEntity>> GetPagedAsync(int pageIndex, int pageSize, ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(specification);
        cancellationToken.ThrowIfCancellationRequested();

        var query = ApplySpecification(_dbClient.Queryable<TEntity>(), specification);

        // 计算总数
        var totalCount = await query.CountAsync(cancellationToken);

        // 应用分页
        var pageInfo = new PageInfo(pageIndex, pageSize);
        var items = await query
            .Skip(pageInfo.Skip)
            .Take(pageInfo.Take)
            .ToListAsync(cancellationToken);

        return new PageResponse<TEntity>(items, new PageData(pageIndex, pageSize, totalCount));
    }

    /// <summary>
    /// 根据分页查询对象获取分页数据
    /// </summary>
    /// <param name="query">分页查询对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分页结果</returns>
    public async Task<PageResponse<TEntity>> GetPagedAsync(PageQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        var queryable = _dbClient.Queryable<TEntity>();

        // 应用过滤条件
        queryable = ApplyFilters(queryable, query.Filters);
        // 应用排序
        queryable = ApplySorts(queryable, query.Sorts);

        // 计算总数
        var totalCount = await queryable.CountAsync(cancellationToken);

        // 如果禁用分页，返回所有数据
        if (query.DisablePaging)
        {
            var allItems = await queryable.ToListAsync(cancellationToken);
            return new PageResponse<TEntity>(allItems, new PageData(1, (int)totalCount, totalCount));
        }

        // 应用分页
        var pageInfo = query.ToPageInfo();
        var items = await queryable
            .Skip(pageInfo.Skip)
            .Take(pageInfo.Take)
            .ToListAsync(cancellationToken);

        return new PageResponse<TEntity>(items, new PageData(query.PageIndex, query.PageSize, totalCount));
    }

    /// <summary>
    /// 根据分页查询对象和条件获取分页数据
    /// </summary>
    /// <param name="query">分页查询对象</param>
    /// <param name="predicate">条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分页结果</returns>
    public async Task<PageResponse<TEntity>> GetPagedAsync(PageQuery query, Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(predicate);
        cancellationToken.ThrowIfCancellationRequested();

        var queryable = _dbClient.Queryable<TEntity>().Where(predicate);

        // 应用过滤条件
        queryable = ApplyFilters(queryable, query.Filters);
        // 应用排序
        queryable = ApplySorts(queryable, query.Sorts);

        // 计算总数
        var totalCount = await queryable.CountAsync(cancellationToken);

        // 如果禁用分页，返回所有数据
        if (query.DisablePaging)
        {
            var allItems = await queryable.ToListAsync(cancellationToken);
            return new PageResponse<TEntity>(allItems, new PageData(1, (int)totalCount, totalCount));
        }

        // 应用分页
        var pageInfo = query.ToPageInfo();
        var items = await queryable
            .Skip(pageInfo.Skip)
            .Take(pageInfo.Take)
            .ToListAsync(cancellationToken);

        return new PageResponse<TEntity>(items, new PageData(query.PageIndex, query.PageSize, totalCount));
    }

    /// <summary>
    /// 根据分页查询对象和规约获取分页数据
    /// </summary>
    /// <param name="query">分页查询对象</param>
    /// <param name="specification">规约</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分页结果</returns>
    public async Task<PageResponse<TEntity>> GetPagedAsync(PageQuery query, ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(specification);
        cancellationToken.ThrowIfCancellationRequested();

        var queryable = ApplySpecification(_dbClient.Queryable<TEntity>(), specification);

        // 应用过滤条件
        queryable = ApplyFilters(queryable, query.Filters);
        // 应用排序
        queryable = ApplySorts(queryable, query.Sorts);

        // 计算总数
        var totalCount = await queryable.CountAsync(cancellationToken);

        // 如果禁用分页，返回所有数据
        if (query.DisablePaging)
        {
            var allItems = await queryable.ToListAsync(cancellationToken);
            return new PageResponse<TEntity>(allItems, new PageData(1, (int)totalCount, totalCount));
        }

        // 应用分页
        var pageInfo = query.ToPageInfo();
        var items = await queryable
            .Skip(pageInfo.Skip)
            .Take(pageInfo.Take)
            .ToListAsync(cancellationToken);

        return new PageResponse<TEntity>(items, new PageData(query.PageIndex, query.PageSize, totalCount));
    }

    #endregion 分页查询

    #region PageQuery 支持方法

    /// <summary>
    /// 应用过滤条件
    /// </summary>
    /// <param name="queryable">查询对象</param>
    /// <param name="filters">过滤条件集合</param>
    /// <returns>应用过滤后的查询对象</returns>
    private static ISugarQueryable<TEntity> ApplyFilters(ISugarQueryable<TEntity> queryable, List<SelectCondition>? filters)
    {
        if (filters == null || filters.Count == 0)
        {
            return queryable;
        }

        foreach (var filter in filters)
        {
            var expression = SelectConditionParser<TEntity>.GetSelectConditionParser(filter);
            queryable = queryable.Where(expression);
        }

        return queryable;
    }

    /// <summary>
    /// 应用排序条件
    /// </summary>
    /// <param name="queryable">查询对象</param>
    /// <param name="sorts">排序条件集合</param>
    /// <returns>应用排序后的查询对象</returns>
    private static ISugarQueryable<TEntity> ApplySorts(ISugarQueryable<TEntity> queryable, List<SortCondition>? sorts)
    {
        if (sorts == null || sorts.Count == 0)
        {
            return queryable;
        }

        // 按优先级排序
        var orderedSorts = sorts.OrderBy(s => s.Priority).ToList();

        for (var i = 0; i < orderedSorts.Count; i++)
        {
            var sort = orderedSorts[i];
            var expression = SortConditionParser<TEntity>.GetSortConditionParser(sort);

            if (i == 0)
            {
                // 第一个排序使用 OrderBy 或 OrderByDescending
                queryable = sort.Direction == SortDirection.Asc
                    ? queryable.OrderBy(expression)
                    : queryable.OrderByDescending(expression);
            }
            else
            {
                // 后续排序使用 OrderBy 或 OrderByDescending（SqlSugar 会自动处理为 ThenBy）
                queryable = sort.Direction == SortDirection.Asc
                    ? queryable.OrderBy(expression)
                    : queryable.OrderByDescending(expression);
            }
        }

        return queryable;
    }

    #endregion PageQuery 支持方法
}
