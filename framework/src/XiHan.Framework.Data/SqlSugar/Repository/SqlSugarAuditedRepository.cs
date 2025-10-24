#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SqlSugarAuditedRepository
// Guid:2f64a6de-6f7a-4ac6-8a1b-5825ae3a6a4f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/25 06:02:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using SqlSugar;
using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Repositories;
using XiHan.Framework.Domain.Repositories.Models;

namespace XiHan.Framework.Data.SqlSugar.Repository;

/// <summary>
/// SqlSugar 审计仓储
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TKey">主键类型</typeparam>
public class SqlSugarAuditedRepository<TEntity, TKey> : SqlSugarSoftDeleteRepository<TEntity, TKey>, IAuditedRepository<TEntity, TKey>
    where TEntity : class, IFullAuditedEntity<TKey>, new()
    where TKey : IEquatable<TKey>
{
    private readonly ISqlSugarClient _dbClient;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="dbContext">SqlSugar 数据库上下文</param>
    public SqlSugarAuditedRepository(ISqlSugarDbContext dbContext)
        : base(dbContext)
    {
        _dbClient = dbContext.GetClient();
    }

    /// <summary>
    /// 根据创建者获取实体
    /// </summary>
    /// <param name="creatorId">创建者主键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>符合条件的实体集合</returns>
    public async Task<IEnumerable<TEntity>> GetByCreatorAsync(TKey creatorId, CancellationToken cancellationToken = default)
    {
        return await _dbClient.Queryable<TEntity>()
            .Where(entity => entity.CreatorId != null && entity.CreatorId.Equals(creatorId))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 根据修改者获取实体
    /// </summary>
    /// <param name="modifierId">修改者主键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>符合条件的实体集合</returns>
    public async Task<IEnumerable<TEntity>> GetByModifierAsync(TKey modifierId, CancellationToken cancellationToken = default)
    {
        return await _dbClient.Queryable<TEntity>()
            .Where(entity => entity.ModifierId != null && entity.ModifierId.Equals(modifierId))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 根据删除者获取实体
    /// </summary>
    /// <param name="deleterId">删除者主键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>符合条件的实体集合</returns>
    public async Task<IEnumerable<TEntity>> GetByDeleterAsync(TKey deleterId, CancellationToken cancellationToken = default)
    {
        return await _dbClient.Queryable<TEntity>()
            .Where(entity => entity.DeleterId != null && entity.DeleterId.Equals(deleterId))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 根据创建时间范围获取实体
    /// </summary>
    /// <param name="startTime">开始时间</param>
    /// <param name="endTime">结束时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>符合条件的实体集合</returns>
    public async Task<IEnumerable<TEntity>> GetCreatedBetweenAsync(DateTimeOffset startTime, DateTimeOffset endTime, CancellationToken cancellationToken = default)
    {
        return await _dbClient.Queryable<TEntity>()
            .Where(entity => entity.CreationTime >= startTime && entity.CreationTime <= endTime)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 根据修改时间范围获取实体
    /// </summary>
    /// <param name="startTime">开始时间</param>
    /// <param name="endTime">结束时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>符合条件的实体集合</returns>
    public async Task<IEnumerable<TEntity>> GetModifiedBetweenAsync(DateTimeOffset startTime, DateTimeOffset endTime, CancellationToken cancellationToken = default)
    {
        return await _dbClient.Queryable<TEntity>()
            .Where(entity => entity.ModificationTime >= startTime && entity.ModificationTime <= endTime)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 根据删除时间范围获取实体
    /// </summary>
    /// <param name="startTime">开始时间</param>
    /// <param name="endTime">结束时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>符合条件的实体集合</returns>
    public async Task<IEnumerable<TEntity>> GetDeletedBetweenAsync(DateTimeOffset startTime, DateTimeOffset endTime, CancellationToken cancellationToken = default)
    {
        return await _dbClient.Queryable<TEntity>()
            .Where(entity => entity.DeletionTime >= startTime && entity.DeletionTime <= endTime)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 根据审计查询选项获取实体
    /// </summary>
    /// <param name="options">审计查询选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>符合条件的实体集合</returns>
    public async Task<IEnumerable<TEntity>> GetByAuditAsync(AuditQueryOptions<TKey> options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var query = _dbClient.Queryable<TEntity>();

        if (options.CreatorId is not null)
        {
            query = query.Where(entity => entity.CreatorId != null && entity.CreatorId.Equals(options.CreatorId));
        }

        if (options.ModifierId is not null)
        {
            query = query.Where(entity => entity.ModifierId != null && entity.ModifierId.Equals(options.ModifierId));
        }

        if (options.DeleterId is not null)
        {
            query = query.Where(entity => entity.DeleterId != null && entity.DeleterId.Equals(options.DeleterId));
        }

        if (options.CreatedTimeStart.HasValue)
        {
            query = query.Where(entity => entity.CreationTime >= options.CreatedTimeStart.Value);
        }

        if (options.CreatedTimeEnd.HasValue)
        {
            query = query.Where(entity => entity.CreationTime <= options.CreatedTimeEnd.Value);
        }

        if (options.ModifiedTimeStart.HasValue)
        {
            query = query.Where(entity => entity.ModificationTime >= options.ModifiedTimeStart.Value);
        }

        if (options.ModifiedTimeEnd.HasValue)
        {
            query = query.Where(entity => entity.ModificationTime <= options.ModifiedTimeEnd.Value);
        }

        if (options.DeletedTimeStart.HasValue)
        {
            query = query.Where(entity => entity.DeletionTime >= options.DeletedTimeStart.Value);
        }

        if (options.DeletedTimeEnd.HasValue)
        {
            query = query.Where(entity => entity.DeletionTime <= options.DeletedTimeEnd.Value);
        }

        if (options.OnlySoftDeleted)
        {
            query = query.Where(entity => entity.IsDeleted);
        }
        else if (!options.IncludeSoftDeleted)
        {
            query = query.Where(entity => !entity.IsDeleted);
        }

        return await query.ToListAsync(cancellationToken);
    }
}
