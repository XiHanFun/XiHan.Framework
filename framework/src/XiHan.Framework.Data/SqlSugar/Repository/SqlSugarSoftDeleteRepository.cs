#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SqlSugarSoftDeleteRepository
// Guid:8d1a7f90-12f7-4c92-9f15-2b0c40cfe876
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/25 05:56:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Linq.Expressions;
using XiHan.Framework.Data.SqlSugar.Repository.Extensions;
using XiHan.Framework.Data.SqlSugar.SplitTables;
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
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="dbContext">SqlSugar 数据上下文</param>
    /// <param name="splitTableExecutor">分表执行器</param>
    /// <param name="serviceProvider">服务提供者</param>
    public SqlSugarSoftDeleteRepository(
        ISqlSugarDbContext dbContext,
        ISqlSugarSplitTableExecutor splitTableExecutor,
        IServiceProvider serviceProvider)
        : base(dbContext, splitTableExecutor, serviceProvider)
    {
    }

    #region 软删除

    /// <summary>
    /// 软删除实体
    /// </summary>
    /// <param name="entity">实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    public async Task SoftDeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        entity.IsDeleted = true;
        if (entity is IDeletionEntity deletionEntity)
        {
            deletionEntity.DeletedTime = DateTimeOffset.UtcNow;
        }

        await UpdateAsync(entity, cancellationToken);
    }

    /// <summary>
    /// 软删除实体（根据主键）
    /// </summary>
    /// <param name="id">主键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    public async Task SoftDeleteAsync(TKey id, CancellationToken cancellationToken = default)
    {
        var entity = await GetByIdAsync(id, cancellationToken);
        if (entity == null)
        {
            return;
        }

        await SoftDeleteAsync(entity, cancellationToken);
    }

    /// <summary>
    /// 批量软删除实体
    /// </summary>
    /// <param name="entities">实体集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    public async Task SoftDeleteRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);

        // 内部实现使用数组以提高性能
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
                deletionEntity.DeletedTime = DateTimeOffset.UtcNow;
            }
        }

        await UpdateRangeAsync(entityArray, cancellationToken);
    }

    /// <summary>
    /// 批量软删除实体（根据主键）
    /// </summary>
    /// <param name="ids">主键集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    public async Task SoftDeleteRangeAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        // 内部实现使用数组以提高性能
        var idArray = ids.ToArray();
        if (idArray.Length == 0)
        {
            return;
        }

        var entities = await CreateWithDeletedQueryable()
            .Where(entity => idArray.Contains(entity.BasicId))
            .ToListAsync(cancellationToken);
        await SoftDeleteRangeAsync(entities, cancellationToken);
    }

    /// <summary>
    /// 根据条件批量软删除实体
    /// </summary>
    /// <param name="predicate">条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    public async Task SoftDeleteRangeAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var entities = await CreateWithDeletedQueryable()
            .Where(predicate)
            .ToListAsync(cancellationToken);
        await SoftDeleteRangeAsync(entities, cancellationToken);
    }

    /// <summary>
    /// 根据规约批量软删除实体
    /// </summary>
    /// <param name="specification">规约</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    public async Task SoftDeleteRangeAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        var query = CreateTenantQueryable().ApplySpecification(specification);
        var entities = await query.ToListAsync(cancellationToken);
        await SoftDeleteRangeAsync(entities, cancellationToken);
    }

    /// <summary>
    /// 恢复实体
    /// </summary>
    /// <param name="entity">实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    public async Task RestoreAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        entity.IsDeleted = false;
        if (entity is IDeletionEntity deletionEntity)
        {
            deletionEntity.DeletedTime = null;
        }

        await UpdateAsync(entity, cancellationToken);
    }

    /// <summary>
    /// 恢复实体（根据主键）
    /// </summary>
    /// <param name="id">主键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    public async Task RestoreAsync(TKey id, CancellationToken cancellationToken = default)
    {
        var entity = await CreateWithDeletedQueryable()
            .Where(item => item.BasicId.Equals(id))
            .FirstAsync(cancellationToken);
        if (entity == null)
        {
            return;
        }

        await RestoreAsync(entity, cancellationToken);
    }

    /// <summary>
    /// 批量恢复实体
    /// </summary>
    /// <param name="entities">实体集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    public async Task RestoreRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);

        // 内部实现使用数组以提高性能
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
                deletionEntity.DeletedTime = null;
            }
        }

        await UpdateRangeAsync(entityArray, cancellationToken);
    }

    /// <summary>
    /// 批量恢复实体（根据主键）
    /// </summary>
    /// <param name="ids">主键集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    public async Task RestoreRangeAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        // 内部实现使用数组以提高性能
        var idArray = ids.ToArray();
        if (idArray.Length == 0)
        {
            return;
        }

        var entities = await CreateWithDeletedQueryable()
            .Where(entity => idArray.Contains(entity.BasicId))
            .ToListAsync(cancellationToken);
        await RestoreRangeAsync(entities, cancellationToken);
    }

    /// <summary>
    /// 根据条件批量恢复实体
    /// </summary>
    /// <param name="predicate">条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    public async Task RestoreRangeAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var entities = await CreateWithDeletedQueryable()
            .Where(predicate)
            .ToListAsync(cancellationToken);
        await RestoreRangeAsync(entities, cancellationToken);
    }

    /// <summary>
    /// 根据规约批量恢复实体
    /// </summary>
    /// <param name="specification">规约</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    public async Task RestoreRangeAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        var query = CreateWithDeletedQueryable().ApplySpecification(specification);
        var entities = await query.ToListAsync(cancellationToken);
        await RestoreRangeAsync(entities, cancellationToken);
    }

    #endregion 软删除

    #region 查询软删除记录

    /// <summary>
    /// 获取所有软删除实体
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>只读实体集合</returns>
    public async Task<IReadOnlyList<TEntity>> GetAllWithDeletedAsync(CancellationToken cancellationToken = default)
    {
        return await CreateWithDeletedQueryable()
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 获取所有软删除实体
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>只读实体集合</returns>
    public async Task<IReadOnlyList<TEntity>> GetDeletedAsync(CancellationToken cancellationToken = default)
    {
        return await CreateWithDeletedQueryable()
            .Where(entity => entity.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 根据条件获取软删除实体
    /// </summary>
    /// <param name="predicate">条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>只读实体集合</returns>
    public async Task<IReadOnlyList<TEntity>> GetDeletedAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await CreateWithDeletedQueryable()
            .Where(entity => entity.IsDeleted)
            .Where(predicate)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 根据规约获取软删除实体
    /// </summary>
    /// <param name="specification">规约</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>只读实体集合</returns>
    public async Task<IReadOnlyList<TEntity>> GetDeletedAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        return await CreateWithDeletedQueryable()
            .Where(entity => entity.IsDeleted)
            .ApplySpecification(specification)
            .ToListAsync(cancellationToken);
    }

    #endregion 查询软删除记录
}
