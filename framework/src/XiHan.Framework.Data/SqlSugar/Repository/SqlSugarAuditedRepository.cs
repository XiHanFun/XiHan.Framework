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
    /// <param name="createdId">创建者主键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>符合条件的实体集合</returns>
    public async Task<List<TEntity>> GetByCreatorAsync(TKey createdId, CancellationToken cancellationToken = default)
    {
        return await _dbClient.Queryable<TEntity>()
            .Where(entity => entity.CreatedId != null && entity.CreatedId.Equals(createdId))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 根据修改者获取实体
    /// </summary>
    /// <param name="modifiedId">修改者主键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>符合条件的实体集合</returns>
    public async Task<List<TEntity>> GetByModifierAsync(TKey modifiedId, CancellationToken cancellationToken = default)
    {
        return await _dbClient.Queryable<TEntity>()
            .Where(entity => entity.ModifiedId != null && entity.ModifiedId.Equals(modifiedId))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 根据删除者获取实体
    /// </summary>
    /// <param name="deletedId">删除者主键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>符合条件的实体集合</returns>
    public async Task<List<TEntity>> GetByDeleterAsync(TKey deletedId, CancellationToken cancellationToken = default)
    {
        return await _dbClient.Queryable<TEntity>()
            .Where(entity => entity.DeletedId != null && entity.DeletedId.Equals(deletedId))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 根据创建时间范围获取实体
    /// </summary>
    /// <param name="startTime">开始时间</param>
    /// <param name="endTime">结束时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>符合条件的实体集合</returns>
    public async Task<List<TEntity>> GetCreatedBetweenAsync(DateTimeOffset startTime, DateTimeOffset endTime, CancellationToken cancellationToken = default)
    {
        return await _dbClient.Queryable<TEntity>()
            .Where(entity => entity.CreatedTime >= startTime && entity.CreatedTime <= endTime)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 根据修改时间范围获取实体
    /// </summary>
    /// <param name="startTime">开始时间</param>
    /// <param name="endTime">结束时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>符合条件的实体集合</returns>
    public async Task<List<TEntity>> GetModifiedBetweenAsync(DateTimeOffset startTime, DateTimeOffset endTime, CancellationToken cancellationToken = default)
    {
        return await _dbClient.Queryable<TEntity>()
            .Where(entity => entity.ModifiedTime >= startTime && entity.ModifiedTime <= endTime)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 根据删除时间范围获取实体
    /// </summary>
    /// <param name="startTime">开始时间</param>
    /// <param name="endTime">结束时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>符合条件的实体集合</returns>
    public async Task<List<TEntity>> GetDeletedBetweenAsync(DateTimeOffset startTime, DateTimeOffset endTime, CancellationToken cancellationToken = default)
    {
        return await _dbClient.Queryable<TEntity>()
            .Where(entity => entity.DeletedTime >= startTime && entity.DeletedTime <= endTime)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 根据审计查询选项获取实体
    /// </summary>
    /// <param name="options">审计查询选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>符合条件的实体集合</returns>
    public async Task<List<TEntity>> GetByAuditAsync(AuditQueryOptions<TKey> options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var query = _dbClient.Queryable<TEntity>();

        query = query.WhereIF(options.CreatedId is not null, entity => entity.CreatedId != null && entity.CreatedId.Equals(options.CreatedId))
            .WhereIF(options.ModifiedId is not null, entity => entity.ModifiedId != null && entity.ModifiedId.Equals(options.ModifiedId))
            .WhereIF(options.DeletedId is not null, entity => entity.DeletedId != null && entity.DeletedId.Equals(options.DeletedId))
            .WhereIF(options.CreatedTimeStart.HasValue, entity => entity.CreatedTime >= options.CreatedTimeStart)
            .WhereIF(options.CreatedTimeEnd.HasValue, entity => entity.CreatedTime <= options.CreatedTimeEnd)
            .WhereIF(options.ModifiedTimeStart.HasValue, entity => entity.ModifiedTime >= options.ModifiedTimeStart)
            .WhereIF(options.ModifiedTimeEnd.HasValue, entity => entity.ModifiedTime <= options.ModifiedTimeEnd)
            .WhereIF(options.DeletedTimeStart.HasValue, entity => entity.DeletedTime >= options.DeletedTimeStart)
            .WhereIF(options.DeletedTimeEnd.HasValue, entity => entity.DeletedTime <= options.DeletedTimeEnd)
            // 只要只查软删，优先处理
            .WhereIF(options.OnlySoftDeleted, entity => entity.IsDeleted)
            // 否则，如果不包含软删（即查未软删）
            .WhereIF(!options.IncludeSoftDeleted, entity => !entity.IsDeleted);

        return await query.ToListAsync(cancellationToken);
    }
}
