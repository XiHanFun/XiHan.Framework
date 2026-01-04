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
using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Repositories;

namespace XiHan.Framework.Data.SqlSugar.Repository;

/// <summary>
/// SqlSugar 仓储基类
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TKey">主键类型</typeparam>
public class SqlSugarRepositoryBase<TEntity, TKey> : SqlSugarReadOnlyRepository<TEntity, TKey>, IRepositoryBase<TEntity, TKey>
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
    public SqlSugarRepositoryBase(ISqlSugarDbContext dbContext) : base(dbContext)
    {
        _dbContext = dbContext;
        _dbClient = _dbContext.GetClient();
        _simpleClient = _dbClient.GetSimpleClient<TEntity>();
    }

    /// <summary>
    /// 添加实体
    /// </summary>
    /// <param name="entity">实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>已添加的实体</returns>
    public async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        cancellationToken.ThrowIfCancellationRequested();

        var result = await _dbClient.Insertable(entity)
            .ExecuteReturnEntityAsync();
        return result;
    }

    /// <summary>
    /// 添加实体并返回主键
    /// </summary>
    /// <param name="entity">实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>主键</returns>
    public async Task<TKey> AddReturnIdAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        cancellationToken.ThrowIfCancellationRequested();

        var result = await _dbClient.Insertable(entity)
            .ExecuteReturnEntityAsync();

        return result.BasicId;
    }

    /// <summary>
    /// 添加实体集合
    /// </summary>
    /// <param name="entities">实体集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>已添加的实体集合</returns>
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

    /// <summary>
    /// 更新实体
    /// </summary>
    /// <param name="entity">实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>已更新的实体</returns>
    public async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        cancellationToken.ThrowIfCancellationRequested();

        await _dbClient.Updateable(entity)
            .ExecuteCommandAsync(cancellationToken);
        return entity;
    }

    /// <summary>
    /// 更新实体
    /// </summary>
    /// <param name="columns">更新列</param>
    /// <param name="whereExpression">条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
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

    /// <summary>
    /// 更新实体集合
    /// </summary>
    /// <param name="entities">实体集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>已更新的实体集合</returns>
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

    /// <summary>
    /// 新增或更新实体
    /// </summary>
    /// <param name="entity">实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
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

    /// <summary>
    /// 批量新增或更新实体
    /// </summary>
    /// <param name="entities">实体集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
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

    /// <summary>
    /// 删除实体
    /// </summary>
    /// <param name="entity">实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    public async Task<bool> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        cancellationToken.ThrowIfCancellationRequested();

        var affectedRows = await _dbClient.Deleteable(entity)
            .ExecuteCommandAsync(cancellationToken);
        return affectedRows > 0;
    }

    /// <summary>
    /// 根据主键删除实体
    /// </summary>
    /// <param name="id">主键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    public async Task<bool> DeleteAsync(TKey id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var affectedRows = await _dbClient.Deleteable<TEntity>()
            .In(id)
            .ExecuteCommandAsync(cancellationToken);
        return affectedRows > 0;
    }

    /// <summary>
    /// 批量删除实体
    /// </summary>
    /// <param name="entities">实体集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
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

    /// <summary>
    /// 批量删除实体
    /// </summary>
    /// <param name="ids">主键集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
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

    /// <summary>
    /// 根据条件删除实体
    /// </summary>
    /// <param name="predicate">条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    public async Task<bool> DeleteAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        cancellationToken.ThrowIfCancellationRequested();

        var affectedRows = await _dbClient.Deleteable<TEntity>()
            .Where(predicate)
            .ExecuteCommandAsync(cancellationToken);
        return affectedRows > 0;
    }
}
