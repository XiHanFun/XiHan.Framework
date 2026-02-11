#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IEventInbox
// Guid:56a10754-5d47-42a7-a0d2-5a61d2d6636d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/24 05:09:27
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Linq.Expressions;

namespace XiHan.Framework.EventBus.Abstractions.Distributed;

/// <summary>
/// 事件收件箱接口
/// </summary>
public interface IEventInbox
{
    /// <summary>
    /// 将事件信息添加到收件箱
    /// </summary>
    /// <param name="incomingEvent">入站事件信息</param>
    /// <returns></returns>
    Task EnqueueAsync(IncomingEventInfo incomingEvent);

    /// <summary>
    /// 获取等待处理的事件信息
    /// </summary>
    /// <param name="maxCount">最大数量</param>
    /// <param name="filter">过滤条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task<List<IncomingEventInfo>> GetWaitingEventsAsync(int maxCount, Expression<Func<IIncomingEventInfo, bool>>? filter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 标记事件为已处理
    /// </summary>
    /// <param name="id">事件唯一标识符</param>
    /// <returns></returns>
    Task MarkAsProcessedAsync(Guid id);

    /// <summary>
    /// 延迟处理事件
    /// </summary>
    /// <param name="id">事件唯一标识符</param>
    /// <param name="retryCount">重试次数</param>
    /// <param name="nextRetryTime">下次重试时间</param>
    /// <returns></returns>
    Task RetryLaterAsync(Guid id, int retryCount, DateTime? nextRetryTime);

    /// <summary>
    /// 标记事件为已丢弃
    /// </summary>
    /// <param name="id">事件唯一标识</param>
    /// <returns></returns>
    Task MarkAsDiscardAsync(Guid id);

    /// <summary>
    /// 检查消息标识符是否存在
    /// </summary>
    /// <param name="messageId">消息标识符</param>
    /// <returns></returns>
    Task<bool> ExistsByMessageIdAsync(string messageId);

    /// <summary>
    /// 删除过期事件
    /// </summary>
    /// <returns></returns>
    Task DeleteOldEventsAsync();
}
