// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.EventBus.Abstractions;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.Abstractions.Local;
using XiHan.Framework.EventBus.Distributed;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Timing;
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

    /// <inheritdoc />
    public override Task PublishFromOutboxAsync(OutgoingEventInfo outgoingEvent, OutboxConfig outboxConfig)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task PublishManyFromOutboxAsync(IEnumerable<OutgoingEventInfo> outgoingEvents, OutboxConfig outboxConfig)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task ProcessFromInboxAsync(IncomingEventInfo incomingEvent, InboxConfig inboxConfig)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override byte[] Serialize(object eventData)
    {
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(eventData));
    }

    /// <inheritdoc />
    protected override Task PublishToEventBusAsync(Type eventType, object eventData)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override void AddToUnitOfWork(IUnitOfWork unitOfWork, UnitOfWorkEventRecord eventRecord)
    {
    }

    /// <inheritdoc />
    protected override IEnumerable<EventTypeWithEventHandlerFactories> GetHandlerFactories(Type eventType)
    {
        return [];
    }

    /// <inheritdoc />
    public override IDisposable Subscribe(Type eventType, IEventHandlerFactory factory) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Unsubscribe<TEvent>(Func<TEvent, Task> action) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Unsubscribe(Type eventType, IEventHandler handler) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Unsubscribe(Type eventType, IEventHandlerFactory factory) => throw new NotSupportedException();

    /// <inheritdoc />
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
