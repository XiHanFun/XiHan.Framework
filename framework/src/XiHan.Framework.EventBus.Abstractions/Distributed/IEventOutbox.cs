#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IEventOutbox
// Guid:714b369f-782c-4022-9009-e58e9dbf4404
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/24 5:07:34
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
