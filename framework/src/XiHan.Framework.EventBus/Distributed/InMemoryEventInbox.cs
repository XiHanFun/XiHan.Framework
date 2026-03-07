#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:InMemoryEventInbox
// Guid:b9f86bf5-e141-4a36-9589-5b0c35529b0f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/08 20:48:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using System.Linq.Expressions;
using XiHan.Framework.EventBus.Abstractions.Distributed;

namespace XiHan.Framework.EventBus.Distributed;

/// <summary>
/// 基于内存的事件收件箱实现
/// </summary>
public class InMemoryEventInbox : IEventInbox
{
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(7);
    private readonly ConcurrentDictionary<Guid, InboxEntry> _incomingEvents = new();

    /// <summary>
    /// 加入入站事件
    /// </summary>
    /// <param name="incomingEvent"></param>
    /// <returns></returns>
    public Task EnqueueAsync(IncomingEventInfo incomingEvent)
    {
        ArgumentNullException.ThrowIfNull(incomingEvent);
        _incomingEvents[incomingEvent.Id] = new InboxEntry(incomingEvent);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取等待处理事件
    /// </summary>
    /// <param name="maxCount"></param>
    /// <param name="filter"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<List<IncomingEventInfo>> GetWaitingEventsAsync(
        int maxCount,
        Expression<Func<IIncomingEventInfo, bool>>? filter = null,
        CancellationToken cancellationToken = default)
    {
        if (maxCount <= 0)
        {
            return Task.FromResult(new List<IncomingEventInfo>());
        }

        cancellationToken.ThrowIfCancellationRequested();
        var now = DateTime.UtcNow;
        var predicate = filter?.Compile();

        var items = _incomingEvents.Values
            .Where(entry => entry.State == InboxEventState.Waiting)
            .Where(entry => !entry.NextRetryTime.HasValue || entry.NextRetryTime.Value <= now)
            .Select(entry => entry.Event)
            .Where(item => predicate == null || predicate(item))
            .OrderBy(item => item.CreatedTime)
            .Take(maxCount)
            .ToList();

        return Task.FromResult(items);
    }

    /// <summary>
    /// 标记事件为已处理
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public Task MarkAsProcessedAsync(Guid id)
    {
        if (_incomingEvents.TryGetValue(id, out var entry))
        {
            entry.State = InboxEventState.Processed;
            entry.NextRetryTime = null;
            entry.LastModificationTime = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 延迟重试事件
    /// </summary>
    /// <param name="id"></param>
    /// <param name="retryCount"></param>
    /// <param name="nextRetryTime"></param>
    /// <returns></returns>
    public Task RetryLaterAsync(Guid id, int retryCount, DateTime? nextRetryTime)
    {
        if (_incomingEvents.TryGetValue(id, out var entry))
        {
            entry.State = InboxEventState.Waiting;
            entry.RetryCount = retryCount;
            entry.NextRetryTime = nextRetryTime ?? DateTime.UtcNow;
            entry.LastModificationTime = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 标记事件为已丢弃
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public Task MarkAsDiscardAsync(Guid id)
    {
        if (_incomingEvents.TryGetValue(id, out var entry))
        {
            entry.State = InboxEventState.Discarded;
            entry.NextRetryTime = null;
            entry.LastModificationTime = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 判断消息是否存在
    /// </summary>
    /// <param name="messageId"></param>
    /// <returns></returns>
    public Task<bool> ExistsByMessageIdAsync(string messageId)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            return Task.FromResult(false);
        }

        var exists = _incomingEvents.Values.Any(entry =>
            string.Equals(entry.Event.MessageId, messageId, StringComparison.Ordinal));
        return Task.FromResult(exists);
    }

    /// <summary>
    /// 删除过期事件
    /// </summary>
    /// <returns></returns>
    public Task DeleteOldEventsAsync()
    {
        var cutoff = DateTime.UtcNow - RetentionPeriod;
        foreach (var pair in _incomingEvents)
        {
            if (pair.Value.State == InboxEventState.Waiting)
            {
                continue;
            }

            if (pair.Value.LastModificationTime <= cutoff)
            {
                _incomingEvents.TryRemove(pair.Key, out _);
            }
        }

        return Task.CompletedTask;
    }

    private sealed class InboxEntry
    {
        public InboxEntry(IncomingEventInfo eventInfo)
        {
            Event = eventInfo;
            State = InboxEventState.Waiting;
            LastModificationTime = DateTime.UtcNow;
        }

        public IncomingEventInfo Event { get; }

        public InboxEventState State { get; set; }

        public int RetryCount { get; set; }

        public DateTime? NextRetryTime { get; set; }

        public DateTime LastModificationTime { get; set; }
    }

    private enum InboxEventState
    {
        Waiting = 0,
        Processed = 1,
        Discarded = 2
    }
}
