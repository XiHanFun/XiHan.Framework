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
using XiHan.Framework.Data.SqlSugar.Extensions;
using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Repositories;
using XiHan.Framework.Domain.Shared.Paging.Dtos;
using XiHan.Framework.Domain.Shared.Paging.Models;
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

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="dbContext">SqlSugar 数据库上下文</param>
    public SqlSugarReadOnlyRepository(ISqlSugarDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// 创建租户隔离查询
    /// </summary>
    /// <returns></returns>
    protected virtual ISugarQueryable<TEntity> CreateTenantQueryable()
    {
        return _dbContext.CreateQueryable<TEntity>();
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

        return await CreateTenantQueryable()
            .Where(entity => entity.BasicId.Equals(id))
            .FirstAsync(cancellationToken);
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

        var result = await CreateTenantQueryable()
            .Where(it => idList.Contains(it.BasicId))
            .ToListAsync(cancellationToken);
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

        return await CreateTenantQueryable()
            .Where(predicate)
            .FirstAsync(cancellationToken);
    }

    /// <summary>
    /// 根据规约获取实体
    /// </summary>
    /// <param name="specification">规约</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实体，如果不存在则返回 <c>null</c></returns>
    public async Task<TEntity?> GetFirstAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        var query = ApplySpecification(CreateTenantQueryable(), specification);
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
        return await CreateTenantQueryable()
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

        return await CreateTenantQueryable()
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

        return await CreateTenantQueryable()
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
        var query = ApplySpecification(CreateTenantQueryable(), specification);
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

        return await CreateTenantQueryable()
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

        return await CreateTenantQueryable()
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
        var query = ApplySpecification(CreateTenantQueryable(), specification);
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

        return await CreateTenantQueryable()
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
        var query = ApplySpecification(CreateTenantQueryable(), specification);
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
    public async Task<PageResultDtoBase<TEntity>> GetPagedAsync(int pageIndex, int pageSize, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var query = CreateTenantQueryable();
        RefAsync<int> totalCount = 0;
        var items = await query
            .ToPageListAsync(pageIndex, pageSize, totalCount, cancellationToken);

        return new PageResultDtoBase<TEntity>(items, new PageResultMetadata(pageIndex, pageSize, totalCount));
    }

    /// <summary>
    /// 根据条件获取分页数据
    /// </summary>
    /// <param name="pageIndex">页码（从1开始）</param>
    /// <param name="pageSize">每页大小</param>
    /// <param name="predicate">条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分页结果</returns>
    public async Task<PageResultDtoBase<TEntity>> GetPagedAsync(int pageIndex, int pageSize, Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        cancellationToken.ThrowIfCancellationRequested();

        var query = CreateTenantQueryable().Where(predicate);
        RefAsync<int> totalCount = 0;
        var items = await query
            .ToPageListAsync(pageIndex, pageSize, totalCount, cancellationToken);

        return new PageResultDtoBase<TEntity>(items, new PageResultMetadata(pageIndex, pageSize, totalCount));
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
    public async Task<PageResultDtoBase<TEntity>> GetPagedAsync(int pageIndex, int pageSize, Expression<Func<TEntity, bool>>? predicate, Expression<Func<TEntity, object>> orderBy, bool isAscending = true, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(orderBy);
        cancellationToken.ThrowIfCancellationRequested();

        var query = CreateTenantQueryable();

        // 应用条件
        if (predicate != null)
        {
            query = query.Where(predicate);
        }
        // 应用排序
        query = isAscending ? query.OrderBy(orderBy) : query.OrderByDescending(orderBy);

        RefAsync<int> totalCount = 0;
        var items = await query
            .ToPageListAsync(pageIndex, pageSize, totalCount, cancellationToken);

        return new PageResultDtoBase<TEntity>(items, new PageResultMetadata(pageIndex, pageSize, totalCount));
    }

    /// <summary>
    /// 根据规约获取分页数据
    /// </summary>
    /// <param name="pageIndex">页码（从1开始）</param>
    /// <param name="pageSize">每页大小</param>
    /// <param name="specification">规约</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分页结果</returns>
    public async Task<PageResultDtoBase<TEntity>> GetPagedAsync(int pageIndex, int pageSize, ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(specification);
        cancellationToken.ThrowIfCancellationRequested();

        var query = ApplySpecification(CreateTenantQueryable(), specification);

        RefAsync<int> totalCount = 0;
        var items = await query
            .ToPageListAsync(pageIndex, pageSize, totalCount, cancellationToken);

        return new PageResultDtoBase<TEntity>(items, new PageResultMetadata(pageIndex, pageSize, totalCount));
    }

    /// <summary>
    /// 根据分页查询对象获取分页数据
    /// </summary>
    /// <param name="pageRequestDto">分页查询对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分页结果</returns>
    public async Task<PageResultDtoBase<TEntity>> GetPagedAsync(PageRequestDtoBase pageRequestDto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pageRequestDto);
        cancellationToken.ThrowIfCancellationRequested();

        var query = CreateTenantQueryable();

        // 使用扩展方法应用完整的查询条件
        query = query.ApplyPageRequest(pageRequestDto);

        // 使用扩展方法转换为分页结果
        return await query.ToPageResultAsync(pageRequestDto, cancellationToken);
    }

    /// <summary>
    /// 根据分页查询对象和条件获取分页数据
    /// </summary>
    /// <param name="pageRequestDto">分页查询对象</param>
    /// <param name="predicate">条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分页结果</returns>
    public async Task<PageResultDtoBase<TEntity>> GetPagedAsync(PageRequestDtoBase pageRequestDto, Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pageRequestDto);
        ArgumentNullException.ThrowIfNull(predicate);
        cancellationToken.ThrowIfCancellationRequested();

        var query = CreateTenantQueryable().Where(predicate);

        // 使用扩展方法应用完整的查询条件
        query = query.ApplyPageRequest(pageRequestDto);

        // 使用扩展方法转换为分页结果
        return await query.ToPageResultAsync(pageRequestDto, cancellationToken);
    }

    /// <summary>
    /// 根据分页查询对象和规约获取分页数据
    /// </summary>
    /// <param name="pageRequestDto">分页查询对象</param>
    /// <param name="specification">规约</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分页结果</returns>
    public async Task<PageResultDtoBase<TEntity>> GetPagedAsync(PageRequestDtoBase pageRequestDto, ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pageRequestDto);
        ArgumentNullException.ThrowIfNull(specification);
        cancellationToken.ThrowIfCancellationRequested();

        var query = ApplySpecification(CreateTenantQueryable(), specification);

        // 使用扩展方法应用完整的查询条件
        query = query.ApplyPageRequest(pageRequestDto);

        // 使用扩展方法转换为分页结果
        return await query.ToPageResultAsync(pageRequestDto, cancellationToken);
    }

    #endregion 分页查询

    #region 自动查询分页

    /// <summary>
    /// 自动查询分页数据（根据查询DTO自动构建条件）
    /// </summary>
    /// <param name="queryDto">查询DTO对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分页结果</returns>
    public async Task<PageResultDtoBase<TEntity>> GetPagedAutoAsync(object queryDto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queryDto);
        cancellationToken.ThrowIfCancellationRequested();

        var queryable = CreateTenantQueryable();

        // 使用扩展方法自动构建和执行查询
        return await queryable.ToPageResultAutoAsync(queryDto, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 自动查询分页数据（带额外条件）
    /// </summary>
    /// <param name="queryDto">查询DTO对象</param>
    /// <param name="predicate">额外的条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分页结果</returns>
    public async Task<PageResultDtoBase<TEntity>> GetPagedAutoAsync(object queryDto, Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queryDto);
        ArgumentNullException.ThrowIfNull(predicate);
        cancellationToken.ThrowIfCancellationRequested();

        var queryable = CreateTenantQueryable().Where(predicate);

        return await queryable.ToPageResultAutoAsync(queryDto, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 自动查询分页数据（带规约）
    /// </summary>
    /// <param name="queryDto">查询DTO对象</param>
    /// <param name="specification">规约</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分页结果</returns>
    public async Task<PageResultDtoBase<TEntity>> GetPagedAutoAsync(object queryDto, ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queryDto);
        ArgumentNullException.ThrowIfNull(specification);
        cancellationToken.ThrowIfCancellationRequested();

        var queryable = ApplySpecification(CreateTenantQueryable(), specification);

        return await queryable.ToPageResultAutoAsync(queryDto, cancellationToken: cancellationToken);
    }

    #endregion 自动查询分页
}
