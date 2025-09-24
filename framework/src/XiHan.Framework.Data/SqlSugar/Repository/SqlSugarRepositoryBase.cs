#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SqlSugarRepositoryBase
// Guid:5f3a1f88-6d0e-4ab3-8f41-3a6d9c2b8e74
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/25 05:52:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using SqlSugar;
using System.Linq.Expressions;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Repositories;
using XiHan.Framework.Domain.Specifications.Abstracts;

namespace XiHan.Framework.Data.SqlSugar.Repository;

/// <summary>
/// SqlSugar 仓储基类
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TKey">主键类型</typeparam>
public class SqlSugarRepositoryBase<TEntity, TKey> : IRepositoryBase<TEntity, TKey>, ITransientDependency
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
    public SqlSugarRepositoryBase(ISqlSugarDbContext dbContext)
    {
        _dbContext = dbContext;
        _dbClient = dbContext.GetClient();
        _simpleClient = _dbClient.GetSimpleClient<TEntity>();
    }

    #region 查询

    /// <inheritdoc />
    public async Task<TEntity?> FindByIdAsync(TKey id, CancellationToken cancellationToken = default)
    {
        return await _simpleClient.GetByIdAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TEntity>> FindByIdsAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToArray();
        if (idList.Length == 0)
        {
            return [];
        }

        var result = await _dbClient.Queryable<TEntity>()
            .Where(entity => idList.Contains(entity.BasicId))
            .ToListAsync(cancellationToken);
        return result;
    }

    /// <inheritdoc />
    public async Task<TEntity?> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await _simpleClient.GetSingleAsync(predicate, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TEntity?> FindAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        var query = ApplySpecification(_dbClient.Queryable<TEntity>(), specification);
        return await query.FirstAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _simpleClient.GetListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TEntity>> GetAllAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await _simpleClient.GetListAsync(predicate, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TEntity>> GetAllAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        var query = ApplySpecification(_dbClient.Queryable<TEntity>(), specification);
        return await query.ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        return await _dbClient.Queryable<TEntity>().CountAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> CountAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await _dbClient.Queryable<TEntity>().Where(predicate).CountAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> CountAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        var query = ApplySpecification(_dbClient.Queryable<TEntity>(), specification);
        return await query.CountAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await _dbClient.Queryable<TEntity>().Where(predicate).AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> AnyAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        var query = ApplySpecification(_dbClient.Queryable<TEntity>(), specification);
        return await query.AnyAsync(cancellationToken);
    }

    #endregion 查询

    #region 增删改

    /// <inheritdoc />
    public async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var result = await _dbClient.Insertable(entity).ExecuteReturnEntityAsync();
        return result;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TEntity>> AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);

        var entityArray = entities.ToArray();
        if (entityArray.Length == 0)
        {
            return [];
        }

        await _dbClient.Insertable(entityArray).ExecuteCommandAsync(cancellationToken);
        return entityArray;
    }

    /// <inheritdoc />
    public async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        await _dbClient.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return entity;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TEntity>> UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);

        var entityArray = entities.ToArray();
        if (entityArray.Length == 0)
        {
            return [];
        }

        await _dbClient.Updateable(entityArray).ExecuteCommandAsync(cancellationToken);
        return entityArray;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        await _dbClient.Deleteable(entity).ExecuteCommandAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(TKey id, CancellationToken cancellationToken = default)
    {
        await _dbClient.Deleteable<TEntity>().In(id).ExecuteCommandAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);

        var entityArray = entities.ToArray();
        if (entityArray.Length == 0)
        {
            return;
        }

        var idArray = entityArray
            .Select(entity => entity.BasicId)
            .Where(id => id is not null)
            .ToArray();

        if (idArray.Length == 0)
        {
            return;
        }

        await _dbClient.Deleteable<TEntity>()
            .In(idArray)
            .ExecuteCommandAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteRangeAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default)
    {
        var idArray = ids.ToArray();
        if (idArray.Length == 0)
        {
            return;
        }

        await _dbClient.Deleteable<TEntity>().In(idArray).ExecuteCommandAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        await _dbClient.Deleteable<TEntity>().Where(predicate).ExecuteCommandAsync(cancellationToken);
    }

    #endregion 增删改

    #region 规约支持

    private static ISugarQueryable<TEntity> ApplySpecification(ISugarQueryable<TEntity> query, ISpecification<TEntity> specification)
    {
        ArgumentNullException.ThrowIfNull(specification);

        return query.Where(specification.ToExpression());
    }

    #endregion 规约支持
}
