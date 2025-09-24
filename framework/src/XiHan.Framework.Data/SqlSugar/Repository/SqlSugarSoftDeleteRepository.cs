#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SqlSugarSoftDeleteRepository
// Guid:8d1a7f90-12f7-4c92-9f15-2b0c40cfe876
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/25 05:56:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using SqlSugar;
using System.Linq.Expressions;
using XiHan.Framework.Data.SqlSugar.Repository.Extensions;
using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Repositories;
using XiHan.Framework.Domain.Specifications.Abstracts;

namespace XiHan.Framework.Data.SqlSugar.Repository;

/// <summary>
/// SqlSugar 软删除仓储实现
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TKey">主键类型</typeparam>
public class SqlSugarSoftDeleteRepository<TEntity, TKey> : SqlSugarRepositoryBase<TEntity, TKey>, ISoftDeleteRepositoryBase<TEntity, TKey>
    where TEntity : class, IEntityBase<TKey>, ISoftDelete, new()
    where TKey : IEquatable<TKey>
{
    private readonly ISqlSugarClient _dbClient;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="dbContext">SqlSugar 数据库上下文</param>
    public SqlSugarSoftDeleteRepository(ISqlSugarDbContext dbContext)
        : base(dbContext)
    {
        _dbClient = dbContext.GetClient();
    }

    #region 软删除

    /// <inheritdoc />
    public async Task SoftDeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        entity.IsDeleted = true;
        if (entity is IDeletionEntity deletionEntity)
        {
            deletionEntity.DeletionTime = DateTimeOffset.UtcNow;
        }

        await _dbClient.Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task SoftDeleteAsync(TKey id, CancellationToken cancellationToken = default)
    {
        var entity = await FindByIdAsync(id, cancellationToken);
        if (entity == null)
        {
            return;
        }

        await SoftDeleteAsync(entity, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SoftDeleteRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);

        var entityArray = entities.ToArray();
        if (entityArray.Length == 0)
        {
            return;
        }

        foreach (var entity in entityArray)
        {
            entity.IsDeleted = true;
            if (entity is IDeletionEntity deletionEntity)
            {
                deletionEntity.DeletionTime = DateTimeOffset.UtcNow;
            }
        }

        await _dbClient.Updateable(entityArray).ExecuteCommandAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task SoftDeleteRangeAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default)
    {
        var idArray = ids.ToArray();
        if (idArray.Length == 0)
        {
            return;
        }

        await _dbClient.Updateable<TEntity>()
            .Where(entity => idArray.Contains(entity.BasicId))
            .SetColumns(entity => entity.IsDeleted == true)
            .SetColumns(entity => ((IDeletionEntity)entity).DeletionTime == DateTimeOffset.UtcNow)
            .ExecuteCommandAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task SoftDeleteRangeAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        await _dbClient.Updateable<TEntity>()
            .Where(predicate)
            .SetColumns(entity => entity.IsDeleted == true)
            .SetColumns(entity => ((IDeletionEntity)entity).DeletionTime == DateTimeOffset.UtcNow)
            .ExecuteCommandAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task SoftDeleteRangeAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        var query = _dbClient.Queryable<TEntity>().ApplySpecification(specification);
        var entities = await query.ToListAsync(cancellationToken);
        await SoftDeleteRangeAsync(entities, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RestoreAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        entity.IsDeleted = false;
        if (entity is IDeletionEntity deletionEntity)
        {
            deletionEntity.DeletionTime = null;
        }

        await _dbClient.Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task RestoreAsync(TKey id, CancellationToken cancellationToken = default)
    {
        var entity = await FindByIdAsync(id, cancellationToken);
        if (entity == null)
        {
            return;
        }

        await RestoreAsync(entity, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RestoreRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);

        var entityArray = entities.ToArray();
        if (entityArray.Length == 0)
        {
            return;
        }

        foreach (var entity in entityArray)
        {
            entity.IsDeleted = false;
            if (entity is IDeletionEntity deletionEntity)
            {
                deletionEntity.DeletionTime = null;
            }
        }

        await _dbClient.Updateable(entityArray).ExecuteCommandAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task RestoreRangeAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default)
    {
        var idArray = ids.ToArray();
        if (idArray.Length == 0)
        {
            return;
        }

        await _dbClient.Updateable<TEntity>()
            .Where(entity => idArray.Contains(entity.BasicId))
            .SetColumns(entity => entity.IsDeleted == false)
            .SetColumns(entity => ((IDeletionEntity)entity).DeletionTime == null)
            .ExecuteCommandAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task RestoreRangeAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        await _dbClient.Updateable<TEntity>()
            .Where(predicate)
            .SetColumns(entity => entity.IsDeleted == false)
            .ExecuteCommandAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task RestoreRangeAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        var query = _dbClient.Queryable<TEntity>().ApplySpecification(specification);
        var entities = await query.ToListAsync(cancellationToken);
        await RestoreRangeAsync(entities, cancellationToken);
    }

    #endregion 软删除

    #region 查询软删除记录

    /// <inheritdoc />
    public async Task<IEnumerable<TEntity>> GetAllWithDeletedAsync(CancellationToken cancellationToken = default)
    {
        return await _dbClient.Queryable<TEntity>().ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TEntity>> GetDeletedAsync(CancellationToken cancellationToken = default)
    {
        return await _dbClient.Queryable<TEntity>().Where(entity => entity.IsDeleted).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TEntity>> GetDeletedAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await _dbClient.Queryable<TEntity>().Where(entity => entity.IsDeleted).Where(predicate).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TEntity>> GetDeletedAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        var query = _dbClient.Queryable<TEntity>().Where(entity => entity.IsDeleted).ApplySpecification(specification);
        return await query.ToListAsync(cancellationToken);
    }

    #endregion 查询软删除记录
}
