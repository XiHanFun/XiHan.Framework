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
using XiHan.Framework.Data.SqlSugar.Helpers;
using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Repositories;
using XiHan.Framework.MultiTenancy;

namespace XiHan.Framework.Data.SqlSugar.Repository;

/// <summary>
/// SqlSugar 仓储基类（读 + 写）
/// </summary>
/// <remarks>
/// 设计原则：
/// <list type="bullet">
///   <item>仓储只负责纯持久化 + 租户安全边界（Update/Delete 前的 before 预读用于确保实体在当前租户范围内）。</item>
///   <item>租户连接/租户过滤/软删过滤统一由 <see cref="ISqlSugarClientResolver"/> + 全局 QueryFilter AOP 承担。</item>
///   <item><b>写操作的租户/软删过滤由 <c>IsAutoUpdateQueryFilter</c>/<c>IsAutoDeleteQueryFilter</c>（默认开启）在
///         <c>DbClient.Updateable&lt;T&gt;()</c>/<c>Deleteable&lt;T&gt;()</c> 工厂内部自动挂 <c>EnableQueryFilter</c>，仓储<b>禁止再显式调用 <c>.EnableQueryFilter()</c></b></b>：
///         重复挂会把同一份过滤烘进 WHERE 两次、生成同名参数（<c>@constant*</c>），一旦叠加 Diff 的 <c>GetDiffTable</c> 重查旧值即触发
///         MySQL 驱动“Parameter already been defined”崩溃（PG 驱动容忍重名故不崩，但仍是冗余死条件）。</item>
///   <item>审计字段（CreatedTime/ModifiedTime/TenantId 等）通过 SqlSugar <c>DataExecuting</c> AOP 自动注入。</item>
///   <item>实体差异日志通过 SqlSugar 原生 <c>OnDiffLogEvent</c> AOP 处理：仓储只需在写操作挂 <c>.EnableDiffLogEvent(typeof(TEntity))</c>（不叠 <c>EnableQueryFilter</c> 即安全）。</item>
///   <item><b>写路径租户边界（读共享 ≠ 写共享）</b>：全局租户过滤器为「读共享」放行 <c>TenantId=0</c> 的平台全局行，
///         但写路径不得复用该口径——租户上下文内禁止改写/删除非本租户行（含全局行）：
///         预读守卫经 <c>EnsureWritableInCurrentTenant</c> 校验取回行的 TenantId，条件写自动追加当前租户 Where；
///         平台维护全局/跨租户数据的唯一合法入口是平台态（无租户上下文，<c>ICurrentTenant.Change(null)</c>）。</item>
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

        var existing = await GetByIdAsync(entity.BasicId, cancellationToken)
            ?? throw new InvalidOperationException("更新失败：实体不存在、已被软删除或不在当前租户范围内。");
        EnsureWritableInCurrentTenant(existing);

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

        // appendColumnsByDataFilter:true —— 表达式更新的审计补偿：
        // 无参 Updateable<T>() 走占位 new T()，SqlSugar 生成 SQL 前会把 DbColumnInfoList 过滤为「仅 SetValues 命中列 + 主键」，
        // DataExecuting AOP 写在占位实体上的 Modified* 审计值会被整体丢弃——条件更新的行永远不写 Modified_Time/Id/By，
        // 与实体更新路径审计口径不一致。该重载会对清零占位实体重跑 DataExecuting，把 AOP 写入的非默认审计值
        // 以参数追加进 SET；用户表达式已显式设置的列因 SetValues 先入先取不会被覆盖。
        var updateable = DbClient.Updateable<TEntity>()
            .SetColumns(columns, appendColumnsByDataFilter: true)
            .Where(whereExpression);

        // 租户态收紧写边界：自动过滤沿用读口径放行 TenantId=0 全局行，条件写须额外限定仅本租户行
        var tenantScope = BuildTenantWriteScopePredicate();
        if (tenantScope is not null)
        {
            updateable = updateable.Where(tenantScope);
        }

        var affectedRows = await updateable
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
        await EnsureVisibleAsync(entityArray.Select(entity => entity.BasicId), "批量更新失败：存在不可见的实体（不存在、已被软删除或不在当前租户范围内）。", cancellationToken);

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

        var existing = await GetByIdAsync(entity.BasicId, cancellationToken)
            ?? throw new InvalidOperationException("新增或更新失败：实体不存在、已被软删除或不在当前租户范围内。");
        EnsureWritableInCurrentTenant(existing);

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
            await EnsureVisibleAsync(updateEntities.Select(entity => entity.BasicId), "批量新增或更新失败：存在不可见的更新实体（不存在、已被软删除或不在当前租户范围内）。", cancellationToken);

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

        var existing = await GetByIdAsync(entity.BasicId, cancellationToken)
            ?? throw new InvalidOperationException("删除失败：实体不存在、已被软删除或不在当前租户范围内。");
        EnsureWritableInCurrentTenant(existing);

        var affectedRows = await DbClient.Deleteable<TEntity>()
            .In(entity.BasicId!)
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

        if (await GetByIdAsync(id, cancellationToken) is not { } existing)
        {
            return false;
        }

        EnsureWritableInCurrentTenant(existing);

        var affectedRows = await DbClient.Deleteable<TEntity>()
            .In(id!)
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
        await EnsureVisibleAsync(idArray, "批量删除失败：存在不可见的实体（不存在、已被软删除或不在当前租户范围内）。", cancellationToken);

        var affectedRows = await DbClient.Deleteable<TEntity>()
            .In(idArray.Cast<object>().ToArray())
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

        var deleteable = DbClient.Deleteable<TEntity>()
            .Where(predicate);

        // 租户态收紧写边界：自动过滤沿用读口径放行 TenantId=0 全局行，谓词删除须额外限定仅本租户行
        var tenantScope = BuildTenantWriteScopePredicate();
        if (tenantScope is not null)
        {
            deleteable = deleteable.Where(tenantScope);
        }

        var affectedRows = await deleteable
            .EnableDiffLogEvent(AuditBusinessData)
            .ExecuteCommandAsync(cancellationToken);

        return affectedRows > 0;
    }

    #endregion

    /// <summary>
    /// 校验主键集合是否均在当前查询过滤范围内可见；租户上下文下同时校验写边界（禁止改写全局行/异租户行）。
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

        // 需要写边界校验时取整行（要读 TenantId），否则维持只取主键的轻量路径
        if (RequiresTenantWriteGuard())
        {
            var rows = await CreateQueryable()
                .Where(entity => idArray.Contains(entity.BasicId))
                .ToListAsync(cancellationToken);
            if (rows.Count != idArray.Length)
            {
                throw new InvalidOperationException(message);
            }

            foreach (var row in rows)
            {
                EnsureWritableInCurrentTenant(row);
            }

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

    #region 写路径租户边界

    /// <summary>
    /// 是否需要写路径租户边界校验（实体为多租户实体且当前处于租户上下文）
    /// </summary>
    private static bool RequiresTenantWriteGuard()
    {
        return SqlSugarEntityTypeHelper.IsMultiTenantEntity<TEntity>() && GetActiveTenantId() is not null;
    }

    /// <summary>
    /// 获取当前激活的租户标识（平台态返回 null）
    /// </summary>
    /// <remarks>
    /// 与全局租户过滤器同源：DI 注册的 <c>ICurrentTenantAccessor</c> 单例正是
    /// <see cref="AsyncLocalCurrentTenantAccessor.Instance"/>（AsyncLocal 语义，随执行流），
    /// 仓储静态读取以避免为全部仓储构造函数增加注入涟漪。
    /// </remarks>
    private static long? GetActiveTenantId()
    {
        return AsyncLocalCurrentTenantAccessor.Instance.Current?.TenantId;
    }

    /// <summary>
    /// 校验实体在当前租户上下文内可写：租户态禁止改写非本租户行——含 <c>TenantId=0</c> 的平台全局行。
    /// </summary>
    /// <remarks>
    /// 全局租户过滤器为「读共享」放行 <c>TenantId=0</c>，写路径若复用该口径，租户即可改写/删除平台级共享数据（影响所有租户）。
    /// 平台态（无租户上下文）放行——那是维护全局/跨租户数据的唯一合法入口。
    /// 传入的必须是<b>从库中取回</b>的行（预读结果）：入参实体的 TenantId 可能是调用方伪造的。
    /// </remarks>
    /// <param name="entity">从库中取回的实体行</param>
    /// <exception cref="InvalidOperationException">租户上下文内试图改写全局行或异租户行</exception>
    protected static void EnsureWritableInCurrentTenant(TEntity entity)
    {
        if (entity is not IMultiTenantEntity multiTenantEntity)
        {
            return;
        }

        // 显式逃逸作用域（用户主体数据的自有行写入，如平台归属用户在租户态改自己的资料/设置/收件行）
        if (TenantWriteGuard.IsSuppressed)
        {
            return;
        }

        var tenantId = GetActiveTenantId();
        if (tenantId is null)
        {
            return;
        }

        if (multiTenantEntity.TenantId != tenantId.Value)
        {
            throw new InvalidOperationException(
                $"写入失败：不允许在租户上下文修改其他租户或平台级（TenantId={multiTenantEntity.TenantId}）的数据；平台数据维护请在平台态执行。");
        }
    }

    /// <summary>
    /// 为无预读的条件写（表达式更新/谓词删除）构建当前租户的严格 Where 谓词；无需守卫时返回 null。
    /// </summary>
    /// <remarks>
    /// 自动查询过滤（IsAutoUpdate/DeleteQueryFilter）沿用读口径放行 <c>TenantId=0</c>，
    /// 本谓词在租户态额外收紧为「仅本租户行」（表达式内只出现 long 标量，符合过滤表达式约束）。
    /// </remarks>
    private static Expression<Func<TEntity, bool>>? BuildTenantWriteScopePredicate()
    {
        if (!SqlSugarEntityTypeHelper.IsMultiTenantEntity<TEntity>())
        {
            return null;
        }

        // 显式逃逸作用域内不追加租户谓词（调用方自行保证条件只命中当前用户自有行）
        if (TenantWriteGuard.IsSuppressed)
        {
            return null;
        }

        var tenantId = GetActiveTenantId();
        if (tenantId is null)
        {
            return null;
        }

        var parameter = Expression.Parameter(typeof(TEntity), "entity");
        var body = Expression.Equal(
            Expression.Property(parameter, nameof(IMultiTenantEntity.TenantId)),
            Expression.Constant(tenantId.Value, typeof(long)));
        return Expression.Lambda<Func<TEntity, bool>>(body, parameter);
    }

    #endregion

    #region 含软删写路径（恢复/清除场景专用）

    /// <summary>
    /// 按主键更新实体，预读校验放行已软删数据（恢复场景专用）
    /// </summary>
    /// <remarks>
    /// 常规 <see cref="UpdateAsync(TEntity, CancellationToken)"/> 的预读带全局软删过滤，对已软删行必失败；
    /// 本路径预读用 <c>CreateWithDeletedQueryable</c>——保留租户过滤（租户安全边界不放松）、仅忽略软删过滤。
    /// 随后的对象式 UPDATE 按主键定向命中（对象式更新不吃全局写过滤，SqlSugar 对其调用 <c>EnableQueryFilter</c> 直接抛异常，
    /// 结构上无法把过滤烘进 WHERE）。0 行受影响按 fail-closed 抛异常（预读与写入之间行被并发物理删除）。
    /// </remarks>
    /// <param name="entity">实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新后的实体</returns>
    protected async Task<TEntity> UpdateIncludingDeletedAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        cancellationToken.ThrowIfCancellationRequested();

        var id = entity.BasicId;
        var existingRows = await CreateWithDeletedQueryable()
            .Where(item => item.BasicId.Equals(id))
            .Take(1)
            .ToListAsync(cancellationToken);
        var existing = existingRows.FirstOrDefault()
            ?? throw new InvalidOperationException("更新失败：实体不存在或不在当前租户范围内。");
        EnsureWritableInCurrentTenant(existing);

        var affectedRows = await DbClient.Updateable(entity)
            .EnableDiffLogEvent(AuditBusinessData)
            .ExecuteCommandAsync(cancellationToken);
        if (affectedRows == 0)
        {
            throw new InvalidOperationException("更新失败：目标行已被并发删除（0 行受影响）。");
        }

        return entity;
    }

    /// <summary>
    /// 批量按主键更新实体，预读校验放行已软删数据（批量恢复场景专用）
    /// </summary>
    /// <param name="entities">实体集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新后的实体集合</returns>
    protected async Task<IReadOnlyList<TEntity>> UpdateRangeIncludingDeletedAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);

        var entityArray = entities.ToArray();
        if (entityArray.Length == 0)
        {
            return [];
        }

        cancellationToken.ThrowIfCancellationRequested();

        var idArray = entityArray.Select(entity => entity.BasicId).Distinct().ToArray();
        var existingRows = await CreateWithDeletedQueryable()
            .Where(entity => idArray.Contains(entity.BasicId))
            .ToListAsync(cancellationToken);
        if (existingRows.Count != idArray.Length)
        {
            throw new InvalidOperationException("批量更新失败：存在不可见的实体（不存在或不在当前租户范围内）。");
        }

        foreach (var existing in existingRows)
        {
            EnsureWritableInCurrentTenant(existing);
        }

        var affectedRows = await DbClient.Updateable(entityArray)
            .EnableDiffLogEvent(AuditBusinessData)
            .ExecuteCommandAsync(cancellationToken);

        // 只在「全部未命中」时 fail-closed：严格按 Length 比对会误报——
        // MySQL 连接串 UseAffectedRows=true 时相同值 UPDATE 计 0 行、输入含重复主键时末次更新亦计 0，
        // 而预读与写入之间被并发物理删除（软删行仅 Purge 能物理删）本就是极窄窗口。
        if (affectedRows == 0)
        {
            throw new InvalidOperationException("批量更新失败：目标行已被并发删除（0 行受影响）。");
        }

        return entityArray;
    }

    #endregion
}
