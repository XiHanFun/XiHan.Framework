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
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.Data.SqlSugar.Extensions;
using XiHan.Framework.Data.SqlSugar.Helpers;
using XiHan.Framework.Data.SqlSugar.Repository.Extensions;
using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Repositories;
using XiHan.Framework.Domain.Shared.Paging.Dtos;
using XiHan.Framework.Domain.Shared.Paging.Models;
using XiHan.Framework.Domain.Specifications.Abstracts;

namespace XiHan.Framework.Data.SqlSugar.Repository;

/// <summary>
/// SqlSugar 只读仓储基类
/// </summary>
/// <remarks>
/// 架构分层：
/// <list type="bullet">
///   <item>租户连接选择 → 由 <see cref="ISqlSugarClientResolver"/> + <c>ISqlSugarTenantConnectionResolver</c> 自动解析。</item>
///   <item>租户行级过滤 + 软删过滤 → 由 <c>QueryFilter.AddTableFilter</c> 全局 AOP 统一注入，仓储无感。</item>
///   <item>分表实体 → 禁止使用常规仓储，请改用 <see cref="ISplitRepositoryBase{TEntity}"/>。</item>
/// </list>
/// 仓储方法专注纯业务 CRUD；跨租户/含软删场景使用 <see cref="CreateNoTenantQueryable"/> / <see cref="CreateWithDeletedQueryable"/>。
/// </remarks>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TKey">主键类型</typeparam>
public class SqlSugarReadOnlyRepository<TEntity, TKey> : IReadOnlyRepositoryBase<TEntity, TKey>
    where TEntity : class, IEntityBase<TKey>, new()
    where TKey : IEquatable<TKey>
{
    private readonly ISqlSugarClientResolver _clientResolver;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="clientResolver">SqlSugar 客户端解析器</param>
    public SqlSugarReadOnlyRepository(ISqlSugarClientResolver clientResolver)
    {
        _clientResolver = clientResolver;
        EnsureNotSplitEntity();
    }

    /// <summary>
    /// 当前租户对应的 SqlSugar 客户端（每次访问都按当前租户上下文解析）
    /// </summary>
    protected ISqlSugarClient DbClient => _clientResolver.GetCurrentClient();

    #region 查询构建

    /// <summary>
    /// 创建默认查询（租户/软删过滤器由全局 AOP 自动生效）
    /// </summary>
    protected virtual ISugarQueryable<TEntity> CreateQueryable()
    {
        return DbClient.Queryable<TEntity>();
    }

    /// <summary>
    /// 创建忽略租户过滤的查询（平台管理员跨租户场景使用，需权限校验）
    /// </summary>
    protected virtual ISugarQueryable<TEntity> CreateNoTenantQueryable()
    {
        var queryable = DbClient.Queryable<TEntity>();
        if (SqlSugarEntityTypeHelper.IsMultiTenantEntity<TEntity>())
        {
            queryable = queryable.ClearFilter<IMultiTenantEntity>();
        }
        return queryable;
    }

    /// <summary>
    /// 创建包含软删除数据的查询（审计/恢复场景使用）
    /// </summary>
    protected virtual ISugarQueryable<TEntity> CreateWithDeletedQueryable()
    {
        var queryable = DbClient.Queryable<TEntity>();
        if (SqlSugarEntityTypeHelper.IsSoftDeleteEntity<TEntity>())
        {
            queryable = queryable.ClearFilter<ISoftDelete>();
        }
        return queryable;
    }

    /// <summary>
    /// 创建辅助实体类型的查询（跨实体联查场景）
    /// </summary>
    protected ISugarQueryable<T> CreateQueryable<T>() where T : class, new()
    {
        return DbClient.Queryable<T>();
    }

    #endregion

    #region 查询

    /// <summary>
    /// 根据主键获取实体
    /// </summary>
    public async Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await CreateQueryable()
            .Where(entity => entity.BasicId.Equals(id))
            .FirstAsync(cancellationToken);
    }

    /// <summary>
    /// 根据主键集合获取实体
    /// </summary>
    public async Task<IReadOnlyList<TEntity>> GetByIdsAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var idArray = ids.ToArray();
        if (idArray.Length == 0)
        {
            return [];
        }

        cancellationToken.ThrowIfCancellationRequested();

        return await CreateQueryable()
            .Where(entity => idArray.Contains(entity.BasicId))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 根据条件获取首个实体
    /// </summary>
    public async Task<TEntity?> GetFirstAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        cancellationToken.ThrowIfCancellationRequested();

        return await CreateQueryable()
            .Where(predicate)
            .FirstAsync(cancellationToken);
    }

    /// <summary>
    /// 根据规约获取首个实体
    /// </summary>
    public async Task<TEntity?> GetFirstAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await CreateQueryable().ApplySpecification(specification)
            .FirstAsync(cancellationToken);
    }

    /// <summary>
    /// 获取所有实体
    /// </summary>
    public async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await CreateQueryable().ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 根据条件获取实体列表
    /// </summary>
    public async Task<IReadOnlyList<TEntity>> GetListAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        cancellationToken.ThrowIfCancellationRequested();

        return await CreateQueryable()
            .Where(predicate)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 根据条件获取实体列表（带排序）
    /// </summary>
    public async Task<IReadOnlyList<TEntity>> GetListAsync(Expression<Func<TEntity, bool>> predicate, Expression<Func<TEntity, object>> orderBy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(orderBy);
        cancellationToken.ThrowIfCancellationRequested();

        return await CreateQueryable()
            .Where(predicate)
            .OrderBy(orderBy)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 根据规约获取实体列表
    /// </summary>
    public async Task<IReadOnlyList<TEntity>> GetListAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await CreateQueryable().ApplySpecification(specification)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 获取实体总数
    /// </summary>
    public async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await CreateQueryable().CountAsync(cancellationToken);
    }

    /// <summary>
    /// 根据条件统计
    /// </summary>
    public async Task<long> CountAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        cancellationToken.ThrowIfCancellationRequested();

        return await CreateQueryable()
            .Where(predicate)
            .CountAsync(cancellationToken);
    }

    /// <summary>
    /// 根据规约统计
    /// </summary>
    public async Task<long> CountAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await CreateQueryable().ApplySpecification(specification)
            .CountAsync(cancellationToken);
    }

    /// <summary>
    /// 根据条件判断是否存在
    /// </summary>
    public async Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        cancellationToken.ThrowIfCancellationRequested();

        return await CreateQueryable()
            .Where(predicate)
            .AnyAsync(cancellationToken);
    }

    /// <summary>
    /// 根据规约判断是否存在
    /// </summary>
    public async Task<bool> AnyAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await CreateQueryable().ApplySpecification(specification)
            .AnyAsync(cancellationToken);
    }

    #endregion

    #region 分页查询

    /// <summary>
    /// 分页
    /// </summary>
    public async Task<PageResultDtoBase<TEntity>> GetPagedAsync(int pageIndex, int pageSize, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        RefAsync<int> totalCount = 0;
        var items = await CreateQueryable()
            .ToPageListAsync(pageIndex, pageSize, totalCount, cancellationToken);

        return new PageResultDtoBase<TEntity>(items, new PageResultMetadata(pageIndex, pageSize, totalCount));
    }

    /// <summary>
    /// 条件分页
    /// </summary>
    public async Task<PageResultDtoBase<TEntity>> GetPagedAsync(int pageIndex, int pageSize, Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        cancellationToken.ThrowIfCancellationRequested();

        RefAsync<int> totalCount = 0;
        var items = await CreateQueryable()
            .Where(predicate)
            .ToPageListAsync(pageIndex, pageSize, totalCount, cancellationToken);

        return new PageResultDtoBase<TEntity>(items, new PageResultMetadata(pageIndex, pageSize, totalCount));
    }

    /// <summary>
    /// 条件 + 排序分页
    /// </summary>
    public async Task<PageResultDtoBase<TEntity>> GetPagedAsync(int pageIndex, int pageSize, Expression<Func<TEntity, bool>>? predicate, Expression<Func<TEntity, object>> orderBy, bool isAscending = true, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(orderBy);
        cancellationToken.ThrowIfCancellationRequested();

        var query = CreateQueryable();
        if (predicate is not null)
        {
            query = query.Where(predicate);
        }
        query = isAscending ? query.OrderBy(orderBy) : query.OrderByDescending(orderBy);

        RefAsync<int> totalCount = 0;
        var items = await query.ToPageListAsync(pageIndex, pageSize, totalCount, cancellationToken);

        return new PageResultDtoBase<TEntity>(items, new PageResultMetadata(pageIndex, pageSize, totalCount));
    }

    /// <summary>
    /// 规约分页
    /// </summary>
    public async Task<PageResultDtoBase<TEntity>> GetPagedAsync(int pageIndex, int pageSize, ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(specification);
        cancellationToken.ThrowIfCancellationRequested();

        RefAsync<int> totalCount = 0;
        var items = await CreateQueryable().ApplySpecification(specification)
            .ToPageListAsync(pageIndex, pageSize, totalCount, cancellationToken);

        return new PageResultDtoBase<TEntity>(items, new PageResultMetadata(pageIndex, pageSize, totalCount));
    }

    /// <summary>
    /// 分页请求对象分页
    /// </summary>
    public async Task<PageResultDtoBase<TEntity>> GetPagedAsync(PageRequestDtoBase pageRequestDto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pageRequestDto);
        cancellationToken.ThrowIfCancellationRequested();

        var query = CreateQueryable().ApplyPageRequest(pageRequestDto);
        return await query.ToPageResultAsync(pageRequestDto, cancellationToken);
    }

    /// <summary>
    /// 分页请求 + 条件分页
    /// </summary>
    public async Task<PageResultDtoBase<TEntity>> GetPagedAsync(PageRequestDtoBase pageRequestDto, Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pageRequestDto);
        ArgumentNullException.ThrowIfNull(predicate);
        cancellationToken.ThrowIfCancellationRequested();

        var query = CreateQueryable().Where(predicate).ApplyPageRequest(pageRequestDto);
        return await query.ToPageResultAsync(pageRequestDto, cancellationToken);
    }

    /// <summary>
    /// 分页请求 + 规约分页
    /// </summary>
    public async Task<PageResultDtoBase<TEntity>> GetPagedAsync(PageRequestDtoBase pageRequestDto, ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pageRequestDto);
        ArgumentNullException.ThrowIfNull(specification);
        cancellationToken.ThrowIfCancellationRequested();

        var query = CreateQueryable().ApplySpecification(specification).ApplyPageRequest(pageRequestDto);
        return await query.ToPageResultAsync(pageRequestDto, cancellationToken);
    }

    #endregion

    #region 自动查询分页

    /// <summary>
    /// 自动查询分页（根据 DTO 构建条件）
    /// </summary>
    public async Task<PageResultDtoBase<TEntity>> GetPagedAutoAsync(object queryDto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queryDto);
        cancellationToken.ThrowIfCancellationRequested();
        return await CreateQueryable().ToPageResultAutoAsync(queryDto, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 自动查询分页（带额外条件）
    /// </summary>
    public async Task<PageResultDtoBase<TEntity>> GetPagedAutoAsync(object queryDto, Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queryDto);
        ArgumentNullException.ThrowIfNull(predicate);
        cancellationToken.ThrowIfCancellationRequested();

        return await CreateQueryable().Where(predicate).ToPageResultAutoAsync(queryDto, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 自动查询分页（带规约）
    /// </summary>
    public async Task<PageResultDtoBase<TEntity>> GetPagedAutoAsync(object queryDto, ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queryDto);
        ArgumentNullException.ThrowIfNull(specification);
        cancellationToken.ThrowIfCancellationRequested();

        return await CreateQueryable().ApplySpecification(specification).ToPageResultAutoAsync(queryDto, cancellationToken: cancellationToken);
    }

    #endregion

    /// <summary>
    /// 分表实体防呆：不允许用常规仓储承载
    /// </summary>
    private static void EnsureNotSplitEntity()
    {
        if (SqlSugarEntityTypeHelper.IsSplitTableEntity<TEntity>())
        {
            throw new InvalidOperationException(
                $"实体 {typeof(TEntity).FullName} 是分表实体，请改用 ISplitRepositoryBase<{typeof(TEntity).Name}>。");
        }
    }
}
