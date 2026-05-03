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

using System.Linq.Expressions;
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
///   <item>仓储只负责纯持久化 + 租户安全边界（Update/Delete 前的 before 预读用于确保实体在当前租户范围内）。</item>
///   <item>租户连接/租户过滤/软删过滤统一由 <see cref="ISqlSugarClientResolver"/> + 全局 QueryFilter AOP 承担。</item>
///   <item>审计字段（CreatedTime/ModifiedTime/TenantId 等）通过 SqlSugar <c>DataExecuting</c> AOP 自动注入。</item>
///   <item>实体审计日志通过 SqlSugar 原生 <c>OnDiffLogEvent</c> AOP 处理：仓储只需在写操作挂 <c>.EnableDiffLogEvent(typeof(TEntity))</c>。</item>
///   <item>事务不在仓储内开启，统一由工作单元接管 SqlSugar 连接事务。</item>
/// </list>
/// </remarks>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TKey">主键类型</typeparam>
public class SqlSugarRepositoryBase<TEntity, TKey> : SqlSugarReadOnlyRepository<TEntity, TKey>, IRepositoryBase<TEntity, TKey>
    where TEntity : class, IEntityBase<TKey>, new()
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 审计开关业务对象：传入实体 Type 供 AOP 辨识该条 Diff 属于哪个实体类型
    /// </summary>
    private static readonly Type AuditBusinessData = typeof(TEntity);

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="clientResolver">SqlSugar 客户端解析器</param>
    public SqlSugarRepositoryBase(ISqlSugarClientResolver clientResolver)
        : base(clientResolver)
    {
    }

    #region 新增

    /// <summary>
    /// 添加实体
    /// </summary>
    public virtual async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        cancellationToken.ThrowIfCancellationRequested();

        return await DbClient.Insertable(entity)
            .EnableDiffLogEvent(AuditBusinessData)
            .ExecuteReturnEntityAsync();
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
        await DbClient.Insertable(entityArray)
            .EnableDiffLogEvent(AuditBusinessData)
            .ExecuteCommandAsync(cancellationToken);

        return entityArray;
    }

    #endregion

    #region 更新

    /// <summary>
    /// 按主键更新实体
    /// </summary>
    public virtual async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        cancellationToken.ThrowIfCancellationRequested();

        _ = await GetByIdAsync(entity.BasicId, cancellationToken)
            ?? throw new InvalidOperationException("更新失败：实体不存在或不在当前租户范围内。");

        await DbClient.Updateable(entity)
            .EnableDiffLogEvent(AuditBusinessData)
            .ExecuteCommandAsync(cancellationToken);

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

        var affectedRows = await DbClient.Updateable<TEntity>()
            .SetColumns(columns)
            .Where(whereExpression)
            .EnableQueryFilter()
            .EnableDiffLogEvent(AuditBusinessData)
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
        await EnsureVisibleAsync(entityArray.Select(entity => entity.BasicId), "批量更新失败：存在不在当前租户范围内的实体。", cancellationToken);

        await DbClient.Updateable(entityArray)
            .EnableDiffLogEvent(AuditBusinessData)
            .ExecuteCommandAsync(cancellationToken);

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
            var insertedRows = await DbClient.Insertable(entity)
                .EnableDiffLogEvent(AuditBusinessData)
                .ExecuteCommandAsync(cancellationToken);

            return insertedRows > 0;
        }

        _ = await GetByIdAsync(entity.BasicId, cancellationToken)
            ?? throw new InvalidOperationException("新增或更新失败：实体不存在或不在当前租户范围内。");

        var affectedRows = await DbClient.Updateable(entity)
            .EnableDiffLogEvent(AuditBusinessData)
            .ExecuteCommandAsync(cancellationToken);

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

        var addEntities = entityArray.Where(entity => entity.IsTransient()).ToArray();
        var updateEntities = entityArray.Where(entity => !entity.IsTransient()).ToArray();
        var hasChanges = false;

        if (addEntities.Length > 0)
        {
            var insertedRows = await DbClient.Insertable(addEntities)
                .EnableDiffLogEvent(AuditBusinessData)
                .ExecuteCommandAsync(cancellationToken);

            hasChanges = insertedRows > 0;
        }

        if (updateEntities.Length > 0)
        {
            await EnsureVisibleAsync(updateEntities.Select(entity => entity.BasicId), "批量新增或更新失败：存在不在当前租户范围内的更新实体。", cancellationToken);

            var updatedRows = await DbClient.Updateable(updateEntities)
                .EnableDiffLogEvent(AuditBusinessData)
                .ExecuteCommandAsync(cancellationToken);

            hasChanges = hasChanges || updatedRows > 0;
        }

        return hasChanges;
    }

    #endregion

    #region 删除

    /// <summary>
    /// 按实体删除
    /// </summary>
    public virtual async Task<bool> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        cancellationToken.ThrowIfCancellationRequested();

        _ = await GetByIdAsync(entity.BasicId, cancellationToken)
            ?? throw new InvalidOperationException("删除失败：实体不存在或不在当前租户范围内。");

        var affectedRows = await DbClient.Deleteable<TEntity>()
            .In(entity.BasicId!)
            .EnableQueryFilter()
            .EnableDiffLogEvent(AuditBusinessData)
            .ExecuteCommandAsync(cancellationToken);

        return affectedRows > 0;
    }

    /// <summary>
    /// 按主键删除
    /// </summary>
    public async Task<bool> DeleteByIdAsync(TKey id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (await GetByIdAsync(id, cancellationToken) is null)
        {
            return false;
        }

        var affectedRows = await DbClient.Deleteable<TEntity>()
            .In(id!)
            .EnableQueryFilter()
            .EnableDiffLogEvent(AuditBusinessData)
            .ExecuteCommandAsync(cancellationToken);

        return affectedRows > 0;
    }

    /// <summary>
    /// 批量按实体删除
    /// </summary>
    public async Task<bool> DeleteRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);
        return await DeleteRangeAsync(entities.Select(entity => entity.BasicId), cancellationToken);
    }

    /// <summary>
    /// 批量按主键删除
    /// </summary>
    public async Task<bool> DeleteRangeAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var idArray = ids.ToArray();
        if (idArray.Length == 0)
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await EnsureVisibleAsync(idArray, "批量删除失败：存在不在当前租户范围内的实体。", cancellationToken);

        var affectedRows = await DbClient.Deleteable<TEntity>()
            .In(idArray.Cast<object>().ToArray())
            .EnableQueryFilter()
            .EnableDiffLogEvent(AuditBusinessData)
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
            .EnableDiffLogEvent(AuditBusinessData)
            .ExecuteCommandAsync(cancellationToken);

        return affectedRows > 0;
    }

    #endregion

    /// <summary>
    /// 校验主键集合是否均在当前查询过滤范围内可见。
    /// </summary>
    /// <param name="ids">待校验主键集合</param>
    /// <param name="message">校验失败异常信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    private async Task EnsureVisibleAsync(IEnumerable<TKey> ids, string message, CancellationToken cancellationToken)
    {
        var idArray = ids.Distinct().ToArray();
        if (idArray.Length == 0)
        {
            return;
        }

        var existingIds = await CreateQueryable()
            .Where(entity => idArray.Contains(entity.BasicId))
            .Select(entity => entity.BasicId)
            .ToListAsync(cancellationToken);

        if (existingIds.Count != idArray.Length)
        {
            throw new InvalidOperationException(message);
        }
    }
}
