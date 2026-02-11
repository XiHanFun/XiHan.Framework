#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SqlSugarRepositoryBase
// Guid:5f3a1f88-6d0e-4ab3-8f41-3a6d9c2b8e74
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/25 05:52:00
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

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="dbContext">SqlSugar 数据库上下文</param>
    public SqlSugarRepositoryBase(ISqlSugarDbContext dbContext) : base(dbContext)
    {
        _dbContext = dbContext;
    }

    private ISqlSugarClient DbClient => _dbContext.GetClient();

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
        _dbContext.TrySetTenantId(entity);

        var result = await DbClient.Insertable(entity)
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
        _dbContext.TrySetTenantId(entity);

        var result = await DbClient.Insertable(entity)
            .ExecuteReturnEntityAsync();

        return result.BasicId;
    }

    /// <summary>
    /// 添加实体集合
    /// </summary>
    /// <param name="entities">实体集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>只读实体集合</returns>
    public async Task<IReadOnlyList<TEntity>> AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);

        // 内部实现使用 Array<T> 以提高性能
        var entityList = entities.ToArray();
        if (entityList.Length == 0)
        {
            return [];
        }

        cancellationToken.ThrowIfCancellationRequested();
        foreach (var item in entityList)
        {
            _dbContext.TrySetTenantId(item);
        }

        await DbClient.Insertable(entityList)
            .ExecuteCommandAsync(cancellationToken);
        return entityList;
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
        _dbContext.TrySetTenantId(entity);

        await DbClient.Updateable(entity)
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

        await DbClient.Updateable<TEntity>()
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
    /// <returns>只读实体集合</returns>
    public async Task<IReadOnlyList<TEntity>> UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);

        // 内部实现使用 List<T> 以提高性能
        var entityList = entities.ToList();
        if (entityList.Count == 0)
        {
            return [];
        }

        cancellationToken.ThrowIfCancellationRequested();
        foreach (var item in entityList)
        {
            _dbContext.TrySetTenantId(item);
        }

        await DbClient.Updateable(entityList)
            .ExecuteCommandAsync(cancellationToken);
        return entityList;
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
        _dbContext.TrySetTenantId(entity);

        if (entity.IsTransient())
        {
            var insertedRows = await DbClient.Insertable(entity).ExecuteCommandAsync(cancellationToken);
            return insertedRows > 0;
        }

        var affectedRows = await DbClient.Updateable(entity)
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

        // 内部实现使用数组以提高性能
        var entityArray = entities.ToArray();
        if (entityArray.Length == 0)
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var addEntities = entityArray.Where(entity => entity.IsTransient()).ToArray();
        var updateEntities = entityArray.Where(entity => !entity.IsTransient()).ToArray();
        foreach (var item in addEntities)
        {
            _dbContext.TrySetTenantId(item);
        }
        foreach (var item in updateEntities)
        {
            _dbContext.TrySetTenantId(item);
        }

        if (addEntities.Length == 0 && updateEntities.Length == 0)
        {
            return false;
        }

        var hasChanges = false;

        if (addEntities.Length > 0)
        {
            var insertedRows = await DbClient.Insertable(addEntities)
                .ExecuteCommandAsync(cancellationToken);
            hasChanges |= insertedRows > 0;
        }

        if (updateEntities.Length > 0)
        {
            var updatedRows = await DbClient.Updateable(updateEntities)
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

        var affectedRows = await DbClient.Deleteable(entity)
            .ExecuteCommandAsync(cancellationToken);
        return affectedRows > 0;
    }

    /// <summary>
    /// 根据主键删除实体
    /// </summary>
    /// <param name="id">主键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    public async Task<bool> DeleteByIdAsync(TKey id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var affectedRows = await DbClient.Deleteable<TEntity>()
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

        // 内部实现使用数组以提高性能
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

        var affectedRows = await DbClient.Deleteable<TEntity>()
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

        // 内部实现使用数组以提高性能
        var idArray = ids.ToArray();
        if (idArray.Length == 0)
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var affectedRows = await DbClient.Deleteable<TEntity>()
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

        var affectedRows = await DbClient.Deleteable<TEntity>()
            .Where(predicate)
            .ExecuteCommandAsync(cancellationToken);
        return affectedRows > 0;
    }

    /// <summary>
    /// 使用事务执行操作
    /// </summary>
    /// <param name="action">需要在事务中执行的操作</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<bool> UseTranAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();

        var result = await DbClient.Ado.UseTranAsync(async () =>
        {
            await action();
        });
        return result.IsSuccess;
    }

    /// <summary>
    /// 使用事务执行操作并返回结果
    /// </summary>
    /// <typeparam name="TResult">返回结果类型</typeparam>
    /// <param name="func">需要在事务中执行的操作</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>事务操作的结果</returns>
    public async Task<TResult> UseTranAsync<TResult>(Func<Task<TResult>> func, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(func);
        cancellationToken.ThrowIfCancellationRequested();

        var result = await DbClient.Ado.UseTranAsync(async () =>
        {
            return await func();
        });

        return result.IsSuccess ? result.Data : default!;
    }
}
