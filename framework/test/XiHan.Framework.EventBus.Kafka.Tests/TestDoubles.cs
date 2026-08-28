// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using XiHan.Framework.Core.Tracing;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.EventBus.Abstractions;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.Attributes;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Timing;
using XiHan.Framework.Uow;
using XiHan.Framework.Uow.Options;

namespace XiHan.Framework.EventBus.Kafka.Tests;

/// <summary>
/// 测试桩：空释放器
/// </summary>
public sealed class StubDisposable : IDisposable
{
    /// <summary>
    /// 释放资源，测试桩不做任何处理
    /// </summary>
    public void Dispose()
    {
    }
}

/// <summary>
/// 测试桩：租户上下文
/// </summary>
/// <remarks>
/// Kafka 事件总线的入站/出站路径都不读取租户上下文，此处仅为满足基类构造函数的非空校验。
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
    public IDisposable Change(long? id, string? name = null) => new StubDisposable();
}

/// <summary>
/// 测试桩：工作单元管理器
/// </summary>
/// <remarks>
/// <c>Current</c> 恒为空，令事件发布走「无工作单元」的直发分支；其余成员被调用即视为测试用法错误。
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
    /// <returns>工作单元实例</returns>
    public IUnitOfWork Begin(XiHanUnitOfWorkOptions options, bool requiresNew = false) => throw new NotSupportedException();

    /// <summary>
    /// 预留一个工作单元，测试桩不实现该操作
    /// </summary>
    /// <param name="reservationName">预留名称</param>
    /// <param name="requiresNew">是否要求新的工作单元</param>
    /// <returns>工作单元实例</returns>
    public IUnitOfWork Reserve(string reservationName, bool requiresNew = false) => throw new NotSupportedException();

    /// <summary>
    /// 开始一个预留的工作单元，测试桩不实现该操作
    /// </summary>
    /// <param name="reservationName">预留名称</param>
    /// <param name="options">工作单元选项</param>
    public void BeginReserved(string reservationName, XiHanUnitOfWorkOptions options) => throw new NotSupportedException();

    /// <summary>
    /// 尝试开始一个预留的工作单元，测试桩不实现该操作
    /// </summary>
    /// <param name="reservationName">预留名称</param>
    /// <param name="options">工作单元选项</param>
    /// <returns>是否成功开始</returns>
    public bool TryBeginReserved(string reservationName, XiHanUnitOfWorkOptions options) => throw new NotSupportedException();
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
    public DateTime Normalize(DateTime dateTime) => dateTime;

    /// <summary>
    /// 转换为用户时间，测试桩原样返回
    /// </summary>
    /// <param name="utcDateTime">UTC 时间</param>
    /// <returns>用户时间</returns>
    public DateTime ConvertToUserTime(DateTime utcDateTime) => utcDateTime;

    /// <summary>
    /// 转换为用户时间，测试桩原样返回
    /// </summary>
    /// <param name="dateTimeOffset">时间偏移</param>
    /// <returns>用户时间</returns>
    public DateTimeOffset ConvertToUserTime(DateTimeOffset dateTimeOffset) => dateTimeOffset;

    /// <summary>
    /// 转换为 UTC 时间，测试桩原样返回
    /// </summary>
    /// <param name="dateTime">时间</param>
    /// <returns>UTC 时间</returns>
    public DateTime ConvertToUtc(DateTime dateTime) => dateTime;
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
    /// <returns>生成的唯一标识</returns>
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
    /// <returns>生成的唯一标识字符串</returns>
    public string NextIdString() => NextId().ToString();

    /// <summary>
    /// 批量获取唯一标识
    /// </summary>
    /// <param name="count">需要获取的唯一标识数量</param>
    /// <returns>唯一标识数组</returns>
    public Guid[] NextIds(int count) => [.. Enumerable.Range(0, count).Select(_ => NextId())];

    /// <summary>
    /// 批量获取唯一标识(字符串形式)
    /// </summary>
    /// <param name="count">需要获取的唯一标识数量</param>
    /// <returns>唯一标识字符串数组</returns>
    public string[] NextIdStrings(int count) => [.. NextIds(count).Select(id => id.ToString())];

    /// <summary>
    /// 异步获取下一个唯一标识
    /// </summary>
    /// <returns>生成的唯一标识</returns>
    public Task<Guid> NextIdAsync() => Task.FromResult(NextId());

    /// <summary>
    /// 异步获取下一个唯一标识(字符串形式)
    /// </summary>
    /// <returns>生成的唯一标识字符串</returns>
    public Task<string> NextIdStringAsync() => Task.FromResult(NextIdString());

    /// <summary>
    /// 异步批量获取唯一标识
    /// </summary>
    /// <param name="count">需要获取的唯一标识数量</param>
    /// <returns>唯一标识数组</returns>
    public Task<Guid[]> NextIdsAsync(int count) => Task.FromResult(NextIds(count));

    /// <summary>
    /// 异步批量获取唯一标识(字符串形式)
    /// </summary>
    /// <param name="count">需要获取的唯一标识数量</param>
    /// <returns>唯一标识字符串数组</returns>
    public Task<string[]> NextIdStringsAsync(int count) => Task.FromResult(NextIdStrings(count));

    /// <summary>
    /// 从唯一标识中提取时间戳，测试桩不实现该操作
    /// </summary>
    /// <param name="id">唯一标识</param>
    /// <returns>时间戳</returns>
    public DateTime ExtractTime(Guid id) => throw new NotSupportedException();

    /// <summary>
    /// 从唯一标识中提取工作机器唯一标识，测试桩不实现该操作
    /// </summary>
    /// <param name="id">唯一标识</param>
    /// <returns>工作机器唯一标识</returns>
    public int ExtractWorkerId(Guid id) => throw new NotSupportedException();

    /// <summary>
    /// 从唯一标识中提取序列号，测试桩不实现该操作
    /// </summary>
    /// <param name="id">唯一标识</param>
    /// <returns>序列号</returns>
    public int ExtractSequence(Guid id) => throw new NotSupportedException();

    /// <summary>
    /// 从唯一标识中提取数据中心唯一标识，测试桩不实现该操作
    /// </summary>
    /// <param name="id">唯一标识</param>
    /// <returns>数据中心唯一标识</returns>
    public int ExtractDataCenterId(Guid id) => throw new NotSupportedException();

    /// <summary>
    /// 获取生成器类型
    /// </summary>
    /// <returns>生成器类型名称</returns>
    public string GetGeneratorType() => nameof(StubGuidGenerator);

    /// <summary>
    /// 获取生成器状态信息，测试桩返回空字典
    /// </summary>
    /// <returns>状态信息字典</returns>
    public Dictionary<string, object> GetStats() => [];
}

/// <summary>
/// 测试桩：关联标识提供器
/// </summary>
/// <remarks>
/// Kafka 消费入口会用消息头里的关联标识临时切换上下文，这里记录最后一次切换的值以便断言。
/// </remarks>
public sealed class StubCorrelationIdProvider : ICorrelationIdProvider
{
    /// <summary>
    /// 当前关联标识
    /// </summary>
    public string? Current { get; set; }

    /// <summary>
    /// 最后一次被要求切换到的关联标识
    /// </summary>
    public string? LastChangedTo { get; private set; }

    /// <summary>
    /// 获取当前关联标识
    /// </summary>
    /// <returns>当前关联标识</returns>
    public string? Get() => Current;

    /// <summary>
    /// 临时切换关联标识，测试桩只记录不恢复
    /// </summary>
    /// <param name="correlationId">关联标识</param>
    /// <returns>空释放器</returns>
    public IDisposable Change(string? correlationId)
    {
        LastChangedTo = correlationId;
        return new StubDisposable();
    }
}

/// <summary>
/// 一次事件处理器调用的记录
/// </summary>
/// <param name="Handler">被调用的处理器实例</param>
/// <param name="EventData">传入的事件数据</param>
/// <param name="EventType">传入的事件类型</param>
public sealed record HandlerInvocation(IEventHandler Handler, object EventData, Type EventType);

/// <summary>
/// 测试桩：记录每一次处理器调用的调用器
/// </summary>
/// <remarks>
/// 事件总线把「解析事件类型 → 反序列化 → 选出处理器」的结果全部汇聚到这一步，
/// 因此在这里取样即可断言入站链路的最终结果，而不必依赖真实处理器的副作用。
/// </remarks>
public sealed class RecordingEventHandlerInvoker : IEventHandlerInvoker
{
    private readonly ConcurrentQueue<HandlerInvocation> _invocations = new();

    /// <summary>
    /// 调用时的附加行为，用于制造处理失败等异常路径
    /// </summary>
    public Func<HandlerInvocation, Task>? OnInvoke { get; set; }

    /// <summary>
    /// 已记录的调用
    /// </summary>
    public IReadOnlyCollection<HandlerInvocation> Invocations => _invocations;

    /// <summary>
    /// 调用事件处理器
    /// </summary>
    /// <param name="eventHandler">事件处理器实例</param>
    /// <param name="eventData">事件数据</param>
    /// <param name="eventType">事件类型</param>
    /// <returns>表示异步操作的任务</returns>
    public Task InvokeAsync(IEventHandler eventHandler, object eventData, Type eventType)
    {
        var invocation = new HandlerInvocation(eventHandler, eventData, eventType);
        _invocations.Enqueue(invocation);

        return OnInvoke is null ? Task.CompletedTask : OnInvoke(invocation);
    }
}

/// <summary>
/// 测试事件
/// </summary>
/// <remarks>
/// 显式标注事件名，用来验证「Kafka 消息 Key 取自事件名而非类型全名」这一路由约定。
/// </remarks>
[EventName("kafka.test.event")]
public class KafkaTestEvent
{
    /// <summary>
    /// 载荷
    /// </summary>
    public string Payload { get; set; } = string.Empty;
}

/// <summary>
/// 测试用分布式事件处理器
/// </summary>
public sealed class KafkaTestEventHandler : IDistributedEventHandler<KafkaTestEvent>
{
    private readonly ConcurrentQueue<KafkaTestEvent> _handled = new();

    /// <summary>
    /// 已处理的事件
    /// </summary>
    public IReadOnlyCollection<KafkaTestEvent> Handled => _handled;

    /// <summary>
    /// 处理事件
    /// </summary>
    /// <param name="eventData">事件数据</param>
    /// <returns>表示异步操作的任务</returns>
    public Task HandleEventAsync(KafkaTestEvent eventData)
    {
        _handled.Enqueue(eventData);
        return Task.CompletedTask;
    }
}
