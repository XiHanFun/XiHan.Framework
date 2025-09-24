#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ISoftDeleteRepositoryBase
// Guid:4677f6af-fd69-49e5-bda2-e255a41b43fe
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/25 4:42:39
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Linq.Expressions;
using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Specifications.Abstracts;

namespace XiHan.Framework.Domain.Repositories;

/// <summary>
/// 软删除仓储接口，扩展常规仓储以支持软删除与恢复操作
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TKey">主键类型</typeparam>
public interface ISoftDeleteRepositoryBase<TEntity, TKey> : IRepositoryBase<TEntity, TKey>
    where TEntity : class, IEntityBase<TKey>, ISoftDelete
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 软删除实体
    /// </summary>
    /// <param name="entity">待软删除的实体实例</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>表示软删除操作的任务</returns>
    Task SoftDeleteAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 软删除实体（根据主键）
    /// </summary>
    /// <param name="id">实体主键</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>表示软删除操作的任务</returns>
    Task SoftDeleteAsync(TKey id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量软删除实体
    /// </summary>
    /// <param name="entities">待软删除的实体集合</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>表示软删除操作的任务</returns>
    Task SoftDeleteRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量软删除实体（根据主键）
    /// </summary>
    /// <param name="ids">需要软删除的实体主键集合</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>表示软删除操作的任务</returns>
    Task SoftDeleteRangeAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据条件批量软删除实体
    /// </summary>
    /// <param name="predicate">用于筛选软删除实体的条件表达式</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>表示软删除操作的任务</returns>
    Task SoftDeleteRangeAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据规约批量软删除实体
    /// </summary>
    /// <param name="specification">定义软删除条件的规约</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>表示软删除操作的任务</returns>
    Task SoftDeleteRangeAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default);

    /// <summary>
    /// 恢复软删除的实体
    /// </summary>
    /// <param name="entity">待恢复的实体实例</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>表示恢复操作的任务</returns>
    Task RestoreAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 恢复软删除的实体（根据主键）
    /// </summary>
    /// <param name="id">实体主键</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>表示恢复操作的任务</returns>
    Task RestoreAsync(TKey id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量恢复软删除的实体
    /// </summary>
    /// <param name="entities">待恢复的实体集合</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>表示恢复操作的任务</returns>
    Task RestoreRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量恢复软删除的实体（根据主键）
    /// </summary>
    /// <param name="ids">需要恢复的实体主键集合</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>表示恢复操作的任务</returns>
    Task RestoreRangeAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据条件批量恢复软删除的实体
    /// </summary>
    /// <param name="predicate">用于筛选恢复实体的条件表达式</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>表示恢复操作的任务</returns>
    Task RestoreRangeAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据规约批量恢复软删除的实体
    /// </summary>
    /// <param name="specification">定义恢复条件的规约</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>表示恢复操作的任务</returns>
    Task RestoreRangeAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取包含已删除的所有实体
    /// </summary>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>包含软删除实体的集合</returns>
    Task<IEnumerable<TEntity>> GetAllWithDeletedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取已删除的实体
    /// </summary>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>所有软删除实体的集合</returns>
    Task<IEnumerable<TEntity>> GetDeletedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据条件获取已删除的实体
    /// </summary>
    /// <param name="predicate">用于筛选软删除实体的条件表达式</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>满足条件的软删除实体集合</returns>
    Task<IEnumerable<TEntity>> GetDeletedAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据规约获取已删除的实体
    /// </summary>
    /// <param name="specification">定义筛选条件的规约</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>满足规约的软删除实体集合</returns>
    Task<IEnumerable<TEntity>> GetDeletedAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default);
}
