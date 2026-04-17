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
using System.Linq.Expressions;
using System.Text.Json;
using XiHan.Framework.Data.Auditing;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Repositories;

namespace XiHan.Framework.Data.SqlSugar.Repository;

/// <summary>
/// SqlSugar 仓储基类（读 + 写）
/// </summary>
/// <remarks>
/// 设计原则：
/// <list type="bullet">
///   <item>方法体专注纯业务语义：不包含 <c>.SplitTable()</c>、<c>.EnableQueryFilter()</c> 等横切细节。</item>
///   <item>租户连接/租户过滤/软删过滤 统一由 <see cref="ISqlSugarClientResolver"/> + 全局 QueryFilter AOP 承担。</item>
///   <item>审计字段（CreatedTime/ModifiedTime/TenantId 等）通过 SqlSugar <c>DataExecuting</c> AOP 自动注入，无需仓储侧处理。</item>
///   <item>审计日志通过可选的 <see cref="IEntityAuditLogWriter"/> 写入，未注册时不产生开销。</item>
/// </list>
/// </remarks>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TKey">主键类型</typeparam>
public class SqlSugarRepositoryBase<TEntity, TKey> : SqlSugarReadOnlyRepository<TEntity, TKey>, IRepositoryBase<TEntity, TKey>
    where TEntity : class, IEntityBase<TKey>, new()
    where TKey : IEquatable<TKey>
{
    private const string OperationCreate = "Create";
    private const string OperationUpdate = "Update";
    private const string OperationDelete = "Delete";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="clientResolver">SqlSugar 客户端解析器</param>
    /// <param name="serviceProvider">服务提供者（审计/TraceId 可选服务）</param>
    public SqlSugarRepositoryBase(
        ISqlSugarClientResolver clientResolver,
        IServiceProvider serviceProvider)
        : base(clientResolver)
    {
        _serviceProvider = serviceProvider;
    }

    #region 新增

    /// <summary>
    /// 添加实体
    /// </summary>
    public async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        cancellationToken.ThrowIfCancellationRequested();

        TryFillTraceId(entity);

        var inserted = await DbClient.Insertable(entity).ExecuteReturnEntityAsync();
        await TryWriteAuditLogAsync(null, inserted, OperationCreate, cancellationToken);
        return inserted;
    }

    /// <summary>
    /// 添加实体并返回主键
    /// </summary>
    public async Task<TKey> AddReturnIdAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        var inserted = await AddAsync(entity, cancellationToken);
        return inserted.BasicId;
    }

    /// <summary>
    /// 批量添加实体
    /// </summary>
    public async Task<IReadOnlyList<TEntity>> AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);

        var entityArray = entities.ToArray();
        if (entityArray.Length == 0)
        {
            return [];
        }

        cancellationToken.ThrowIfCancellationRequested();
        foreach (var entity in entityArray)
        {
            TryFillTraceId(entity);
        }

        await DbClient.Insertable(entityArray).ExecuteCommandAsync(cancellationToken);

        foreach (var entity in entityArray)
        {
            await TryWriteAuditLogAsync(null, entity, OperationCreate, cancellationToken);
        }
        return entityArray;
    }

    #endregion

    #region 更新

    /// <summary>
    /// 按主键更新实体
    /// </summary>
    public async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        cancellationToken.ThrowIfCancellationRequested();

        var beforeEntity = await GetByIdAsync(entity.BasicId, cancellationToken)
            ?? throw new InvalidOperationException("更新失败：实体不存在或不在当前租户范围内。");

        await DbClient.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        await TryWriteAuditLogAsync(beforeEntity, entity, OperationUpdate, cancellationToken);
        return entity;
    }

    /// <summary>
    /// 按条件更新（SetColumns + Where）
    /// </summary>
    public async Task<bool> UpdateAsync(Expression<Func<TEntity, TEntity>> columns, Expression<Func<TEntity, bool>> whereExpression, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(whereExpression);
        cancellationToken.ThrowIfCancellationRequested();

        // 条件更新需显式 EnableQueryFilter 让全局过滤器生效
        var affectedRows = await DbClient.Updateable<TEntity>()
            .SetColumns(columns)
            .Where(whereExpression)
            .EnableQueryFilter()
            .ExecuteCommandAsync(cancellationToken);
        return affectedRows > 0;
    }

    /// <summary>
    /// 批量更新实体
    /// </summary>
    public async Task<IReadOnlyList<TEntity>> UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);

        var entityArray = entities.ToArray();
        if (entityArray.Length == 0)
        {
            return [];
        }

        cancellationToken.ThrowIfCancellationRequested();

        var idArray = entityArray.Select(e => e.BasicId).Distinct().ToArray();
        var beforeEntities = await CreateQueryable()
            .Where(e => idArray.Contains(e.BasicId))
            .ToListAsync(cancellationToken);
        var beforeMap = beforeEntities.ToDictionary(e => e.BasicId);

        foreach (var entity in entityArray)
        {
            if (!beforeMap.ContainsKey(entity.BasicId))
            {
                throw new InvalidOperationException("批量更新失败：存在不在当前租户范围内的实体。");
            }
        }

        await DbClient.Updateable(entityArray).ExecuteCommandAsync(cancellationToken);

        foreach (var entity in entityArray)
        {
            if (beforeMap.TryGetValue(entity.BasicId, out var beforeEntity))
            {
                await TryWriteAuditLogAsync(beforeEntity, entity, OperationUpdate, cancellationToken);
            }
        }
        return entityArray;
    }

    #endregion

    #region 新增或更新

    /// <summary>
    /// 新增或更新单条实体（根据 IsTransient 判断）
    /// </summary>
    public async Task<bool> AddOrUpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        cancellationToken.ThrowIfCancellationRequested();

        if (entity.IsTransient())
        {
            TryFillTraceId(entity);
            var insertedRows = await DbClient.Insertable(entity).ExecuteCommandAsync(cancellationToken);
            if (insertedRows > 0)
            {
                await TryWriteAuditLogAsync(null, entity, OperationCreate, cancellationToken);
                return true;
            }
            return false;
        }

        var beforeEntity = await GetByIdAsync(entity.BasicId, cancellationToken);
        if (beforeEntity is null)
        {
            return false;
        }

        var affectedRows = await DbClient.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        if (affectedRows > 0)
        {
            await TryWriteAuditLogAsync(beforeEntity, entity, OperationUpdate, cancellationToken);
        }
        return affectedRows > 0;
    }

    /// <summary>
    /// 批量新增或更新（按 IsTransient 分组处理）
    /// </summary>
    public async Task<bool> AddOrUpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);

        var entityArray = entities.ToArray();
        if (entityArray.Length == 0)
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var addEntities = entityArray.Where(e => e.IsTransient()).ToArray();
        var updateEntities = entityArray.Where(e => !e.IsTransient()).ToArray();

        var hasChanges = false;

        if (addEntities.Length > 0)
        {
            foreach (var entity in addEntities)
            {
                TryFillTraceId(entity);
            }

            var insertedRows = await DbClient.Insertable(addEntities).ExecuteCommandAsync(cancellationToken);
            if (insertedRows > 0)
            {
                foreach (var entity in addEntities)
                {
                    await TryWriteAuditLogAsync(null, entity, OperationCreate, cancellationToken);
                }
                hasChanges = true;
            }
        }

        if (updateEntities.Length > 0)
        {
            var updateIds = updateEntities.Select(e => e.BasicId).Distinct().ToArray();
            var beforeEntities = await CreateQueryable()
                .Where(e => updateIds.Contains(e.BasicId))
                .ToListAsync(cancellationToken);
            var beforeMap = beforeEntities.ToDictionary(e => e.BasicId);

            foreach (var entity in updateEntities)
            {
                if (!beforeMap.ContainsKey(entity.BasicId))
                {
                    throw new InvalidOperationException("批量新增或更新失败：存在不在当前租户范围内的更新实体。");
                }
            }

            var updatedRows = await DbClient.Updateable(updateEntities).ExecuteCommandAsync(cancellationToken);
            if (updatedRows > 0)
            {
                foreach (var entity in updateEntities)
                {
                    if (beforeMap.TryGetValue(entity.BasicId, out var beforeEntity))
                    {
                        await TryWriteAuditLogAsync(beforeEntity, entity, OperationUpdate, cancellationToken);
                    }
                }
                hasChanges = true;
            }
        }

        return hasChanges;
    }

    #endregion

    #region 删除

    /// <summary>
    /// 按实体删除
    /// </summary>
    public async Task<bool> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        cancellationToken.ThrowIfCancellationRequested();

        var beforeEntity = await GetByIdAsync(entity.BasicId, cancellationToken);
        if (beforeEntity is null)
        {
            return false;
        }

        var affectedRows = await DbClient.Deleteable<TEntity>()
            .In(entity.BasicId!)
            .EnableQueryFilter()
            .ExecuteCommandAsync(cancellationToken);

        if (affectedRows > 0)
        {
            await TryWriteAuditLogAsync(beforeEntity, null, OperationDelete, cancellationToken);
        }
        return affectedRows > 0;
    }

    /// <summary>
    /// 按主键删除
    /// </summary>
    public async Task<bool> DeleteByIdAsync(TKey id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var beforeEntity = await GetByIdAsync(id, cancellationToken);
        if (beforeEntity is null)
        {
            return false;
        }

        var affectedRows = await DbClient.Deleteable<TEntity>()
            .In(id!)
            .EnableQueryFilter()
            .ExecuteCommandAsync(cancellationToken);

        if (affectedRows > 0)
        {
            await TryWriteAuditLogAsync(beforeEntity, null, OperationDelete, cancellationToken);
        }
        return affectedRows > 0;
    }

    /// <summary>
    /// 批量按实体删除
    /// </summary>
    public async Task<bool> DeleteRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);

        var idArray = entities
            .Where(e => e.BasicId is not null)
            .Select(e => e.BasicId!)
            .ToArray();

        if (idArray.Length == 0)
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var affectedRows = await DbClient.Deleteable<TEntity>()
            .In(idArray)
            .EnableQueryFilter()
            .ExecuteCommandAsync(cancellationToken);
        return affectedRows > 0;
    }

    /// <summary>
    /// 批量按主键删除
    /// </summary>
    public async Task<bool> DeleteRangeAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var idArray = ids.Cast<object>().ToArray();
        if (idArray.Length == 0)
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var affectedRows = await DbClient.Deleteable<TEntity>()
            .In(idArray)
            .EnableQueryFilter()
            .ExecuteCommandAsync(cancellationToken);
        return affectedRows > 0;
    }

    /// <summary>
    /// 按条件删除
    /// </summary>
    public async Task<bool> DeleteAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        cancellationToken.ThrowIfCancellationRequested();

        var affectedRows = await DbClient.Deleteable<TEntity>()
            .Where(predicate)
            .EnableQueryFilter()
            .ExecuteCommandAsync(cancellationToken);
        return affectedRows > 0;
    }

    #endregion

    #region 事务

    /// <summary>
    /// 在事务中执行操作（无返回值）
    /// </summary>
    public async Task<bool> UseTranAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();

        var result = await DbClient.Ado.UseTranAsync(async () => await action());
        return result.IsSuccess;
    }

    /// <summary>
    /// 在事务中执行操作（带返回值）
    /// </summary>
    public async Task<TResult> UseTranAsync<TResult>(Func<Task<TResult>> func, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(func);
        cancellationToken.ThrowIfCancellationRequested();

        var result = await DbClient.Ado.UseTranAsync(async () => await func());
        return result.IsSuccess ? result.Data : default!;
    }

    #endregion

    #region 辅助

    /// <summary>
    /// 如实体实现 <c>ITraceableEntity</c> 且 TraceId 为空，则自动从 <c>ITraceIdProvider</c> 填充
    /// </summary>
    private void TryFillTraceId(TEntity entity)
    {
        if (entity is not ITraceableEntity traceable || !string.IsNullOrWhiteSpace(traceable.TraceId))
        {
            return;
        }

        var traceIdProvider = _serviceProvider.GetService<ITraceIdProvider>();
        var traceId = traceIdProvider?.GetCurrentTraceId();

        if (!string.IsNullOrWhiteSpace(traceId))
        {
            traceable.TraceId = traceId.Length > 64 ? traceId[..64] : traceId;
        }
    }

    /// <summary>
    /// 尝试写审计日志（未注册审计服务时静默跳过）
    /// </summary>
    private async Task TryWriteAuditLogAsync(TEntity? beforeEntity, TEntity? afterEntity, string operationType, CancellationToken cancellationToken)
    {
        var contextProvider = _serviceProvider.GetService<IEntityAuditContextProvider>();
        var writer = _serviceProvider.GetService<IEntityAuditLogWriter>();
        if (contextProvider is null || writer is null || !contextProvider.ShouldAudit(typeof(TEntity)))
        {
            return;
        }

        var changedFields = BuildChangedFields(beforeEntity, afterEntity);
        if (operationType == OperationUpdate && string.IsNullOrWhiteSpace(changedFields))
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

    /// <summary>
    /// 序列化实体为审计 JSON（超长截断至 8000 字符）
    /// </summary>
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

    /// <summary>
    /// 构建字段变更明细（只保留有差异的字段）
    /// </summary>
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

    #endregion
}
