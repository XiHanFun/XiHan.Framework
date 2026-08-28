// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using XiHan.Framework.EventBus.Abstractions;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.Distributed;
using XiHan.Framework.Uow;

namespace XiHan.Framework.EventBus.Tests;

/// <summary>
/// 收件箱幂等语义的测试
/// </summary>
/// <remarks>
/// 覆盖 <c>AddToInboxAsync</c> 的返回契约：调用方按「返回 true 即已由收件箱接管、不再内联处理」解读，
/// 因此检出重复消息时必须返回 true，否则重复消息会被立刻再处理一遍，与幂等目的相反。
/// </remarks>
public class InboxIdempotencyTests
{
    /// <summary>
    /// 首次投递被收件箱接管
    /// </summary>
    [Fact]
    public async Task AddToInbox_FirstDelivery_IsHandled()
    {
        var bus = CreateBus(withInbox: true);

        Assert.True(await bus.AddToInboxForTestAsync("message-1"));
    }

    /// <summary>
    /// 重复投递同样被收件箱接管，不落到内联处理分支
    /// </summary>
    [Fact]
    public async Task AddToInbox_DuplicateDelivery_IsStillHandled()
    {
        var bus = CreateBus(withInbox: true);
        await bus.AddToInboxForTestAsync("message-1");

        Assert.True(await bus.AddToInboxForTestAsync("message-1"));
    }

    /// <summary>
    /// 重复投递不会二次入队
    /// </summary>
    [Fact]
    public async Task AddToInbox_DuplicateDelivery_IsNotEnqueuedTwice()
    {
        var bus = CreateBus(withInbox: true);
        await bus.AddToInboxForTestAsync("message-1");
        await bus.AddToInboxForTestAsync("message-1");

        var waiting = await bus.Inbox.GetWaitingEventsAsync(10);
        Assert.Single(waiting);
    }

    /// <summary>
    /// 不同消息各自入队
    /// </summary>
    [Fact]
    public async Task AddToInbox_DistinctMessages_AreAllEnqueued()
    {
        var bus = CreateBus(withInbox: true);
        await bus.AddToInboxForTestAsync("message-1");
        await bus.AddToInboxForTestAsync("message-2");

        var waiting = await bus.Inbox.GetWaitingEventsAsync(10);
        Assert.Equal(2, waiting.Count);
    }

    /// <summary>
    /// 未配置收件箱时交回调用方内联处理
    /// </summary>
    [Fact]
    public async Task AddToInbox_WithoutInbox_IsNotHandled()
    {
        var bus = CreateBus(withInbox: false);

        Assert.False(await bus.AddToInboxForTestAsync("message-1"));
    }

    /// <summary>
    /// 构建测试用的事件总线
    /// </summary>
    /// <param name="withInbox">是否配置收件箱</param>
    /// <returns>事件总线</returns>
    private static TestDistributedEventBus CreateBus(bool withInbox)
    {
        var inbox = new InMemoryEventInbox();
        var services = new ServiceCollection();
        services.AddSingleton(inbox);
        var provider = services.BuildServiceProvider();

        var options = new XiHanDistributedEventBusOptions();
        if (withInbox)
        {
            options.Inboxes.Configure(config => config.ImplementationType = typeof(InMemoryEventInbox));
        }

        return new TestDistributedEventBus(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(options),
            inbox);
    }
}

/// <summary>
/// 只暴露收件箱入口的测试用分布式事件总线
/// </summary>
public sealed class TestDistributedEventBus : DistributedEventBusBase
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceScopeFactory">服务作用域工厂</param>
    /// <param name="options">分布式事件总线选项</param>
    /// <param name="inbox">收件箱</param>
    public TestDistributedEventBus(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<XiHanDistributedEventBusOptions> options,
        InMemoryEventInbox inbox)
        : base(
            serviceScopeFactory,
            new StubCurrentTenant(),
            new StubUnitOfWorkManager(),
            options,
            new StubGuidGenerator(),
            new StubClock(),
            new StubEventHandlerInvoker(),
            null!,
            null!)
    {
        Inbox = inbox;
    }

    /// <summary>
    /// 收件箱
    /// </summary>
    public InMemoryEventInbox Inbox { get; }

    /// <summary>
    /// 以指定消息标识写入收件箱
    /// </summary>
    /// <param name="messageId">消息标识</param>
    /// <returns>是否已由收件箱接管</returns>
    public Task<bool> AddToInboxForTestAsync(string messageId)
    {
        return AddToInboxAsync(messageId, "test.event", typeof(TestEvent), new TestEvent(), null);
    }

    /// <summary>
    /// 从出站事件盒发布事件，测试桩不做任何处理
    /// </summary>
    /// <param name="outgoingEvent">出站事件信息</param>
    /// <param name="outboxConfig">出站配置</param>
    /// <returns>表示异步操作的任务</returns>
    public override Task PublishFromOutboxAsync(OutgoingEventInfo outgoingEvent, OutboxConfig outboxConfig)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 从出站事件盒批量发布事件，测试桩不做任何处理
    /// </summary>
    /// <param name="outgoingEvents">出站事件信息列表</param>
    /// <param name="outboxConfig">出站配置</param>
    /// <returns>表示异步操作的任务</returns>
    public override Task PublishManyFromOutboxAsync(IEnumerable<OutgoingEventInfo> outgoingEvents, OutboxConfig outboxConfig)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 处理从入站事件盒接收到的事件，测试桩不做任何处理
    /// </summary>
    /// <param name="incomingEvent">入站事件信息</param>
    /// <param name="inboxConfig">入站配置</param>
    /// <returns>表示异步操作的任务</returns>
    public override Task ProcessFromInboxAsync(IncomingEventInfo incomingEvent, InboxConfig inboxConfig)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 将事件数据序列化为 JSON 字节数组
    /// </summary>
    /// <param name="eventData">事件数据</param>
    /// <returns>序列化后的字节数组</returns>
    protected override byte[] Serialize(object eventData)
    {
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(eventData));
    }

    /// <summary>
    /// 发布事件到事件总线，测试桩不做任何处理
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="eventData">事件数据</param>
    /// <returns>表示异步操作的任务</returns>
    protected override Task PublishToEventBusAsync(Type eventType, object eventData)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 将事件添加到工作单元，测试桩不做任何处理
    /// </summary>
    /// <param name="unitOfWork">工作单元实例</param>
    /// <param name="eventRecord">事件记录</param>
    protected override void AddToUnitOfWork(IUnitOfWork unitOfWork, UnitOfWorkEventRecord eventRecord)
    {
    }

    /// <summary>
    /// 获取事件类型对应的处理器工厂集合，测试桩返回空集合
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <returns>事件处理器工厂集合</returns>
    protected override IEnumerable<EventTypeWithEventHandlerFactories> GetHandlerFactories(Type eventType)
    {
        return [];
    }

    /// <summary>
    /// 订阅指定类型的事件，测试桩不实现该操作
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="factory">事件处理器工厂</param>
    /// <returns>用于取消订阅的释放器</returns>
    public override IDisposable Subscribe(Type eventType, IEventHandlerFactory factory) => throw new NotSupportedException();

    /// <summary>
    /// 取消委托方法的事件订阅，测试桩不实现该操作
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="action">要取消订阅的委托方法</param>
    public override void Unsubscribe<TEvent>(Func<TEvent, Task> action) => throw new NotSupportedException();

    /// <summary>
    /// 取消事件处理器的订阅，测试桩不实现该操作
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="handler">要取消订阅的事件处理器</param>
    public override void Unsubscribe(Type eventType, IEventHandler handler) => throw new NotSupportedException();

    /// <summary>
    /// 取消工厂对象的事件订阅，测试桩不实现该操作
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="factory">要取消订阅的事件处理器工厂</param>
    public override void Unsubscribe(Type eventType, IEventHandlerFactory factory) => throw new NotSupportedException();

    /// <summary>
    /// 取消指定事件类型的所有订阅，测试桩不实现该操作
    /// </summary>
    /// <param name="eventType">事件类型</param>
    public override void UnsubscribeAll(Type eventType) => throw new NotSupportedException();
}

/// <summary>
/// 测试事件
/// </summary>
public class TestEvent
{
    /// <summary>
    /// 载荷
    /// </summary>
    public string Payload { get; set; } = "payload";
}
