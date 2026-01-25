#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IAuditedRepository
// Guid:fec439bd-598b-42c8-8c34-97913188c5a4
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/25 4:55:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Repositories.Models;

namespace XiHan.Framework.Domain.Repositories;

/// <summary>
/// 审计仓储接口，针对实现完整审计的实体提供便捷查询
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TKey">主键类型</typeparam>
public interface IAuditedRepository<TEntity, TKey> : ISoftDeleteRepositoryBase<TEntity, TKey>
    where TEntity : class, IFullAuditedEntity<TKey>
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 根据创建者查找实体
    /// </summary>
    /// <param name="createdId">创建者的唯一标识</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>只读实体集合（提供 Count 和索引访问）</returns>
    Task<IReadOnlyList<TEntity>> GetByCreatorAsync(TKey createdId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据修改者查找实体
    /// </summary>
    /// <param name="modifiedId">最后修改者的唯一标识</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>只读实体集合（提供 Count 和索引访问）</returns>
    Task<IReadOnlyList<TEntity>> GetByModifierAsync(TKey modifiedId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据删除者查找实体
    /// </summary>
    /// <param name="deletedId">删除者的唯一标识</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>只读实体集合（提供 Count 和索引访问）</returns>
    Task<IReadOnlyList<TEntity>> GetByDeleterAsync(TKey deletedId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定时间范围内创建的实体
    /// </summary>
    /// <param name="startTime">时间范围开始点（包含）</param>
    /// <param name="endTime">时间范围结束点（包含）</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>只读实体集合（提供 Count 和索引访问）</returns>
    Task<IReadOnlyList<TEntity>> GetCreatedBetweenAsync(DateTimeOffset startTime, DateTimeOffset endTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定时间范围内修改的实体
    /// </summary>
    /// <param name="startTime">时间范围开始点（包含）</param>
    /// <param name="endTime">时间范围结束点（包含）</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>只读实体集合（提供 Count 和索引访问）</returns>
    Task<IReadOnlyList<TEntity>> GetModifiedBetweenAsync(DateTimeOffset startTime, DateTimeOffset endTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定时间范围内删除的实体
    /// </summary>
    /// <param name="startTime">时间范围开始点（包含）</param>
    /// <param name="endTime">时间范围结束点（包含）</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>只读实体集合（提供 Count 和索引访问）</returns>
    Task<IReadOnlyList<TEntity>> GetDeletedBetweenAsync(DateTimeOffset startTime, DateTimeOffset endTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据组合审计条件筛选实体
    /// </summary>
    /// <param name="options">审计查询参数</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>只读实体集合（提供 Count 和索引访问）</returns>
    Task<IReadOnlyList<TEntity>> GetByAuditAsync(AuditQueryOptions<TKey> options, CancellationToken cancellationToken = default);
}
