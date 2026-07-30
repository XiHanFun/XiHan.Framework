// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Linq.Expressions;
using XiHan.Framework.EventBus.Abstractions.Distributed;

namespace XiHan.Framework.EventBus.Distributed;

/// <summary>
/// 基于内存的事件发件箱实现
/// </summary>
public class InMemoryEventOutbox : IEventOutbox
{
    private readonly ConcurrentDictionary<Guid, OutgoingEventInfo> _outgoingEvents = new();

    /// <summary>
    /// 加入出站事件
    /// </summary>
    /// <param name="outgoingEvent"></param>
    /// <returns></returns>
    public Task EnqueueAsync(OutgoingEventInfo outgoingEvent)
    {
        ArgumentNullException.ThrowIfNull(outgoingEvent);
        _outgoingEvents[outgoingEvent.Id] = outgoingEvent;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取等待发送事件
    /// </summary>
    /// <param name="maxCount"></param>
    /// <param name="filter"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<List<OutgoingEventInfo>> GetWaitingEventsAsync(
        int maxCount,
        Expression<Func<IOutgoingEventInfo, bool>>? filter = null,
        CancellationToken cancellationToken = default)
    {
        if (maxCount <= 0)
        {
            return Task.FromResult(new List<OutgoingEventInfo>());
        }

        cancellationToken.ThrowIfCancellationRequested();
        var predicate = filter?.Compile();
        var items = _outgoingEvents.Values
            .OrderBy(item => item.CreatedTime)
            .Where(item => predicate == null || predicate(item))
            .Take(maxCount)
            .ToList();
        return Task.FromResult(items);
    }

    /// <summary>
    /// 删除出站事件
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public Task DeleteAsync(Guid id)
    {
        _outgoingEvents.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 批量删除出站事件
    /// </summary>
    /// <param name="ids"></param>
    /// <returns></returns>
    public Task DeleteManyAsync(IEnumerable<Guid> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        foreach (var id in ids.Distinct())
        {
            _outgoingEvents.TryRemove(id, out _);
        }

        return Task.CompletedTask;
    }
}
