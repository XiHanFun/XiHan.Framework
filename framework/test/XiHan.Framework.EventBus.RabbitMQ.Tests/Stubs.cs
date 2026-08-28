// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using XiHan.Framework.Core.Tracing;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.EventBus.Abstractions;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.Abstractions.Local;
using XiHan.Framework.EventBus.Attributes;
using XiHan.Framework.EventBus.Distributed;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Timing;
using XiHan.Framework.Uow;
using XiHan.Framework.Uow.Options;

namespace XiHan.Framework.EventBus.RabbitMQ.Tests;

/// <summary>
/// 测试桩：空释放器
/// </summary>
public sealed class StubDisposable : IDisposable
{
    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
    }
}

/// <summary>
/// 测试桩：租户上下文
/// </summary>
/// <remarks>
/// RabbitMQ 提供程序自身不读取租户上下文，此桩仅为满足事件总线基类构造函数的非空校验。
/// </remarks>
public sealed class StubCurrentTenant : ICurrentTenant
{
    /// <summary>
    /// 当前租户是否可用，测试桩恒为 false
    /// </summary>
    public bool IsAvailable => false;

    /// <summary>
    /// 当前租户唯一标识，测试桩恒为空
    /// </summary>
    public long? Id => null;

    /// <summary>
    /// 当前租户名称，测试桩恒为空
    /// </summary>
    public string? Name => null;

    /// <summary>
    /// 临时切换租户上下文，测试桩不做任何切换
    /// </summary>
    /// <param name="id">租户唯一标识</param>
    /// <param name="name">租户名称</param>
    /// <returns>空释放器</returns>
    public IDisposable Change(long? id, string? name = null)
    {
        return new StubDisposable();
    }
}

/// <summary>
/// 测试桩：工作单元管理器
/// </summary>
/// <remarks>
/// 恒无当前工作单元，使发布路径始终走「直接投递 Broker」分支，而不是缓冲到工作单元。
/// </remarks>
public sealed class StubUnitOfWorkManager : IUnitOfWorkManager
{
    /// <summary>
    /// 当前工作单元，测试桩恒为空
    /// </summary>
    public IUnitOfWork? Current => null;

    /// <summary>
    /// 开始一个新的工作单元，测试桩不实现该操作
    /// </summary>
    /// <param name="options">工作单元选项</param>
    /// <param name="requiresNew">是否要求新的工作单元</param>
    /// <returns>工作单元</returns>
    public IUnitOfWork Begin(XiHanUnitOfWorkOptions options, bool requiresNew = false)
    {
        throw new NotSupportedException();
    }

    /// <summary>
    /// 预留一个工作单元，测试桩不实现该操作
    /// </summary>
    /// <param name="reservationName">预留名称</param>
    /// <param name="requiresNew">是否要求新的工作单元</param>
    /// <returns>工作单元</returns>
    public IUnitOfWork Reserve(string reservationName, bool requiresNew = false)
    {
        throw new NotSupportedException();
    }

    /// <summary>
    /// 开始一个预留的工作单元，测试桩不实现该操作
    /// </summary>
    /// <param name="reservationName">预留名称</param>
    /// <param name="options">工作单元选项</param>
    public void BeginReserved(string reservationName, XiHanUnitOfWorkOptions options)
    {
        throw new NotSupportedException();
    }

    /// <summary>
    /// 尝试开始一个预留的工作单元，测试桩不实现该操作
    /// </summary>
    /// <param name="reservationName">预留名称</param>
    /// <param name="options">工作单元选项</param>
    /// <returns>是否成功</returns>
    public bool TryBeginReserved(string reservationName, XiHanUnitOfWorkOptions options)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// 测试桩：时钟，返回固定时间以保证断言稳定
/// </summary>
public sealed class StubClock : IClock
{
    private static readonly DateTime FixedNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// 当前时间，测试桩恒为固定时间
    /// </summary>
    public DateTime Now => FixedNow;

    /// <summary>
    /// 时间类型
    /// </summary>
    public DateTimeKind Kind => DateTimeKind.Utc;

    /// <summary>
    /// 是否支持多时区
    /// </summary>
    public bool SupportsMultipleTimezone => false;

    /// <summary>
    /// 规范化时间，测试桩原样返回
    /// </summary>
    /// <param name="dateTime">时间</param>
    /// <returns>规范化后的时间</returns>
    public DateTime Normalize(DateTime dateTime)
    {
        return dateTime;
    }

    /// <summary>
    /// 转换为用户时间，测试桩原样返回
    /// </summary>
    /// <param name="utcDateTime">UTC 时间</param>
    /// <returns>用户时间</returns>
    public DateTime ConvertToUserTime(DateTime utcDateTime)
    {
        return utcDateTime;
    }

    /// <summary>
    /// 转换为用户时间，测试桩原样返回
    /// </summary>
    /// <param name="dateTimeOffset">时间偏移</param>
    /// <returns>用户时间</returns>
    public DateTimeOffset ConvertToUserTime(DateTimeOffset dateTimeOffset)
    {
        return dateTimeOffset;
    }

    /// <summary>
    /// 转换为 UTC 时间，测试桩原样返回
    /// </summary>
    /// <param name="dateTime">时间</param>
    /// <returns>UTC 时间</returns>
    public DateTime ConvertToUtc(DateTime dateTime)
    {
        return dateTime;
    }
}

/// <summary>
/// 测试桩：标识生成器，按顺序产出可预期的标识
/// </summary>
public sealed class StubGuidGenerator : IDistributedIdGenerator<Guid>
{
    private int _counter;

    /// <summary>
    /// 获取下一个唯一标识
    /// </summary>
    /// <returns>唯一标识</returns>
    public Guid NextId()
    {
        var next = Interlocked.Increment(ref _counter);
        var bytes = new byte[16];
        BitConverter.GetBytes(next).CopyTo(bytes, 0);

        return new Guid(bytes);
    }

    /// <summary>
    /// 获取下一个唯一标识(字符串形式)
    /// </summary>
    /// <returns>唯一标识字符串</returns>
    public string NextIdString()
    {
        return NextId().ToString();
    }

    /// <summary>
    /// 批量获取唯一标识
    /// </summary>
    /// <param name="count">数量</param>
    /// <returns>唯一标识数组</returns>
    public Guid[] NextIds(int count)
    {
        return [.. Enumerable.Range(0, count).Select(_ => NextId())];
    }

    /// <summary>
    /// 批量获取唯一标识(字符串形式)
    /// </summary>
    /// <param name="count">数量</param>
    /// <returns>唯一标识字符串数组</returns>
    public string[] NextIdStrings(int count)
    {
        return [.. NextIds(count).Select(id => id.ToString())];
    }

    /// <summary>
    /// 异步获取下一个唯一标识
    /// </summary>
    /// <returns>唯一标识</returns>
    public Task<Guid> NextIdAsync()
    {
        return Task.FromResult(NextId());
    }

    /// <summary>
    /// 异步获取下一个唯一标识(字符串形式)
    /// </summary>
    /// <returns>唯一标识字符串</returns>
    public Task<string> NextIdStringAsync()
    {
        return Task.FromResult(NextIdString());
    }

    /// <summary>
    /// 异步批量获取唯一标识
    /// </summary>
    /// <param name="count">数量</param>
    /// <returns>唯一标识数组</returns>
    public Task<Guid[]> NextIdsAsync(int count)
    {
        return Task.FromResult(NextIds(count));
    }

    /// <summary>
    /// 异步批量获取唯一标识(字符串形式)
    /// </summary>
    /// <param name="count">数量</param>
    /// <returns>唯一标识字符串数组</returns>
    public Task<string[]> NextIdStringsAsync(int count)
    {
        return Task.FromResult(NextIdStrings(count));
    }

    /// <summary>
    /// 从唯一标识中提取时间戳，测试桩不实现该操作
    /// </summary>
    /// <param name="id">唯一标识</param>
    /// <returns>时间戳</returns>
    public DateTime ExtractTime(Guid id)
    {
        throw new NotSupportedException();
    }

    /// <summary>
    /// 从唯一标识中提取工作机器唯一标识，测试桩不实现该操作
    /// </summary>
    /// <param name="id">唯一标识</param>
    /// <returns>工作机器唯一标识</returns>
    public int ExtractWorkerId(Guid id)
    {
        throw new NotSupportedException();
    }

    /// <summary>
    /// 从唯一标识中提取序列号，测试桩不实现该操作
    /// </summary>
    /// <param name="id">唯一标识</param>
    /// <returns>序列号</returns>
    public int ExtractSequence(Guid id)
    {
        throw new NotSupportedException();
    }

    /// <summary>
    /// 从唯一标识中提取数据中心唯一标识，测试桩不实现该操作
    /// </summary>
    /// <param name="id">唯一标识</param>
    /// <returns>数据中心唯一标识</returns>
    public int ExtractDataCenterId(Guid id)
    {
        throw new NotSupportedException();
    }

    /// <summary>
    /// 获取生成器类型
    /// </summary>
    /// <returns>生成器类型名称</returns>
    public string GetGeneratorType()
    {
        return nameof(StubGuidGenerator);
    }

    /// <summary>
    /// 获取生成器状态信息，测试桩返回空字典
    /// </summary>
    /// <returns>状态信息字典</returns>
    public Dictionary<string, object> GetStats()
    {
        return [];
    }
}

/// <summary>
/// 测试桩：关联标识提供器，记录每一次上下文切换
/// </summary>
public sealed class StubCorrelationIdProvider : ICorrelationIdProvider
{
    private readonly ConcurrentQueue<string?> _changes = new();

    /// <summary>
    /// 当前关联标识
    /// </summary>
    public string? Current { get; set; }

    /// <summary>
    /// 已记录的关联标识切换序列
    /// </summary>
    public IReadOnlyCollection<string?> Changes => _changes;

    /// <summary>
    /// 获取当前关联标识
    /// </summary>
    /// <returns>关联标识</returns>
    public string? Get()
    {
        return Current;
    }

    /// <summary>
    /// 临时切换关联标识，仅记录不真正切换
    /// </summary>
    /// <param name="correlationId">关联标识</param>
    /// <returns>空释放器</returns>
    public IDisposable Change(string? correlationId)
    {
        _changes.Enqueue(correlationId);
        return new StubDisposable();
    }
}

/// <summary>
/// 一次事件处理器调用的记录
/// </summary>
/// <param name="Handler">事件处理器</param>
/// <param name="EventData">事件数据</param>
/// <param name="EventType">事件类型</param>
public sealed record HandlerInvocation(IEventHandler Handler, object EventData, Type EventType);

/// <summary>
/// 测试替身：事件处理器调用器，记录每一次调用并可按需抛出
/// </summary>
public sealed class RecordingEventHandlerInvoker : IEventHandlerInvoker
{
    private readonly ConcurrentQueue<HandlerInvocation> _invocations = new();

    /// <summary>
    /// 已记录的调用序列
    /// </summary>
    public IReadOnlyCollection<HandlerInvocation> Invocations => _invocations;

    /// <summary>
    /// 设置后每次调用都抛出该异常，用于验证消费失败的传播路径
    /// </summary>
    public Exception? FailWith { get; set; }

    /// <summary>
    /// 调用事件处理器
    /// </summary>
    /// <param name="eventHandler">事件处理器</param>
    /// <param name="eventData">事件数据</param>
    /// <param name="eventType">事件类型</param>
    /// <returns>任务</returns>
    public Task InvokeAsync(IEventHandler eventHandler, object eventData, Type eventType)
    {
        _invocations.Enqueue(new HandlerInvocation(eventHandler, eventData, eventType));

        return FailWith is null ? Task.CompletedTask : Task.FromException(FailWith);
    }
}

/// <summary>
/// 一次订阅的记录
/// </summary>
/// <param name="EventType">事件类型</param>
/// <param name="Factory">事件处理器工厂</param>
public sealed record SubscriptionRecord(Type EventType, IEventHandlerFactory Factory);

/// <summary>
/// 一次退订的记录
/// </summary>
/// <param name="EventType">事件类型</param>
/// <param name="Target">退订目标（处理器、工厂或委托）</param>
public sealed record UnsubscriptionRecord(Type EventType, object Target);

/// <summary>
/// 测试替身：本地事件总线，记录委派过来的订阅/退订/发布并回放处理器工厂
/// </summary>
/// <remarks>
/// RabbitMQ 事件总线把订阅语义整体委派给本地事件总线，只自己维护「事件名 → 事件类型」映射，
/// 因此这里必须能同时观测委派动作与回放工厂，才能验证委派契约与消费分发路径。
/// </remarks>
public sealed class RecordingLocalEventBus : ILocalEventBus
{
    private readonly ConcurrentDictionary<Type, List<IEventHandlerFactory>> _factories = new();
    private readonly ConcurrentQueue<SubscriptionRecord> _subscriptions = new();
    private readonly ConcurrentQueue<UnsubscriptionRecord> _unsubscriptions = new();
    private readonly ConcurrentQueue<Type> _unsubscribeAllCalls = new();
    private readonly ConcurrentQueue<object> _publishedEvents = new();

    /// <summary>
    /// 所有订阅接口返回的固定句柄，用于断言调用方原样透传
    /// </summary>
    public IDisposable SubscriptionToken { get; } = new StubDisposable();

    /// <summary>
    /// 已记录的订阅序列
    /// </summary>
    public IReadOnlyCollection<SubscriptionRecord> Subscriptions => _subscriptions;

    /// <summary>
    /// 已记录的退订序列
    /// </summary>
    public IReadOnlyCollection<UnsubscriptionRecord> Unsubscriptions => _unsubscriptions;

    /// <summary>
    /// 已记录的「退订全部」调用序列
    /// </summary>
    public IReadOnlyCollection<Type> UnsubscribeAllCalls => _unsubscribeAllCalls;

    /// <summary>
    /// 已记录的本地发布序列
    /// </summary>
    public IReadOnlyCollection<object> PublishedEvents => _publishedEvents;

    /// <summary>
    /// 直接登记处理器工厂（不经过订阅接口）
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="factory">事件处理器工厂</param>
    public void RegisterFactory(Type eventType, IEventHandlerFactory factory)
    {
        var list = _factories.GetOrAdd(eventType, _ => []);
        lock (list)
        {
            list.Add(factory);
        }
    }

    /// <summary>
    /// 获取事件类型对应的处理器工厂列表
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <returns>事件类型及其处理器工厂列表</returns>
    public List<EventTypeWithEventHandlerFactories> GetEventHandlerFactories(Type eventType)
    {
        if (!_factories.TryGetValue(eventType, out var list))
        {
            return [];
        }

        lock (list)
        {
            return [new EventTypeWithEventHandlerFactories(eventType, [.. list])];
        }
    }

    /// <summary>
    /// 发布事件，仅记录
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="eventData">事件数据</param>
    /// <param name="onUnitOfWorkComplete">是否等待工作单元完成</param>
    /// <returns>任务</returns>
    public Task PublishAsync<TEvent>(TEvent eventData, bool onUnitOfWorkComplete = true)
        where TEvent : class
    {
        _publishedEvents.Enqueue(eventData);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 发布事件，仅记录
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="eventData">事件数据</param>
    /// <param name="onUnitOfWorkComplete">是否等待工作单元完成</param>
    /// <returns>任务</returns>
    public Task PublishAsync(Type eventType, object eventData, bool onUnitOfWorkComplete = true)
    {
        _publishedEvents.Enqueue(eventData);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 以处理器实例订阅，测试替身不实现该操作
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">事件处理器</param>
    /// <returns>订阅句柄</returns>
    public IDisposable Subscribe<TEvent>(ILocalEventHandler<TEvent> handler)
        where TEvent : class
    {
        throw new NotSupportedException();
    }

    /// <summary>
    /// 以委托订阅，测试替身不实现该操作
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="action">处理委托</param>
    /// <returns>订阅句柄</returns>
    public IDisposable Subscribe<TEvent>(Func<TEvent, Task> action)
        where TEvent : class
    {
        throw new NotSupportedException();
    }

    /// <summary>
    /// 以处理器类型订阅，测试替身不实现该操作
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <typeparam name="THandler">事件处理器类型</typeparam>
    /// <returns>订阅句柄</returns>
    public IDisposable Subscribe<TEvent, THandler>()
        where TEvent : class
        where THandler : IEventHandler, new()
    {
        throw new NotSupportedException();
    }

    /// <summary>
    /// 以处理器实例订阅，测试替身不实现该操作
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="handler">事件处理器</param>
    /// <returns>订阅句柄</returns>
    public IDisposable Subscribe(Type eventType, IEventHandler handler)
    {
        throw new NotSupportedException();
    }

    /// <summary>
    /// 以工厂订阅，测试替身不实现该操作
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="factory">事件处理器工厂</param>
    /// <returns>订阅句柄</returns>
    public IDisposable Subscribe<TEvent>(IEventHandlerFactory factory)
        where TEvent : class
    {
        throw new NotSupportedException();
    }

    /// <summary>
    /// 以工厂订阅，记录并同时登记为可回放的处理器工厂
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="factory">事件处理器工厂</param>
    /// <returns>固定订阅句柄</returns>
    public IDisposable Subscribe(Type eventType, IEventHandlerFactory factory)
    {
        _subscriptions.Enqueue(new SubscriptionRecord(eventType, factory));
        RegisterFactory(eventType, factory);
        return SubscriptionToken;
    }

    /// <summary>
    /// 以委托退订，仅记录
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="action">处理委托</param>
    public void Unsubscribe<TEvent>(Func<TEvent, Task> action)
        where TEvent : class
    {
        _unsubscriptions.Enqueue(new UnsubscriptionRecord(typeof(TEvent), action));
    }

    /// <summary>
    /// 以处理器实例退订，仅记录
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">事件处理器</param>
    public void Unsubscribe<TEvent>(ILocalEventHandler<TEvent> handler)
        where TEvent : class
    {
        _unsubscriptions.Enqueue(new UnsubscriptionRecord(typeof(TEvent), handler));
    }

    /// <summary>
    /// 以处理器实例退订，仅记录
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="handler">事件处理器</param>
    public void Unsubscribe(Type eventType, IEventHandler handler)
    {
        _unsubscriptions.Enqueue(new UnsubscriptionRecord(eventType, handler));
    }

    /// <summary>
    /// 以工厂退订，仅记录
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="factory">事件处理器工厂</param>
    public void Unsubscribe<TEvent>(IEventHandlerFactory factory)
        where TEvent : class
    {
        _unsubscriptions.Enqueue(new UnsubscriptionRecord(typeof(TEvent), factory));
    }

    /// <summary>
    /// 以工厂退订，仅记录
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="factory">事件处理器工厂</param>
    public void Unsubscribe(Type eventType, IEventHandlerFactory factory)
    {
        _unsubscriptions.Enqueue(new UnsubscriptionRecord(eventType, factory));
    }

    /// <summary>
    /// 退订某事件类型的全部处理器，仅记录
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    public void UnsubscribeAll<TEvent>()
        where TEvent : class
    {
        _unsubscribeAllCalls.Enqueue(typeof(TEvent));
    }

    /// <summary>
    /// 退订某事件类型的全部处理器，仅记录
    /// </summary>
    /// <param name="eventType">事件类型</param>
    public void UnsubscribeAll(Type eventType)
    {
        _unsubscribeAllCalls.Enqueue(eventType);
    }
}

/// <summary>
/// 测试替身：始终返回同一处理器实例的事件处理器工厂
/// </summary>
public sealed class StubEventHandlerFactory : IEventHandlerFactory
{
    private readonly IEventHandler _handler;
    private int _disposeCount;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="handler">被包装的事件处理器</param>
    public StubEventHandlerFactory(IEventHandler handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// 包装器被释放的次数，用于验证调用方按 using 语义释放
    /// </summary>
    public int DisposeCount => Volatile.Read(ref _disposeCount);

    /// <summary>
    /// 获取事件处理器
    /// </summary>
    /// <returns>事件处理器包装</returns>
    public IEventHandlerDisposeWrapper GetHandler()
    {
        return new EventHandlerDisposeWrapper(_handler, () => Interlocked.Increment(ref _disposeCount));
    }

    /// <summary>
    /// 判断当前工厂是否在给定列表中
    /// </summary>
    /// <param name="handlerFactories">工厂列表</param>
    /// <returns>是否存在</returns>
    public bool IsInFactories(List<IEventHandlerFactory> handlerFactories)
    {
        return handlerFactories.Contains(this);
    }
}

/// <summary>
/// 测试事件：未标注事件名，路由键退化为类型全名
/// </summary>
public class RabbitMQTestEvent
{
    /// <summary>
    /// 载荷
    /// </summary>
    public string Payload { get; set; } = string.Empty;
}

/// <summary>
/// 测试事件：显式标注事件名，路由键取标注值
/// </summary>
[EventName("xihan.tests.named-event")]
public class RabbitMQNamedTestEvent
{
    /// <summary>
    /// 标注的事件名，即投递到交换机时使用的路由键
    /// </summary>
    public const string RoutingKey = "xihan.tests.named-event";

    /// <summary>
    /// 载荷
    /// </summary>
    public string Payload { get; set; } = string.Empty;
}

/// <summary>
/// 测试事件：用于真实 Broker 往返验证
/// </summary>
public class RabbitMQRoundTripEvent
{
    /// <summary>
    /// 载荷
    /// </summary>
    public string Payload { get; set; } = string.Empty;
}

/// <summary>
/// 测试用分布式事件处理器
/// </summary>
public class RabbitMQTestEventHandler : IDistributedEventHandler<RabbitMQTestEvent>
{
    /// <summary>
    /// 处理事件，测试替身不做任何处理
    /// </summary>
    /// <param name="eventData">事件数据</param>
    /// <returns>任务</returns>
    public Task HandleEventAsync(RabbitMQTestEvent eventData)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// 测试用往返事件处理器
/// </summary>
public class RabbitMQRoundTripEventHandler : IDistributedEventHandler<RabbitMQRoundTripEvent>
{
    /// <summary>
    /// 处理事件，测试替身不做任何处理
    /// </summary>
    /// <param name="eventData">事件数据</param>
    /// <returns>任务</returns>
    public Task HandleEventAsync(RabbitMQRoundTripEvent eventData)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// 把 RabbitMQ 事件总线的受保护成员暴露给测试的最小子类
/// </summary>
/// <remarks>
/// 事件名映射、序列化与入站消息处理都是受保护成员，但它们正是「路由键推导 / 载荷编码 / 消费分发」
/// 这三条不依赖真实连接的核心逻辑，必须能在无 Broker 的环境下被直接断言。
/// </remarks>
public sealed class TestableRabbitMQEventBus : RabbitMQDistributedEventBus
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceScopeFactory">服务作用域工厂</param>
    /// <param name="distributedEventBusOptions">分布式事件总线选项</param>
    /// <param name="rabbitMqOptions">RabbitMQ 选项</param>
    /// <param name="eventHandlerInvoker">事件处理器调用器</param>
    /// <param name="localEventBus">本地事件总线</param>
    /// <param name="correlationIdProvider">关联标识提供器</param>
    public TestableRabbitMQEventBus(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<XiHanDistributedEventBusOptions> distributedEventBusOptions,
        IOptions<XiHanRabbitMQEventBusOptions> rabbitMqOptions,
        IEventHandlerInvoker eventHandlerInvoker,
        ILocalEventBus localEventBus,
        ICorrelationIdProvider correlationIdProvider)
        : base(
            serviceScopeFactory,
            new StubCurrentTenant(),
            new StubUnitOfWorkManager(),
            distributedEventBusOptions,
            rabbitMqOptions,
            new StubGuidGenerator(),
            new StubClock(),
            eventHandlerInvoker,
            localEventBus,
            correlationIdProvider,
            NullLogger<RabbitMQDistributedEventBus>.Instance)
    {
    }

    /// <summary>
    /// 事件名 → 事件类型映射，即初始化时绑定到队列的路由键集合
    /// </summary>
    public ConcurrentDictionary<string, Type> RegisteredEventTypes => EventTypes;

    /// <summary>
    /// 序列化事件数据
    /// </summary>
    /// <param name="eventData">事件数据</param>
    /// <returns>字节数组</returns>
    public byte[] SerializeForTest(object eventData)
    {
        return Serialize(eventData);
    }

    /// <summary>
    /// 处理入站消息
    /// </summary>
    /// <param name="messageId">消息标识</param>
    /// <param name="eventName">事件名</param>
    /// <param name="correlationId">关联标识</param>
    /// <param name="body">序列化的事件数据</param>
    /// <returns>任务</returns>
    public Task ProcessIncomingMessageForTestAsync(string? messageId, string eventName, string? correlationId, byte[] body)
    {
        return ProcessIncomingMessageAsync(messageId, eventName, correlationId, body);
    }

    /// <summary>
    /// 投递事件到 Broker
    /// </summary>
    /// <param name="eventName">事件名（路由键）</param>
    /// <param name="body">序列化的事件数据</param>
    /// <param name="messageId">消息标识</param>
    /// <param name="correlationId">关联标识</param>
    /// <returns>任务</returns>
    public Task PublishToBrokerForTestAsync(string eventName, byte[] body, string? messageId, string? correlationId)
    {
        return PublishToBrokerAsync(eventName, body, messageId, correlationId);
    }
}

/// <summary>
/// 事件总线测试夹具：把一组测试替身与被测事件总线绑在一起
/// </summary>
public sealed class RabbitMQEventBusTestContext : IDisposable
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="rabbitMqOptions">RabbitMQ 选项，为空则取默认值</param>
    /// <param name="distributedOptions">分布式事件总线选项，为空则取默认值（须在构造前填好处理器列表）</param>
    public RabbitMQEventBusTestContext(
        XiHanRabbitMQEventBusOptions? rabbitMqOptions = null,
        XiHanDistributedEventBusOptions? distributedOptions = null)
    {
        RabbitMqOptions = rabbitMqOptions ?? new XiHanRabbitMQEventBusOptions();
        DistributedOptions = distributedOptions ?? new XiHanDistributedEventBusOptions();
        LocalEventBus = new RecordingLocalEventBus();
        Invoker = new RecordingEventHandlerInvoker();
        CorrelationIdProvider = new StubCorrelationIdProvider();
        Provider = new ServiceCollection().BuildServiceProvider();

        Bus = new TestableRabbitMQEventBus(
            Provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(DistributedOptions),
            Options.Create(RabbitMqOptions),
            Invoker,
            LocalEventBus,
            CorrelationIdProvider);
    }

    /// <summary>
    /// RabbitMQ 选项
    /// </summary>
    public XiHanRabbitMQEventBusOptions RabbitMqOptions { get; }

    /// <summary>
    /// 分布式事件总线选项
    /// </summary>
    public XiHanDistributedEventBusOptions DistributedOptions { get; }

    /// <summary>
    /// 本地事件总线替身
    /// </summary>
    public RecordingLocalEventBus LocalEventBus { get; }

    /// <summary>
    /// 事件处理器调用器替身
    /// </summary>
    public RecordingEventHandlerInvoker Invoker { get; }

    /// <summary>
    /// 关联标识提供器替身
    /// </summary>
    public StubCorrelationIdProvider CorrelationIdProvider { get; }

    /// <summary>
    /// 承载服务作用域工厂的容器
    /// </summary>
    public ServiceProvider Provider { get; }

    /// <summary>
    /// 被测事件总线
    /// </summary>
    public TestableRabbitMQEventBus Bus { get; }

    /// <summary>
    /// 释放容器
    /// </summary>
    public void Dispose()
    {
        Provider.Dispose();
    }
}
