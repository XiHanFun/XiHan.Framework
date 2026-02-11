#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IAggregateRootRepository
// Guid:b2c3d4e5-f6a7-2345-6789-012345678901
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/12 21:15:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Aggregates.Abstracts;

namespace XiHan.Framework.Domain.Repositories;

/// <summary>
/// 聚合根仓储接口，扩展常规仓储以支持聚合根的一致性管理与领域事件处理
/// </summary>
/// <typeparam name="TAggregateRoot">聚合根类型</typeparam>
/// <typeparam name="TKey">主键类型</typeparam>
public interface IAggregateRootRepository<TAggregateRoot, TKey> : IAuditedRepository<TAggregateRoot, TKey>
    where TAggregateRoot : class, IAggregateRoot<TKey>, new()
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 添加聚合根并触发聚合根上的领域事件
    /// </summary>
    /// <param name="aggregate">待添加的聚合根实例</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>已持久化的聚合根实例</returns>
    new Task<TAggregateRoot> AddAsync(TAggregateRoot aggregate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新聚合根并触发聚合根上的领域事件
    /// </summary>
    /// <param name="aggregate">待更新的聚合根实例</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>更新后的聚合根实例</returns>
    new Task<TAggregateRoot> UpdateAsync(TAggregateRoot aggregate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除聚合根并触发聚合根上的领域事件
    /// </summary>
    /// <param name="aggregate">待删除的聚合根实例</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>表示删除操作的任务</returns>
    new Task DeleteAsync(TAggregateRoot aggregate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存聚合根变更（包括事件处理）
    /// </summary>
    /// <param name="aggregate">需要持久化的聚合根实例</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>表示保存操作的任务</returns>
    Task SaveAggregateAsync(TAggregateRoot aggregate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取聚合根及其相关实体
    /// </summary>
    /// <param name="id">聚合根主键</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>聚合根实例，如果不存在则返回 <c>null</c></returns>
    Task<TAggregateRoot?> GetAggregateAsync(TKey id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取包含领域事件的聚合根
    /// </summary>
    /// <param name="id">聚合根主键</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>包含未发布领域事件的聚合根实例，如果不存在则返回 <c>null</c></returns>
    Task<TAggregateRoot?> GetWithEventsAsync(TKey id, CancellationToken cancellationToken = default);
}
