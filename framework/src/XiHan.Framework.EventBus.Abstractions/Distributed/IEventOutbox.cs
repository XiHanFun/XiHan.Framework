// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Linq.Expressions;

namespace XiHan.Framework.EventBus.Abstractions.Distributed;

/// <summary>
/// 事件发件箱接口
/// </summary>
public interface IEventOutbox
{
    /// <summary>
    /// 将事件信息添加到发件箱
    /// </summary>
    /// <param name="outgoingEvent">出站事件信息</param>
    /// <returns></returns>
    Task EnqueueAsync(OutgoingEventInfo outgoingEvent);

    /// <summary>
    /// 获取等待发送的事件信息
    /// </summary>
    /// <param name="maxCount">最大数量</param>
    /// <param name="filter">过滤条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task<List<OutgoingEventInfo>> GetWaitingEventsAsync(int maxCount, Expression<Func<IOutgoingEventInfo, bool>>? filter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除已发送的事件信息
    /// </summary>
    /// <param name="id">事件唯一标识符</param>
    /// <returns></returns>
    Task DeleteAsync(Guid id);

    /// <summary>
    /// 批量删除已发送的事件信息
    /// </summary>
    /// <param name="ids">事件唯一标识符列表</param>
    /// <returns></returns>
    Task DeleteManyAsync(IEnumerable<Guid> ids);
}
