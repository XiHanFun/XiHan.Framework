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
        _dbClient = _dbContext.GetClient();
        _simpleClient = _dbClient.GetSimpleClient<TEntity>();
    }

    #region 查询

    /// <inheritdoc />
    public async Task<TEntity?> FindByIdAsync(TKey id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await _simpleClient.GetByIdAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TEntity>> FindByIdsAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var idList = ids.ToArray();
        if (idList.Length == 0)
        {
            return [];
        }

        cancellationToken.ThrowIfCancellationRequested();

        var result = await _simpleClient.GetListAsync(it => idList.Contains(it.BasicId), cancellationToken);
        return result;
    }

    /// <inheritdoc />
    public async Task<TEntity?> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        cancellationToken.ThrowIfCancellationRequested();

        return await _simpleClient.GetFirstAsync(predicate, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TEntity?> FindAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        var query = ApplySpecification(_dbClient.Queryable<TEntity>(), specification);
        cancellationToken.ThrowIfCancellationRequested();

        return await query.FirstAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await _dbClient.Queryable<TEntity>()
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TEntity>> GetAllAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        cancellationToken.ThrowIfCancellationRequested();

        return await _dbClient.Queryable<TEntity>()
            .Where(predicate)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TEntity>> GetAllAsync(Expression<Func<TEntity, bool>> predicate, Expression<Func<TEntity, object>> orderBy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(orderBy);
        cancellationToken.ThrowIfCancellationRequested();

        return await _dbClient.Queryable<TEntity>()
            .Where(predicate)
            .OrderBy(orderBy)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TEntity>> GetAllAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        var query = ApplySpecification(_dbClient.Queryable<TEntity>(), specification);
        cancellationToken.ThrowIfCancellationRequested();

        return await query.ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await _dbClient.Queryable<TEntity>()
            .CountAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> CountAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        cancellationToken.ThrowIfCancellationRequested();

        return await _dbClient.Queryable<TEntity>()
            .Where(predicate)
            .CountAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> CountAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        var query = ApplySpecification(_dbClient.Queryable<TEntity>(), specification);
        cancellationToken.ThrowIfCancellationRequested();

        return await query.CountAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        cancellationToken.ThrowIfCancellationRequested();

        return await _dbClient.Queryable<TEntity>()
            .Where(predicate)
            .AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> AnyAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        var query = ApplySpecification(_dbClient.Queryable<TEntity>(), specification);
        cancellationToken.ThrowIfCancellationRequested();

        return await query.AnyAsync(cancellationToken);
    }

    #endregion 查询

    #region 增删改

    /// <inheritdoc />
    public async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        cancellationToken.ThrowIfCancellationRequested();

        var result = await _dbClient.Insertable(entity)
            .ExecuteReturnEntityAsync();
        return result;
    }

    /// <inheritdoc />
    public async Task<TKey> AddReturnIdAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        cancellationToken.ThrowIfCancellationRequested();

        var result = await _dbClient.Insertable(entity)
            .ExecuteReturnEntityAsync();

        return result.BasicId;
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

        cancellationToken.ThrowIfCancellationRequested();

        await _dbClient.Insertable(entityArray)
            .ExecuteCommandAsync(cancellationToken);
        return entityArray;
    }

    /// <inheritdoc />
    public async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        cancellationToken.ThrowIfCancellationRequested();

        await _dbClient.Updateable(entity)
            .ExecuteCommandAsync(cancellationToken);
        return entity;
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(Expression<Func<TEntity, TEntity>> columns, Expression<Func<TEntity, bool>> whereExpression, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(whereExpression);

        cancellationToken.ThrowIfCancellationRequested();

        await _dbClient.Updateable<TEntity>()
            .SetColumns(columns)
            .Where(whereExpression)
            .ExecuteCommandAsync(cancellationToken);
        return true;
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

        cancellationToken.ThrowIfCancellationRequested();

        await _dbClient.Updateable(entityArray)
            .ExecuteCommandAsync(cancellationToken);
        return entityArray;
    }

    /// <inheritdoc />
    public async Task<bool> AddOrUpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        cancellationToken.ThrowIfCancellationRequested();

        if (entity.IsTransient())
        {
            var insertedRows = await _dbClient.Insertable(entity).ExecuteCommandAsync(cancellationToken);
            return insertedRows > 0;
        }

        var affectedRows = await _dbClient.Updateable(entity)
            .ExecuteCommandAsync(cancellationToken);
        return affectedRows > 0;
    }

    /// <inheritdoc />
    public async Task<bool> AddOrUpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);

        var entityArray = entities.ToArray();
        if (entityArray.Length == 0)
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var addEntities = entityArray.Where(entity => entity.IsTransient()).ToArray();
        var updateEntities = entityArray.Where(entity => !entity.IsTransient()).ToArray();

        if (addEntities.Length == 0 && updateEntities.Length == 0)
        {
            return false;
        }

        var hasChanges = false;

        if (addEntities.Length > 0)
        {
            var insertedRows = await _dbClient.Insertable(addEntities)
                .ExecuteCommandAsync(cancellationToken);
            hasChanges |= insertedRows > 0;
        }

        if (updateEntities.Length > 0)
        {
            var updatedRows = await _dbClient.Updateable(updateEntities)
                .ExecuteCommandAsync(cancellationToken);
            hasChanges |= updatedRows > 0;
        }

        return hasChanges;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        cancellationToken.ThrowIfCancellationRequested();

        var affectedRows = await _dbClient.Deleteable(entity)
            .ExecuteCommandAsync(cancellationToken);
        return affectedRows > 0;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(TKey id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var affectedRows = await _dbClient.Deleteable<TEntity>()
            .In(id)
            .ExecuteCommandAsync(cancellationToken);
        return affectedRows > 0;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);

        var entityArray = entities.ToArray();
        if (entityArray.Length == 0)
        {
            return false;
        }

        var idArray = entityArray.Select(entity => entity.BasicId)
            .Where(id => id is not null)
            .ToArray();

        if (idArray.Length == 0)
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var affectedRows = await _dbClient.Deleteable<TEntity>()
            .In(idArray)
            .ExecuteCommandAsync(cancellationToken);
        return affectedRows > 0;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteRangeAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var idArray = ids.ToArray();
        if (idArray.Length == 0)
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var affectedRows = await _dbClient.Deleteable<TEntity>()
            .In(idArray)
            .ExecuteCommandAsync(cancellationToken);
        return affectedRows > 0;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        cancellationToken.ThrowIfCancellationRequested();

        var affectedRows = await _dbClient.Deleteable<TEntity>()
            .Where(predicate)
            .ExecuteCommandAsync(cancellationToken);
        return affectedRows > 0;
    }

    #endregion 增删改

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
}
