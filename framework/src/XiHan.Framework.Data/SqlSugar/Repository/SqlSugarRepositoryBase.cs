#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SqlSugarRepositoryBase
// Guid:165b306d-7a53-4480-9f8c-11b8c271f2c6
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/25 05:52:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using SqlSugar;
using System.Linq.Expressions;
using System.Text.Json;
using XiHan.Framework.Data.Auditing;
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
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

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
        var beforeEntity = await TryGetCurrentEntityAsync(entity.BasicId, cancellationToken);

        await DbClient.Updateable(entity)
            .ExecuteCommandAsync(cancellationToken);
        await TryWriteAuditLogAsync(beforeEntity, entity, "Update", cancellationToken);
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

        var beforeEntity = await TryGetCurrentEntityAsync(entity.BasicId, cancellationToken);
        var affectedRows = await DbClient.Updateable(entity)
            .ExecuteCommandAsync(cancellationToken);
        if (affectedRows > 0)
        {
            await TryWriteAuditLogAsync(beforeEntity, entity, "Update", cancellationToken);
        }
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
        var beforeEntity = await TryGetCurrentEntityAsync(entity.BasicId, cancellationToken) ?? entity;

        var affectedRows = await DbClient.Deleteable(entity)
            .ExecuteCommandAsync(cancellationToken);
        if (affectedRows > 0)
        {
            await TryWriteAuditLogAsync(beforeEntity, null, "Delete", cancellationToken);
        }
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
        var beforeEntity = await TryGetCurrentEntityAsync(id, cancellationToken);

        var affectedRows = await DbClient.Deleteable<TEntity>()
            .In(id)
            .ExecuteCommandAsync(cancellationToken);
        if (affectedRows > 0 && beforeEntity is not null)
        {
            await TryWriteAuditLogAsync(beforeEntity, null, "Delete", cancellationToken);
        }
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

    private static string? SerializeForAudit(TEntity? entity)
    {
        if (entity is null)
        {
            return null;
        }

        try
        {
            var json = JsonSerializer.Serialize(entity, JsonOptions);
            return json.Length > 8000 ? json[..8000] : json;
        }
        catch
        {
            return null;
        }
    }

    private static string? BuildChangedFields(TEntity? beforeEntity, TEntity? afterEntity)
    {
        if (beforeEntity is null || afterEntity is null)
        {
            return null;
        }

        var changed = new List<object>();
        var properties = typeof(TEntity).GetProperties()
            .Where(p => p.CanRead && p.Name is not "BasicId");

        foreach (var property in properties)
        {
            var beforeValue = property.GetValue(beforeEntity);
            var afterValue = property.GetValue(afterEntity);
            if (Equals(beforeValue, afterValue))
            {
                continue;
            }

            changed.Add(new
            {
                Field = property.Name,
                Before = beforeValue,
                After = afterValue
            });
        }

        return changed.Count == 0 ? null : JsonSerializer.Serialize(changed, JsonOptions);
    }

    private async Task<TEntity?> TryGetCurrentEntityAsync(TKey id, CancellationToken cancellationToken)
    {
        return await CreateTenantQueryable()
            .Where(entity => entity.BasicId.Equals(id))
            .FirstAsync(cancellationToken);
    }

    private async Task TryWriteAuditLogAsync(TEntity? beforeEntity, TEntity? afterEntity, string operationType, CancellationToken cancellationToken)
    {
        var contextProvider = _dbContext.GetService<IEntityAuditContextProvider>();
        var writer = _dbContext.GetService<IEntityAuditLogWriter>();
        if (contextProvider is null || writer is null || !contextProvider.ShouldAudit(typeof(TEntity)))
        {
            return;
        }

        var changedFields = BuildChangedFields(beforeEntity, afterEntity);
        if (operationType == "Update" && string.IsNullOrWhiteSpace(changedFields))
        {
            return;
        }

        var record = contextProvider.CreateBaseRecord();
        record.OperationType = operationType;
        record.EntityType = typeof(TEntity).Name;
        record.EntityId = afterEntity?.BasicId?.ToString() ?? beforeEntity?.BasicId?.ToString();
        record.BeforeData = SerializeForAudit(beforeEntity);
        record.AfterData = SerializeForAudit(afterEntity);
        record.ChangedFields = changedFields;

        await writer.WriteAsync(record, cancellationToken);
    }
}
