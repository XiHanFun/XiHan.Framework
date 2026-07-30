// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Data.SqlSugar.Clients;
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
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="clientResolver">SqlSugar 客户端解析器</param>
    public SqlSugarAuditedRepository(ISqlSugarClientResolver clientResolver)
        : base(clientResolver)
    {
    }

    /// <summary>
    /// 根据创建者获取实体
    /// </summary>
    /// <param name="createdId">创建者主键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>只读实体集合</returns>
    public async Task<IReadOnlyList<TEntity>> GetByCreatorAsync(TKey createdId, CancellationToken cancellationToken = default)
    {
        // SqlSugar 内部返回 List<T>，符合"内部实现用具体类型"原则
        return await CreateQueryable()
            .Where(entity => entity.CreatedId != null && entity.CreatedId.Equals(createdId))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 根据修改者获取实体
    /// </summary>
    /// <param name="modifiedId">修改者主键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>只读实体集合</returns>
    public async Task<IReadOnlyList<TEntity>> GetByModifierAsync(TKey modifiedId, CancellationToken cancellationToken = default)
    {
        return await CreateQueryable()
            .Where(entity => entity.ModifiedId != null && entity.ModifiedId.Equals(modifiedId))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 根据删除者获取实体
    /// </summary>
    /// <param name="deletedId">删除者主键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>只读实体集合</returns>
    public async Task<IReadOnlyList<TEntity>> GetByDeleterAsync(TKey deletedId, CancellationToken cancellationToken = default)
    {
        return await CreateWithDeletedQueryable()
            .Where(entity => entity.DeletedId != null && entity.DeletedId.Equals(deletedId))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 根据创建时间范围获取实体
    /// </summary>
    /// <param name="startTime">开始时间</param>
    /// <param name="endTime">结束时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>只读实体集合</returns>
    public async Task<IReadOnlyList<TEntity>> GetCreatedBetweenAsync(DateTimeOffset startTime, DateTimeOffset endTime, CancellationToken cancellationToken = default)
    {
        return await CreateQueryable()
            .Where(entity => entity.CreatedTime >= startTime && entity.CreatedTime <= endTime)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 根据修改时间范围获取实体
    /// </summary>
    /// <param name="startTime">开始时间</param>
    /// <param name="endTime">结束时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>只读实体集合</returns>
    public async Task<IReadOnlyList<TEntity>> GetModifiedBetweenAsync(DateTimeOffset startTime, DateTimeOffset endTime, CancellationToken cancellationToken = default)
    {
        return await CreateQueryable()
            .Where(entity => entity.ModifiedTime >= startTime && entity.ModifiedTime <= endTime)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 根据删除时间范围获取实体
    /// </summary>
    /// <param name="startTime">开始时间</param>
    /// <param name="endTime">结束时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>只读实体集合</returns>
    public async Task<IReadOnlyList<TEntity>> GetDeletedBetweenAsync(DateTimeOffset startTime, DateTimeOffset endTime, CancellationToken cancellationToken = default)
    {
        return await CreateWithDeletedQueryable()
            .Where(entity => entity.IsDeleted)
            .Where(entity => entity.DeletedTime >= startTime && entity.DeletedTime <= endTime)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 根据审计查询选项获取实体
    /// </summary>
    /// <param name="options">审计查询选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>只读实体集合</returns>
    public async Task<IReadOnlyList<TEntity>> GetByAuditAsync(AuditQueryOptions<TKey> options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var query = options.IncludeSoftDeleted || options.OnlySoftDeleted
            ? CreateWithDeletedQueryable()
            : CreateQueryable();

        query = query.WhereIF(options.CreatedId is not null, entity => entity.CreatedId != null && entity.CreatedId.Equals(options.CreatedId))
            .WhereIF(options.ModifiedId is not null, entity => entity.ModifiedId != null && entity.ModifiedId.Equals(options.ModifiedId))
            .WhereIF(options.DeletedId is not null, entity => entity.DeletedId != null && entity.DeletedId.Equals(options.DeletedId))
            .WhereIF(options.CreatedTimeStart.HasValue, entity => entity.CreatedTime >= options.CreatedTimeStart)
            .WhereIF(options.CreatedTimeEnd.HasValue, entity => entity.CreatedTime <= options.CreatedTimeEnd)
            .WhereIF(options.ModifiedTimeStart.HasValue, entity => entity.ModifiedTime >= options.ModifiedTimeStart)
            .WhereIF(options.ModifiedTimeEnd.HasValue, entity => entity.ModifiedTime <= options.ModifiedTimeEnd)
            .WhereIF(options.DeletedTimeStart.HasValue, entity => entity.DeletedTime >= options.DeletedTimeStart)
            .WhereIF(options.DeletedTimeEnd.HasValue, entity => entity.DeletedTime <= options.DeletedTimeEnd)
            // 只查软删优先：OnlySoftDeleted=true 时按选项文档忽略 IncludeSoftDeleted——
            // 两个 WhereIF 若独立叠加，OnlySoftDeleted=true + IncludeSoftDeleted=false（默认）会生成
            // IsDeleted AND NOT IsDeleted 的恒假条件，回收站/审计查询恒返回空集
            .WhereIF(options.OnlySoftDeleted, entity => entity.IsDeleted)
            // 其余场景：不包含软删（即只查未删）
            .WhereIF(!options.IncludeSoftDeleted && !options.OnlySoftDeleted, entity => !entity.IsDeleted);

        return await query.ToListAsync(cancellationToken);
    }
}
