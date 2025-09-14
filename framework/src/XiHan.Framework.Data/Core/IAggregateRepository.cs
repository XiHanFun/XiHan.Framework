#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IAggregateRepository
// Guid:b2c3d4e5-f6a7-2345-6789-012345678901
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/12 21:15:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Aggregates.Abstracts;
using XiHan.Framework.Domain.Repositories;

namespace XiHan.Framework.Data.Core;

/// <summary>
/// 聚合根仓储接口
/// 专门用于聚合根的数据访问，集成领域事件处理
/// </summary>
/// <typeparam name="TAggregateRoot">聚合根类型</typeparam>
/// <typeparam name="TKey">主键类型</typeparam>
public interface IAggregateRepository<TAggregateRoot, TKey> : IDataRepository<TAggregateRoot, TKey>
    where TAggregateRoot : class, IAggregateRoot<TKey>, new()
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 添加聚合根并处理领域事件
    /// </summary>
    /// <param name="aggregate">聚合根</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>添加的聚合根</returns>
    new Task<TAggregateRoot> AddAsync(TAggregateRoot aggregate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新聚合根并处理领域事件
    /// </summary>
    /// <param name="aggregate">聚合根</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新的聚合根</returns>
    new Task<TAggregateRoot> UpdateAsync(TAggregateRoot aggregate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除聚合根并处理领域事件
    /// </summary>
    /// <param name="aggregate">聚合根</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    new Task DeleteAsync(TAggregateRoot aggregate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存聚合根变更（包括事件处理）
    /// </summary>
    /// <param name="aggregate">聚合根</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    Task SaveAggregateAsync(TAggregateRoot aggregate, CancellationToken cancellationToken = default);
}
