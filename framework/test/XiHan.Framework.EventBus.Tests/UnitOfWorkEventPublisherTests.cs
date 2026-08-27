// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.EventBus.Distributed;
using XiHan.Framework.EventBus.Tests.Fakes;
using XiHan.Framework.Uow;

namespace XiHan.Framework.EventBus.Tests;

/// <summary>
/// 工作单元事件发布者测试
/// </summary>
/// <remarks>
/// 该发布者在工作单元提交后被回调，用来把缓冲的事件真正发出去。
/// 关键契约是「必须显式要求立即发布」：否则事件会被重新缓冲回同一个工作单元，形成永远发不出去的死循环。
/// </remarks>
public class UnitOfWorkEventPublisherTests
{
    /// <summary>
    /// 即使工作单元仍在，本地事件也立即投递而不是再次缓冲
    /// </summary>
    [Fact]
    public async Task PublishLocalEventsAsync_InsideUnitOfWork_DeliversImmediately()
    {
        using var harness = LocalDistributedEventBusHarness.Create();
        var handler = new RecordingLocalHandler<PlainNoticeEvent>();
        harness.LocalBus.Subscribe(typeof(PlainNoticeEvent), handler);
        var unitOfWork = harness.StartUnitOfWork();
        var publisher = new UnitOfWorkEventPublisher(harness.LocalBus, harness.Bus);
        var eventData = new PlainNoticeEvent { Message = "提交后发布" };

        await publisher.PublishLocalEventsAsync([new UnitOfWorkEventRecord(typeof(PlainNoticeEvent), eventData, 1)]);

        Assert.Same(eventData, Assert.Single(handler.Received));
        Assert.Empty(unitOfWork.LocalEvents);
    }

    /// <summary>
    /// 按记录顺序逐条发布本地事件
    /// </summary>
    [Fact]
    public async Task PublishLocalEventsAsync_PublishesEveryRecordInOrder()
    {
        using var harness = LocalDistributedEventBusHarness.Create();
        var handler = new RecordingLocalHandler<PlainNoticeEvent>();
        harness.LocalBus.Subscribe(typeof(PlainNoticeEvent), handler);
        var publisher = new UnitOfWorkEventPublisher(harness.LocalBus, harness.Bus);

        await publisher.PublishLocalEventsAsync(
        [
            new UnitOfWorkEventRecord(typeof(PlainNoticeEvent), new PlainNoticeEvent { Message = "第一条" }, 1),
            new UnitOfWorkEventRecord(typeof(PlainNoticeEvent), new PlainNoticeEvent { Message = "第二条" }, 2)
        ]);

        Assert.Equal(new[] { "第一条", "第二条" }, handler.Received.Select(item => item.Message));
    }

    /// <summary>
    /// 没有待发布记录时什么都不做
    /// </summary>
    [Fact]
    public async Task PublishLocalEventsAsync_WithEmptySequence_DoesNothing()
    {
        using var harness = LocalDistributedEventBusHarness.Create();
        var handler = new RecordingLocalHandler<PlainNoticeEvent>();
        harness.LocalBus.Subscribe(typeof(PlainNoticeEvent), handler);
        var publisher = new UnitOfWorkEventPublisher(harness.LocalBus, harness.Bus);

        await publisher.PublishLocalEventsAsync([]);

        Assert.Empty(handler.Received);
    }

    /// <summary>
    /// 记录声明走发件箱时事件进入发件箱而不是直接投递
    /// </summary>
    [Fact]
    public async Task PublishDistributedEventsAsync_WhenRecordUsesOutbox_EnqueuesToOutbox()
    {
        using var harness = LocalDistributedEventBusHarness.Create(
            configureOptions: options => options.Outboxes.Configure(config => config.ImplementationType = typeof(InMemoryEventOutbox)));
        var handler = new RecordingDistributedHandler<NamedNoticeEvent>();
        harness.Bus.Subscribe<NamedNoticeEvent>(handler);
        var unitOfWork = harness.StartUnitOfWork();
        var publisher = new UnitOfWorkEventPublisher(harness.LocalBus, harness.Bus);

        await publisher.PublishDistributedEventsAsync(
            [new UnitOfWorkEventRecord(typeof(NamedNoticeEvent), new NamedNoticeEvent(), 1, useOutbox: true)]);

        Assert.Empty(handler.Received);
        Assert.Empty(unitOfWork.DistributedEvents);
        Assert.Single(await harness.GetOutbox().GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 记录声明不走发件箱时事件直接投递
    /// </summary>
    [Fact]
    public async Task PublishDistributedEventsAsync_WhenRecordSkipsOutbox_DeliversDirectly()
    {
        using var harness = LocalDistributedEventBusHarness.Create(
            configureOptions: options => options.Outboxes.Configure(config => config.ImplementationType = typeof(InMemoryEventOutbox)));
        var handler = new RecordingDistributedHandler<NamedNoticeEvent>();
        harness.Bus.Subscribe<NamedNoticeEvent>(handler);
        harness.StartUnitOfWork();
        var publisher = new UnitOfWorkEventPublisher(harness.LocalBus, harness.Bus);

        await publisher.PublishDistributedEventsAsync(
            [new UnitOfWorkEventRecord(typeof(NamedNoticeEvent), new NamedNoticeEvent { Message = "直发" }, 1, useOutbox: false)]);

        Assert.Equal("直发", Assert.Single(handler.Received).Message);
        Assert.Empty(await harness.GetOutbox().GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 分布式事件同样不会被重新缓冲回工作单元
    /// </summary>
    [Fact]
    public async Task PublishDistributedEventsAsync_InsideUnitOfWork_DoesNotRebuffer()
    {
        using var harness = LocalDistributedEventBusHarness.Create();
        var unitOfWork = harness.StartUnitOfWork();
        var publisher = new UnitOfWorkEventPublisher(harness.LocalBus, harness.Bus);

        await publisher.PublishDistributedEventsAsync(
            [new UnitOfWorkEventRecord(typeof(NamedNoticeEvent), new NamedNoticeEvent(), 1, useOutbox: false)]);

        Assert.Empty(unitOfWork.DistributedEvents);
    }

    /// <summary>
    /// 没有待发布记录时什么都不做
    /// </summary>
    [Fact]
    public async Task PublishDistributedEventsAsync_WithEmptySequence_DoesNothing()
    {
        using var harness = LocalDistributedEventBusHarness.Create();
        var handler = new RecordingDistributedHandler<NamedNoticeEvent>();
        harness.Bus.Subscribe<NamedNoticeEvent>(handler);
        var publisher = new UnitOfWorkEventPublisher(harness.LocalBus, harness.Bus);

        await publisher.PublishDistributedEventsAsync([]);

        Assert.Empty(handler.Received);
    }
}
