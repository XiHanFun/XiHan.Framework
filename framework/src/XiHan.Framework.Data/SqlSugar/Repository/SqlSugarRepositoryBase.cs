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

using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using System.Linq.Expressions;
using System.Text.Json;
using XiHan.Framework.Data.Auditing;
using XiHan.Framework.Data.SqlSugar.SplitTables;
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

    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="dbContext">SqlSugar 数据上下文</param>
    /// <param name="splitTableExecutor">分表执行器</param>
    /// <param name="serviceProvider">服务提供者</param>
    public SqlSugarRepositoryBase(
        ISqlSugarDbContext dbContext,
        ISqlSugarSplitTableExecutor splitTableExecutor,
        IServiceProvider serviceProvider)
        : base(dbContext, splitTableExecutor)
    {
        _serviceProvider = serviceProvider;
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

        if (IsSplitTableEntity)
        {
            await SplitTableExecutor.InsertAsync(DbClient, [entity], cancellationToken);
            await TryWriteAuditLogAsync(null, entity, "Create", cancellationToken);
            return entity;
        }

        var insertedEntity = await DbClient.Insertable(entity).ExecuteReturnEntityAsync();
        await TryWriteAuditLogAsync(null, insertedEntity, "Create", cancellationToken);
        return insertedEntity;
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

        if (IsSplitTableEntity)
        {
            await SplitTableExecutor.InsertAsync(DbClient, [entity], cancellationToken);
            await TryWriteAuditLogAsync(null, entity, "Create", cancellationToken);
            return entity.BasicId;
        }

        var insertedEntity = await DbClient.Insertable(entity).ExecuteReturnEntityAsync();
        await TryWriteAuditLogAsync(null, insertedEntity, "Create", cancellationToken);
        return insertedEntity.BasicId;
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
        if (IsSplitTableEntity)
        {
            await SplitTableExecutor.InsertAsync(DbClient, entityList, cancellationToken);
            foreach (var entity in entityList)
            {
                await TryWriteAuditLogAsync(null, entity, "Create", cancellationToken);
            }
            return entityList;
        }

        await DbClient.Insertable(entityList).ExecuteCommandAsync(cancellationToken);
        foreach (var entity in entityList)
        {
            await TryWriteAuditLogAsync(null, entity, "Create", cancellationToken);
        }
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
        var beforeEntity = await TryGetCurrentEntityAsync(entity.BasicId, cancellationToken)
            ?? throw new InvalidOperationException("更新失败：实体不存在或不在当前租户范围内。");

        if (IsSplitTableEntity)
        {
            await SplitTableExecutor.UpdateAsync(DbClient, [entity], cancellationToken);
        }
        else
        {
            await DbClient.Updateable(entity)
                .ExecuteCommandAsync(cancellationToken);
        }

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

        if (IsSplitTableEntity)
        {
            var affectedRows = await DbClient.Updateable<TEntity>()
                .SetColumns(columns)
                .Where(whereExpression)
                .EnableQueryFilter()
                .SplitTable(tables => tables)
                .ExecuteCommandAsync();
            return affectedRows > 0;
        }

        var updatedRows = await DbClient.Updateable<TEntity>()
            .SetColumns(columns)
            .Where(whereExpression)
            .EnableQueryFilter()
            .ExecuteCommandAsync(cancellationToken);
        return updatedRows > 0;
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
        var entityIds = entityList
            .Select(entity => entity.BasicId)
            .Distinct()
            .ToArray();
        var currentEntities = await CreateTenantQueryable()
            .Where(entity => entityIds.Contains(entity.BasicId))
            .ToListAsync(cancellationToken);
        var currentEntityMap = currentEntities.ToDictionary(entity => entity.BasicId);
        foreach (var entity in entityList)
        {
            if (!currentEntityMap.ContainsKey(entity.BasicId))
            {
                throw new InvalidOperationException("批量更新失败：存在不在当前租户范围内的实体。");
            }
        }

        if (IsSplitTableEntity)
        {
            await SplitTableExecutor.UpdateAsync(DbClient, entityList, cancellationToken);
            return entityList;
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

        if (entity.IsTransient())
        {
            int insertedRows;
            if (IsSplitTableEntity)
            {
                insertedRows = await SplitTableExecutor.InsertAsync(DbClient, [entity], cancellationToken);
            }
            else
            {
                insertedRows = await DbClient.Insertable(entity).ExecuteCommandAsync(cancellationToken);
            }
            if (insertedRows > 0)
            {
                await TryWriteAuditLogAsync(null, entity, "Create", cancellationToken);
                return true;
            }

            return false;
        }

        var beforeEntity = await TryGetCurrentEntityAsync(entity.BasicId, cancellationToken);
        if (beforeEntity is null)
        {
            return false;
        }

        int affectedRows;
        if (IsSplitTableEntity)
        {
            affectedRows = await SplitTableExecutor.UpdateAsync(DbClient, [entity], cancellationToken);
        }
        else
        {
            affectedRows = await DbClient.Updateable(entity)
                .ExecuteCommandAsync(cancellationToken);
        }

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

        if (addEntities.Length == 0 && updateEntities.Length == 0)
        {
            return false;
        }

        var hasChanges = false;

        if (addEntities.Length > 0)
        {
            int insertedRows;
            if (IsSplitTableEntity)
            {
                insertedRows = await SplitTableExecutor.InsertAsync(DbClient, addEntities, cancellationToken);
            }
            else
            {
                insertedRows = await DbClient.Insertable(addEntities).ExecuteCommandAsync(cancellationToken);
            }
            hasChanges |= insertedRows > 0;
            if (insertedRows > 0)
            {
                foreach (var entity in addEntities)
                {
                    await TryWriteAuditLogAsync(null, entity, "Create", cancellationToken);
                }
            }
        }

        if (updateEntities.Length > 0)
        {
            var updateEntityList = updateEntities.ToList();
            var updateEntityIds = updateEntityList
                .Select(entity => entity.BasicId)
                .Distinct()
                .ToArray();
            var currentEntities = await CreateTenantQueryable()
                .Where(entity => updateEntityIds.Contains(entity.BasicId))
                .ToListAsync(cancellationToken);
            var currentEntityMap = currentEntities.ToDictionary(entity => entity.BasicId);

            foreach (var updateEntity in updateEntityList)
            {
                if (!currentEntityMap.TryGetValue(updateEntity.BasicId, out _))
                {
                    throw new InvalidOperationException("批量新增或更新失败：存在不在当前租户范围内的更新实体。");
                }
            }

            int updatedRows;
            if (IsSplitTableEntity)
            {
                updatedRows = await SplitTableExecutor.UpdateAsync(DbClient, updateEntityList, cancellationToken);
            }
            else
            {
                updatedRows = await DbClient.Updateable(updateEntityList)
                    .ExecuteCommandAsync(cancellationToken);
            }

            if (updatedRows > 0)
            {
                foreach (var updateEntity in updateEntityList)
                {
                    if (currentEntityMap.TryGetValue(updateEntity.BasicId, out var beforeEntity))
                    {
                        await TryWriteAuditLogAsync(beforeEntity, updateEntity, "Update", cancellationToken);
                    }
                }
            }

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
        var beforeEntity = await TryGetCurrentEntityAsync(entity.BasicId, cancellationToken);
        if (beforeEntity is null)
        {
            return false;
        }

        int affectedRows;
        if (IsSplitTableEntity)
        {
            cancellationToken.ThrowIfCancellationRequested();
            affectedRows = await DbClient.Deleteable<TEntity>()
                .Where(item => item.BasicId.Equals(entity.BasicId))
                .EnableQueryFilter()
                .SplitTable(tables => tables)
                .ExecuteCommandAsync();
        }
        else
        {
            affectedRows = await DbClient.Deleteable<TEntity>()
                .Where(item => item.BasicId.Equals(entity.BasicId))
                .EnableQueryFilter()
                .ExecuteCommandAsync(cancellationToken);
        }

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
        if (beforeEntity is null)
        {
            return false;
        }

        int affectedRows;
        if (IsSplitTableEntity)
        {
            cancellationToken.ThrowIfCancellationRequested();
            affectedRows = await DbClient.Deleteable<TEntity>()
                .In(id)
                .EnableQueryFilter()
                .SplitTable(tables => tables)
                .ExecuteCommandAsync();
        }
        else
        {
            affectedRows = await DbClient.Deleteable<TEntity>()
                .In(id)
                .EnableQueryFilter()
                .ExecuteCommandAsync(cancellationToken);
        }

        if (affectedRows > 0)
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
        if (IsSplitTableEntity)
        {
            var splitTableAffectedRows = await DbClient.Deleteable<TEntity>()
                .In(idArray)
                .EnableQueryFilter()
                .SplitTable(tables => tables)
                .ExecuteCommandAsync();
            return splitTableAffectedRows > 0;
        }

        var affectedRows = await DbClient.Deleteable<TEntity>()
            .In(idArray)
            .EnableQueryFilter()
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
        if (IsSplitTableEntity)
        {
            var splitTableAffectedRows = await DbClient.Deleteable<TEntity>()
                .In(idArray)
                .EnableQueryFilter()
                .SplitTable(tables => tables)
                .ExecuteCommandAsync();
            return splitTableAffectedRows > 0;
        }

        var affectedRows = await DbClient.Deleteable<TEntity>()
            .In(idArray)
            .EnableQueryFilter()
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

        if (IsSplitTableEntity)
        {
            var splitTableAffectedRows = await DbClient.Deleteable<TEntity>()
                .Where(predicate)
                .EnableQueryFilter()
                .SplitTable(tables => tables)
                .ExecuteCommandAsync();
            return splitTableAffectedRows > 0;
        }

        var affectedRows = await DbClient.Deleteable<TEntity>()
            .Where(predicate)
            .EnableQueryFilter()
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
        var contextProvider = _serviceProvider.GetService<IEntityAuditContextProvider>();
        var writer = _serviceProvider.GetService<IEntityAuditLogWriter>();
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
