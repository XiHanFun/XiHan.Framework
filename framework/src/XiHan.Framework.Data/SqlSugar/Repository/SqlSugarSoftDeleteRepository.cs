#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SqlSugarSoftDeleteRepository
// Guid:8d1a7f90-12f7-4c92-9f15-2b0c40cfe876
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/25 05:56:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Linq.Expressions;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.Data.SqlSugar.Repository.Extensions;
using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Repositories;
using XiHan.Framework.Domain.Specifications.Abstracts;

namespace XiHan.Framework.Data.SqlSugar.Repository;

/// <summary>
/// SqlSugar 软删除仓储实现
/// </summary>
/// <remarks>
/// 语义约定：
/// <list type="bullet">
///   <item>软删除/恢复均<b>幂等</b>：已软删的行再软删、未删除的行再恢复，一律静默跳过（不抛异常、不产生写操作）。</item>
///   <item>恢复走含软删写路径（<c>UpdateIncludingDeletedAsync</c>）：预读保留租户过滤、仅忽略软删过滤——
///         常规更新的预读带软删过滤，对已软删行必失败，恢复曾因此全线不可用。</item>
///   <item>恢复同时清空全部删除审计字段（DeletedTime/DeletedId/DeletedBy）——只清 DeletedTime 会残留
///         「被 X 删除于 null」的矛盾数据；AOP 的 ToDeleted 仅在 IsDeleted=true 时介入，恢复清理必须由仓储层完成。</item>
///   <item><see cref="PurgeAsync"/>/<see cref="PurgeRangeAsync"/> 是软删数据的唯一物理清除通路（合规/GDPR 场景）：
///         常规 Delete 家族的 DELETE 语句被自动查询过滤烘入 <c>Is_Deleted=0</c>，对已软删行永远 0 行；
///         清除路径临时摘除软删过滤（保留租户过滤）执行 DELETE，且仅允许作用于已软删数据（fail-closed）。</item>
/// </list>
/// </remarks>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TKey">主键类型</typeparam>
public class SqlSugarSoftDeleteRepository<TEntity, TKey> : SqlSugarRepositoryBase<TEntity, TKey>, ISoftDeleteRepositoryBase<TEntity, TKey>
    where TEntity : class, IEntityBase<TKey>, ISoftDelete, new()
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="clientResolver">SqlSugar 客户端解析器</param>
    public SqlSugarSoftDeleteRepository(ISqlSugarClientResolver clientResolver)
        : base(clientResolver)
    {
    }

    #region 软删除

    /// <summary>
    /// 软删除实体
    /// </summary>
    /// <param name="entity">实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    public async Task SoftDeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        // 幂等：已软删的行直接跳过（与按主键重载的静默语义一致）
        if (entity.IsDeleted)
        {
            return;
        }

        MarkDeleted(entity);
        await UpdateAsync(entity, cancellationToken);
    }

    /// <summary>
    /// 软删除实体（根据主键）
    /// </summary>
    /// <param name="id">主键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    public async Task SoftDeleteAsync(TKey id, CancellationToken cancellationToken = default)
    {
        var entity = await GetByIdAsync(id, cancellationToken);
        if (entity == null)
        {
            return;
        }

        await SoftDeleteAsync(entity, cancellationToken);
    }

    /// <summary>
    /// 批量软删除实体
    /// </summary>
    /// <param name="entities">实体集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    public async Task SoftDeleteRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);

        // 幂等：过滤掉已软删的行——不过滤的话，后续常规批量更新的预读（带软删过滤）会因已删行不可见而整批抛异常，
        // 一行并发软删/重复提交就让其余行也删不掉
        var entityArray = entities.Where(entity => !entity.IsDeleted).ToArray();
        if (entityArray.Length == 0)
        {
            return;
        }

        foreach (var entity in entityArray)
        {
            MarkDeleted(entity);
        }

        // 参与更新的均为当前可见行，走常规批量更新（预读校验租户边界）
        await UpdateRangeAsync(entityArray, cancellationToken);
    }

    /// <summary>
    /// 批量软删除实体（根据主键）
    /// </summary>
    /// <param name="ids">主键集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    public async Task SoftDeleteRangeAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        // 内部实现使用数组以提高性能
        var idArray = ids.ToArray();
        if (idArray.Length == 0)
        {
            return;
        }

        // 含软删预读（租户过滤仍生效）：已删行交由实体重载幂等跳过
        var entities = await CreateWithDeletedQueryable()
            .Where(entity => idArray.Contains(entity.BasicId))
            .ToListAsync(cancellationToken);
        await SoftDeleteRangeAsync(entities, cancellationToken);
    }

    /// <summary>
    /// 根据条件批量软删除实体
    /// </summary>
    /// <param name="predicate">条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    public async Task SoftDeleteRangeAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var entities = await CreateWithDeletedQueryable()
            .Where(predicate)
            .ToListAsync(cancellationToken);
        await SoftDeleteRangeAsync(entities, cancellationToken);
    }

    /// <summary>
    /// 根据规约批量软删除实体
    /// </summary>
    /// <param name="specification">规约</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    public async Task SoftDeleteRangeAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        // 与其余重载统一用含软删预读，语义一致（已删行由实体重载幂等跳过）
        var query = CreateWithDeletedQueryable().ApplySpecification(specification);
        var entities = await query.ToListAsync(cancellationToken);
        await SoftDeleteRangeAsync(entities, cancellationToken);
    }

    /// <summary>
    /// 恢复实体
    /// </summary>
    /// <param name="entity">实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    public async Task RestoreAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        // 幂等：未删除的行无需恢复
        if (!entity.IsDeleted)
        {
            return;
        }

        MarkRestored(entity);

        // 必须走含软删写路径：常规 UpdateAsync 的预读带软删过滤，对已软删行必抛「实体不存在」
        _ = await UpdateIncludingDeletedAsync(entity, cancellationToken);
    }

    /// <summary>
    /// 恢复实体（根据主键）
    /// </summary>
    /// <param name="id">主键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    public async Task RestoreAsync(TKey id, CancellationToken cancellationToken = default)
    {
        var entity = await CreateWithDeletedQueryable()
            .Where(item => item.BasicId.Equals(id))
            .FirstAsync(cancellationToken);
        if (entity == null)
        {
            return;
        }

        await RestoreAsync(entity, cancellationToken);
    }

    /// <summary>
    /// 批量恢复实体
    /// </summary>
    /// <param name="entities">实体集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    public async Task RestoreRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);

        // 幂等：只恢复真正处于已删除状态的行
        var entityArray = entities.Where(entity => entity.IsDeleted).ToArray();
        if (entityArray.Length == 0)
        {
            return;
        }

        foreach (var entity in entityArray)
        {
            MarkRestored(entity);
        }

        // 必须走含软删写路径：常规批量更新的可见性校验带软删过滤，已删行不可见会整批抛异常
        _ = await UpdateRangeIncludingDeletedAsync(entityArray, cancellationToken);
    }

    /// <summary>
    /// 批量恢复实体（根据主键）
    /// </summary>
    /// <param name="ids">主键集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    public async Task RestoreRangeAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        // 内部实现使用数组以提高性能
        var idArray = ids.ToArray();
        if (idArray.Length == 0)
        {
            return;
        }

        var entities = await CreateWithDeletedQueryable()
            .Where(entity => idArray.Contains(entity.BasicId))
            .ToListAsync(cancellationToken);
        await RestoreRangeAsync(entities, cancellationToken);
    }

    /// <summary>
    /// 根据条件批量恢复实体
    /// </summary>
    /// <param name="predicate">条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    public async Task RestoreRangeAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var entities = await CreateWithDeletedQueryable()
            .Where(predicate)
            .ToListAsync(cancellationToken);
        await RestoreRangeAsync(entities, cancellationToken);
    }

    /// <summary>
    /// 根据规约批量恢复实体
    /// </summary>
    /// <param name="specification">规约</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    public async Task RestoreRangeAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        var query = CreateWithDeletedQueryable().ApplySpecification(specification);
        var entities = await query.ToListAsync(cancellationToken);
        await RestoreRangeAsync(entities, cancellationToken);
    }

    #endregion 软删除

    #region 物理清除

    /// <summary>
    /// 物理清除单条已软删除的数据（合规/GDPR 场景）
    /// </summary>
    /// <remarks>
    /// 仅允许作用于已软删除的行：活动数据须先软删除再清除（fail-closed）。
    /// 行不存在（或不在当前租户范围内）返回 false——目标态已达成，不视为错误。
    /// </remarks>
    /// <param name="id">主键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否实际删除了数据行</returns>
    public async Task<bool> PurgeAsync(TKey id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 含软删预读：租户过滤仍生效，跨租户主键探不到、也删不到
        var rows = await CreateWithDeletedQueryable()
            .Where(entity => entity.BasicId.Equals(id))
            .Take(1)
            .ToListAsync(cancellationToken);
        var entity = rows.FirstOrDefault();
        if (entity is null)
        {
            return false;
        }

        if (!entity.IsDeleted)
        {
            throw new InvalidOperationException("物理清除仅允许作用于已软删除的数据；活动数据请先软删除。");
        }

        return await ExecutePurgeAsync([id], cancellationToken) > 0;
    }

    /// <summary>
    /// 批量物理清除已软删除的数据（合规/GDPR 场景）
    /// </summary>
    /// <remarks>
    /// 集合中包含未软删除的活动数据时整批拒绝（fail-closed）；
    /// 不存在的主键被忽略（目标态已达成）。
    /// </remarks>
    /// <param name="ids">主键集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实际删除的行数</returns>
    public async Task<int> PurgeRangeAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var idArray = ids.Distinct().ToArray();
        if (idArray.Length == 0)
        {
            return 0;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var entities = await CreateWithDeletedQueryable()
            .Where(entity => idArray.Contains(entity.BasicId))
            .ToListAsync(cancellationToken);
        if (entities.Count == 0)
        {
            return 0;
        }

        if (entities.Any(entity => !entity.IsDeleted))
        {
            throw new InvalidOperationException("物理清除仅允许作用于已软删除的数据；集合中包含未软删除的活动数据，请先软删除。");
        }

        return await ExecutePurgeAsync([.. entities.Select(entity => entity.BasicId)], cancellationToken);
    }

    /// <summary>
    /// 执行物理清除：临时摘除软删过滤（保留租户过滤）后按主键集合 DELETE
    /// </summary>
    /// <remarks>
    /// <c>Deleteable&lt;T&gt;()</c> 工厂在创建瞬间基于当前 QueryFilter 状态把过滤烘进 DELETE 的 WHERE：
    /// 全局软删过滤（<c>Is_Deleted=0</c>）会让已软删行永远匹配不到，必须在工厂调用前临时摘除；
    /// 用 <c>ClearAndBackup&lt;ISoftDelete&gt;()</c> 只摘软删、保留租户过滤（DELETE 语句自身仍带租户边界），
    /// finally 恢复——摘除窗口内本客户端为顺序使用（工作单元语义），无并发泄漏。
    /// <b>注意：SqlSugar 的 <c>_BackUpFilters</c> 是单槽而非栈</b>——本方法须保持全仓唯一的 ClearAndBackup 调用点，
    /// 任何嵌套/第二调用方都会覆盖备份、把软删过滤永久丢出该 provider（波及该上下文余下全部查询）。
    /// 保留 <c>EnableDiffLogEvent</c>：物理清除必须留审计痕迹（DiffLog writer 只走 Insertable、不触碰 QueryFilter，无重入风险，已核实）。
    /// </remarks>
    /// <param name="idArray">已确认可清除的主键集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实际删除的行数</returns>
    private async Task<int> ExecutePurgeAsync(TKey[] idArray, CancellationToken cancellationToken)
    {
        // 捕获同一客户端实例：QueryFilter 的摘除/还原必须与 Deleteable 工厂作用于同一 provider
        var db = DbClient;
        try
        {
            db.QueryFilter.ClearAndBackup<ISoftDelete>();
            return await db.Deleteable<TEntity>()
                .In(idArray.Cast<object>().ToArray())
                .EnableDiffLogEvent(typeof(TEntity))
                .ExecuteCommandAsync(cancellationToken);
        }
        finally
        {
            db.QueryFilter.Restore();
        }
    }

    #endregion 物理清除

    #region 查询软删除记录

    /// <summary>
    /// 获取所有软删除实体
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>只读实体集合</returns>
    public async Task<IReadOnlyList<TEntity>> GetAllWithDeletedAsync(CancellationToken cancellationToken = default)
    {
        return await CreateWithDeletedQueryable()
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 获取所有软删除实体
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>只读实体集合</returns>
    public async Task<IReadOnlyList<TEntity>> GetDeletedAsync(CancellationToken cancellationToken = default)
    {
        return await CreateWithDeletedQueryable()
            .Where(entity => entity.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 根据条件获取软删除实体
    /// </summary>
    /// <param name="predicate">条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>只读实体集合</returns>
    public async Task<IReadOnlyList<TEntity>> GetDeletedAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await CreateWithDeletedQueryable()
            .Where(entity => entity.IsDeleted)
            .Where(predicate)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 根据规约获取软删除实体
    /// </summary>
    /// <param name="specification">规约</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>只读实体集合</returns>
    public async Task<IReadOnlyList<TEntity>> GetDeletedAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        return await CreateWithDeletedQueryable()
            .Where(entity => entity.IsDeleted)
            .ApplySpecification(specification)
            .ToListAsync(cancellationToken);
    }

    #endregion 查询软删除记录

    #region 状态标记

    /// <summary>
    /// 标记实体为已删除（DeletedId/DeletedBy 由 DataExecuting AOP 在 IsDeleted=true 时填充）
    /// </summary>
    /// <param name="entity">实体</param>
    private static void MarkDeleted(TEntity entity)
    {
        entity.IsDeleted = true;
        if (entity is IDeletionEntity deletionEntity)
        {
            deletionEntity.DeletedTime = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// 标记实体为已恢复，并清空全部删除审计字段
    /// </summary>
    /// <remarks>
    /// AOP 的 ToDeleted 仅在 IsDeleted=true 时介入删除审计字段，恢复路径必须由仓储层清理；
    /// 只清 DeletedTime 会把从库里读回的 DeletedId/DeletedBy 原样写回，残留「被 X 删除于 null」的矛盾数据。
    /// </remarks>
    /// <param name="entity">实体</param>
    private static void MarkRestored(TEntity entity)
    {
        entity.IsDeleted = false;
        if (entity is IDeletionEntity deletionEntity)
        {
            deletionEntity.DeletedTime = null;
        }

        if (entity is IDeletionEntity<TKey> typedDeletionEntity)
        {
            typedDeletionEntity.DeletedId = default;
            typedDeletionEntity.DeletedBy = null;
        }
    }

    #endregion 状态标记
}
