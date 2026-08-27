// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Linq.Expressions;
using System.Text;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.Abstractions.Local;

namespace XiHan.Framework.EventBus.Abstractions.Tests;

/// <summary>
/// 测试用事件数据
/// </summary>
public class SampleEvent
{
    /// <summary>
    /// 载荷
    /// </summary>
    public string Payload { get; set; } = "payload";
}

/// <summary>
/// 另一个测试用事件数据，用于验证同一处理器可承载多个事件类型
/// </summary>
public class AnotherSampleEvent
{
    /// <summary>
    /// 载荷
    /// </summary>
    public string Payload { get; set; } = "another";
}

/// <summary>
/// 测试桩：声明自身携带租户信息的事件数据
/// </summary>
public sealed class TenantAwareSampleEvent : IEventDataMayHaveTenantId
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="tenantId">租户唯一标识</param>
    public TenantAwareSampleEvent(long tenantId)
    {
        TenantId = tenantId;
    }

    /// <summary>
    /// 租户唯一标识
    /// </summary>
    public long TenantId { get; }

    /// <summary>
    /// 声明该事件与租户相关，并回填租户唯一标识
    /// </summary>
    /// <param name="tenantId">租户唯一标识</param>
    /// <returns>恒为 true</returns>
    public bool IsMultiTenant(out long? tenantId)
    {
        tenantId = TenantId;
        return true;
    }
}

/// <summary>
/// 测试桩：声明自身不携带租户信息的事件数据
/// </summary>
public sealed class TenantAgnosticSampleEvent : IEventDataMayHaveTenantId
{
    /// <summary>
    /// 声明该事件与租户无关，出参不具有意义
    /// </summary>
    /// <param name="tenantId">租户唯一标识</param>
    /// <returns>恒为 false</returns>
    public bool IsMultiTenant(out long? tenantId)
    {
        tenantId = null;
        return false;
    }
}

/// <summary>
/// 测试桩：带单个可继承泛型参数的事件数据
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
public sealed class InheritableSampleEventData<TEntity> : IEventDataWithInheritableGenericArgument
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="entity">实体</param>
    public InheritableSampleEventData(TEntity entity)
    {
        Entity = entity;
    }

    /// <summary>
    /// 实体
    /// </summary>
    public TEntity Entity { get; }

    /// <summary>
    /// 获取构造参数，供事件总线用父类泛型参数重建实例
    /// </summary>
    /// <returns>构造函数参数</returns>
    public object[] GetConstructorArgs()
    {
        return [Entity!];
    }
}

/// <summary>
/// 测试桩：基类实体
/// </summary>
public class SamplePerson
{
    /// <summary>
    /// 姓名
    /// </summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// 测试桩：派生实体
/// </summary>
public sealed class SampleStudent : SamplePerson
{
    /// <summary>
    /// 学号
    /// </summary>
    public string StudentNo { get; set; } = string.Empty;
}

/// <summary>
/// 测试桩：记录处理次数的本地事件处理器
/// </summary>
public sealed class RecordingLocalEventHandler : ILocalEventHandler<SampleEvent>
{
    /// <summary>
    /// 已处理的事件
    /// </summary>
    public List<SampleEvent> Handled { get; } = [];

    /// <summary>
    /// 处理事件
    /// </summary>
    /// <param name="eventData">事件数据</param>
    /// <returns>表示异步操作的任务</returns>
    public Task HandleEventAsync(SampleEvent eventData)
    {
        Handled.Add(eventData);
        return Task.CompletedTask;
    }
}

/// <summary>
/// 测试桩：同时承载多个事件类型的处理器
/// </summary>
public sealed class MultiEventHandler :
    ILocalEventHandler<SampleEvent>,
    ILocalEventHandler<AnotherSampleEvent>,
    IDistributedEventHandler<SampleEvent>
{
    /// <summary>
    /// 已处理的事件
    /// </summary>
    public List<object> Handled { get; } = [];

    /// <summary>
    /// 处理 <see cref="SampleEvent"/>
    /// </summary>
    /// <param name="eventData">事件数据</param>
    /// <returns>表示异步操作的任务</returns>
    public Task HandleEventAsync(SampleEvent eventData)
    {
        Handled.Add(eventData);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 处理 <see cref="AnotherSampleEvent"/>
    /// </summary>
    /// <param name="eventData">事件数据</param>
    /// <returns>表示异步操作的任务</returns>
    public Task HandleEventAsync(AnotherSampleEvent eventData)
    {
        Handled.Add(eventData);
        return Task.CompletedTask;
    }
}

/// <summary>
/// 测试桩：以 object 为事件类型的本地处理器，用于验证逆变
/// </summary>
public sealed class ObjectLocalEventHandler : ILocalEventHandler<object>
{
    /// <summary>
    /// 已处理的事件
    /// </summary>
    public List<object> Handled { get; } = [];

    /// <summary>
    /// 处理事件
    /// </summary>
    /// <param name="eventData">事件数据</param>
    /// <returns>表示异步操作的任务</returns>
    public Task HandleEventAsync(object eventData)
    {
        Handled.Add(eventData);
        return Task.CompletedTask;
    }
}

/// <summary>
/// 测试桩：事件处理器释放包装
/// </summary>
public sealed class FakeEventHandlerDisposeWrapper : IEventHandlerDisposeWrapper
{
    private readonly Action? _disposeAction;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="eventHandler">事件处理器</param>
    /// <param name="disposeAction">释放时的回调</param>
    public FakeEventHandlerDisposeWrapper(IEventHandler eventHandler, Action? disposeAction = null)
    {
        EventHandler = eventHandler;
        _disposeAction = disposeAction;
    }

    /// <summary>
    /// 事件处理器
    /// </summary>
    public IEventHandler EventHandler { get; }

    /// <summary>
    /// 是否已释放
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        IsDisposed = true;
        _disposeAction?.Invoke();
    }
}

/// <summary>
/// 测试桩：始终返回同一处理器实例的工厂
/// </summary>
public sealed class SingleInstanceEventHandlerFactory : IEventHandlerFactory
{
    private readonly IEventHandler _eventHandler;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="eventHandler">事件处理器</param>
    public SingleInstanceEventHandlerFactory(IEventHandler eventHandler)
    {
        _eventHandler = eventHandler;
    }

    /// <summary>
    /// 已发放的包装数量
    /// </summary>
    public int GetHandlerCount { get; private set; }

    /// <summary>
    /// 最近一次发放的包装
    /// </summary>
    public FakeEventHandlerDisposeWrapper? LastWrapper { get; private set; }

    /// <summary>
    /// 获取事件处理器包装
    /// </summary>
    /// <returns>事件处理器包装</returns>
    public IEventHandlerDisposeWrapper GetHandler()
    {
        GetHandlerCount++;
        LastWrapper = new FakeEventHandlerDisposeWrapper(_eventHandler);
        return LastWrapper;
    }

    /// <summary>
    /// 判断当前工厂是否已在给定列表中
    /// </summary>
    /// <param name="handlerFactories">工厂列表</param>
    /// <returns>存在则返回 true</returns>
    public bool IsInFactories(List<IEventHandlerFactory> handlerFactories)
    {
        return handlerFactories.Contains(this);
    }
}

/// <summary>
/// 测试桩：按事件类型反射派发的事件处理器调用器
/// </summary>
/// <remarks>
/// 复刻真实事件总线的派发方式：用运行时事件类型构造 <see cref="ILocalEventHandler{TEvent}"/> 闭合接口再调用，
/// 用于验证抽象包定义的处理器接口可被这种方式驱动。
/// </remarks>
public sealed class ReflectionEventHandlerInvoker : IEventHandlerInvoker
{
    /// <summary>
    /// 实际派发次数
    /// </summary>
    public int InvokedCount { get; private set; }

    /// <summary>
    /// 调用事件处理器
    /// </summary>
    /// <param name="eventHandler">事件处理器</param>
    /// <param name="eventData">事件数据</param>
    /// <param name="eventType">事件类型</param>
    /// <returns>表示异步操作的任务</returns>
    public Task InvokeAsync(IEventHandler eventHandler, object eventData, Type eventType)
    {
        var handlerInterface = typeof(ILocalEventHandler<>).MakeGenericType(eventType);
        if (!handlerInterface.IsInstanceOfType(eventHandler))
        {
            return Task.CompletedTask;
        }

        var method = handlerInterface.GetMethod(nameof(ILocalEventHandler<object>.HandleEventAsync))!;
        InvokedCount++;
        return (Task)method.Invoke(eventHandler, [eventData])!;
    }
}

/// <summary>
/// 测试桩：内存发件箱
/// </summary>
public sealed class InMemoryEventOutbox : IEventOutbox
{
    private readonly List<OutgoingEventInfo> _events = [];

    /// <summary>
    /// 当前留存的事件
    /// </summary>
    public IReadOnlyList<OutgoingEventInfo> Events => _events;

    /// <summary>
    /// 入队
    /// </summary>
    /// <param name="outgoingEvent">出站事件信息</param>
    /// <returns>表示异步操作的任务</returns>
    public Task EnqueueAsync(OutgoingEventInfo outgoingEvent)
    {
        _events.Add(outgoingEvent);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取等待发送的事件
    /// </summary>
    /// <param name="maxCount">最大数量</param>
    /// <param name="filter">过滤条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>等待发送的事件</returns>
    public Task<List<OutgoingEventInfo>> GetWaitingEventsAsync(
        int maxCount,
        Expression<Func<IOutgoingEventInfo, bool>>? filter = null,
        CancellationToken cancellationToken = default)
    {
        var predicate = filter?.Compile();
        var waiting = _events
            .Where(x => predicate is null || predicate(x))
            .Take(maxCount)
            .ToList();
        return Task.FromResult(waiting);
    }

    /// <summary>
    /// 删除单个事件
    /// </summary>
    /// <param name="id">事件唯一标识</param>
    /// <returns>表示异步操作的任务</returns>
    public Task DeleteAsync(Guid id)
    {
        _events.RemoveAll(x => x.Id == id);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 批量删除事件
    /// </summary>
    /// <param name="ids">事件唯一标识集合</param>
    /// <returns>表示异步操作的任务</returns>
    public Task DeleteManyAsync(IEnumerable<Guid> ids)
    {
        var idSet = ids.ToHashSet();
        _events.RemoveAll(x => idSet.Contains(x.Id));
        return Task.CompletedTask;
    }
}

/// <summary>
/// 内存收件箱中的一条记录及其状态
/// </summary>
public sealed class InMemoryInboxEntry
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="incomingEvent">入站事件信息</param>
    public InMemoryInboxEntry(IncomingEventInfo incomingEvent)
    {
        Event = incomingEvent;
    }

    /// <summary>
    /// 入站事件信息
    /// </summary>
    public IncomingEventInfo Event { get; }

    /// <summary>
    /// 是否已处理
    /// </summary>
    public bool IsProcessed { get; set; }

    /// <summary>
    /// 是否已丢弃
    /// </summary>
    public bool IsDiscarded { get; set; }

    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// 下次重试时间
    /// </summary>
    public DateTime? NextRetryTime { get; set; }
}

/// <summary>
/// 测试桩：内存收件箱
/// </summary>
public sealed class InMemoryEventInbox : IEventInbox
{
    private readonly List<InMemoryInboxEntry> _entries = [];

    /// <summary>
    /// 当前留存的记录
    /// </summary>
    public IReadOnlyList<InMemoryInboxEntry> Entries => _entries;

    /// <summary>
    /// 入队
    /// </summary>
    /// <param name="incomingEvent">入站事件信息</param>
    /// <returns>表示异步操作的任务</returns>
    public Task EnqueueAsync(IncomingEventInfo incomingEvent)
    {
        _entries.Add(new InMemoryInboxEntry(incomingEvent));
        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取等待处理的事件
    /// </summary>
    /// <param name="maxCount">最大数量</param>
    /// <param name="filter">过滤条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>等待处理的事件</returns>
    public Task<List<IncomingEventInfo>> GetWaitingEventsAsync(
        int maxCount,
        Expression<Func<IIncomingEventInfo, bool>>? filter = null,
        CancellationToken cancellationToken = default)
    {
        var predicate = filter?.Compile();
        var waiting = _entries
            .Where(x => !x.IsProcessed && !x.IsDiscarded)
            .Where(x => predicate is null || predicate(x.Event))
            .Take(maxCount)
            .Select(x => x.Event)
            .ToList();
        return Task.FromResult(waiting);
    }

    /// <summary>
    /// 标记为已处理
    /// </summary>
    /// <param name="id">事件唯一标识</param>
    /// <returns>表示异步操作的任务</returns>
    public Task MarkAsProcessedAsync(Guid id)
    {
        Find(id).IsProcessed = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 延迟重试
    /// </summary>
    /// <param name="id">事件唯一标识</param>
    /// <param name="retryCount">重试次数</param>
    /// <param name="nextRetryTime">下次重试时间</param>
    /// <returns>表示异步操作的任务</returns>
    public Task RetryLaterAsync(Guid id, int retryCount, DateTime? nextRetryTime)
    {
        var entry = Find(id);
        entry.RetryCount = retryCount;
        entry.NextRetryTime = nextRetryTime;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 标记为已丢弃
    /// </summary>
    /// <param name="id">事件唯一标识</param>
    /// <returns>表示异步操作的任务</returns>
    public Task MarkAsDiscardAsync(Guid id)
    {
        Find(id).IsDiscarded = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 判断消息标识是否已存在
    /// </summary>
    /// <param name="messageId">消息标识</param>
    /// <returns>存在则返回 true</returns>
    public Task<bool> ExistsByMessageIdAsync(string messageId)
    {
        return Task.FromResult(_entries.Any(x => x.Event.MessageId == messageId));
    }

    /// <summary>
    /// 清理已终结的记录
    /// </summary>
    /// <returns>表示异步操作的任务</returns>
    public Task DeleteOldEventsAsync()
    {
        _entries.RemoveAll(x => x.IsProcessed || x.IsDiscarded);
        return Task.CompletedTask;
    }

    private InMemoryInboxEntry Find(Guid id)
    {
        return _entries.Single(x => x.Event.Id == id);
    }
}

/// <summary>
/// 测试桩：支持事件盒的事件总线，仅记录被回灌的事件
/// </summary>
public sealed class RecordingEventBoxesBus : ISupportsEventBoxes
{
    /// <summary>
    /// 已发布的出站事件
    /// </summary>
    public List<OutgoingEventInfo> Published { get; } = [];

    /// <summary>
    /// 已处理的入站事件
    /// </summary>
    public List<IncomingEventInfo> Processed { get; } = [];

    /// <summary>
    /// 发布时使用的发件箱名称
    /// </summary>
    public List<string> OutboxNames { get; } = [];

    /// <summary>
    /// 处理时使用的收件箱名称
    /// </summary>
    public List<string> InboxNames { get; } = [];

    /// <summary>
    /// 从发件箱发布单个事件
    /// </summary>
    /// <param name="outgoingEvent">出站事件信息</param>
    /// <param name="outboxConfig">发件箱配置</param>
    /// <returns>表示异步操作的任务</returns>
    public Task PublishFromOutboxAsync(OutgoingEventInfo outgoingEvent, OutboxConfig outboxConfig)
    {
        Published.Add(outgoingEvent);
        OutboxNames.Add(outboxConfig.Name);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 从发件箱批量发布事件
    /// </summary>
    /// <param name="outgoingEvents">出站事件集合</param>
    /// <param name="outboxConfig">发件箱配置</param>
    /// <returns>表示异步操作的任务</returns>
    public Task PublishManyFromOutboxAsync(IEnumerable<OutgoingEventInfo> outgoingEvents, OutboxConfig outboxConfig)
    {
        Published.AddRange(outgoingEvents);
        OutboxNames.Add(outboxConfig.Name);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 处理收件箱中的事件
    /// </summary>
    /// <param name="incomingEvent">入站事件信息</param>
    /// <param name="inboxConfig">收件箱配置</param>
    /// <returns>表示异步操作的任务</returns>
    public Task ProcessFromInboxAsync(IncomingEventInfo incomingEvent, InboxConfig inboxConfig)
    {
        Processed.Add(incomingEvent);
        InboxNames.Add(inboxConfig.Name);
        return Task.CompletedTask;
    }
}

/// <summary>
/// 事件盒记录构造辅助
/// </summary>
public static class EventInfoFactory
{
    /// <summary>
    /// 固定创建时间，避免断言受当前时钟影响
    /// </summary>
    public static readonly DateTime FixedCreatedTime = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// 构造出站事件信息
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="payload">载荷</param>
    /// <param name="id">事件唯一标识</param>
    /// <returns>出站事件信息</returns>
    public static OutgoingEventInfo CreateOutgoing(
        string eventName = "sample.event",
        string payload = "payload",
        Guid? id = null)
    {
        return new OutgoingEventInfo(
            id ?? Guid.NewGuid(),
            eventName,
            Encoding.UTF8.GetBytes(payload),
            FixedCreatedTime);
    }

    /// <summary>
    /// 构造入站事件信息
    /// </summary>
    /// <param name="messageId">消息标识</param>
    /// <param name="eventName">事件名称</param>
    /// <param name="payload">载荷</param>
    /// <param name="id">事件唯一标识</param>
    /// <returns>入站事件信息</returns>
    public static IncomingEventInfo CreateIncoming(
        string messageId = "message-1",
        string eventName = "sample.event",
        string payload = "payload",
        Guid? id = null)
    {
        return new IncomingEventInfo(
            id ?? Guid.NewGuid(),
            messageId,
            eventName,
            Encoding.UTF8.GetBytes(payload),
            FixedCreatedTime);
    }
}
