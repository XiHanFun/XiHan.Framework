// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.EventBus.Abstractions;
using XiHan.Framework.EventBus.Abstractions.Local;
using XiHan.Framework.EventBus.Tests.Fakes;

namespace XiHan.Framework.EventBus.Tests.Local;

/// <summary>
/// 本地事件总线订阅与退订测试
/// </summary>
/// <remarks>
/// 订阅表用「工厂是否已存在」去重，不同工厂的判重口径不同（单实例按实例、瞬时按处理器类型），
/// 这里按工厂数量断言，避免把内部数据结构写进测试。
/// </remarks>
public class LocalEventBusSubscriptionTests
{
    /// <summary>
    /// 委托订阅会登记一个处理器工厂
    /// </summary>
    [Fact]
    public void Subscribe_WithAction_RegistersOneFactory()
    {
        using var harness = LocalEventBusHarness.Create();
        Func<PlainNoticeEvent, Task> action = _ => Task.CompletedTask;

        harness.Bus.Subscribe(action);

        Assert.Single(harness.Bus.GetEventHandlerFactories(typeof(PlainNoticeEvent)));
    }

    /// <summary>
    /// 用同一个委托退订可移除订阅
    /// </summary>
    [Fact]
    public void Unsubscribe_WithSameAction_RemovesFactory()
    {
        using var harness = LocalEventBusHarness.Create();
        Func<PlainNoticeEvent, Task> action = _ => Task.CompletedTask;
        harness.Bus.Subscribe(action);

        harness.Bus.Unsubscribe(action);

        Assert.Empty(harness.Bus.GetEventHandlerFactories(typeof(PlainNoticeEvent)));
    }

    /// <summary>
    /// 用另一个委托退订不会误删已有订阅
    /// </summary>
    [Fact]
    public void Unsubscribe_WithDifferentAction_KeepsFactory()
    {
        using var harness = LocalEventBusHarness.Create();
        Func<PlainNoticeEvent, Task> subscribed = _ => Task.CompletedTask;
        Func<PlainNoticeEvent, Task> other = _ => Task.CompletedTask;
        harness.Bus.Subscribe(subscribed);

        harness.Bus.Unsubscribe(other);

        Assert.Single(harness.Bus.GetEventHandlerFactories(typeof(PlainNoticeEvent)));
    }

    /// <summary>
    /// 同一个处理器实例重复订阅只登记一次
    /// </summary>
    [Fact]
    public void Subscribe_WithSameHandlerInstanceTwice_RegistersOnce()
    {
        using var harness = LocalEventBusHarness.Create();
        var handler = new RecordingLocalHandler<PlainNoticeEvent>();

        harness.Bus.Subscribe(typeof(PlainNoticeEvent), handler);
        harness.Bus.Subscribe(typeof(PlainNoticeEvent), handler);

        Assert.Single(harness.Bus.GetEventHandlerFactories(typeof(PlainNoticeEvent)));
    }

    /// <summary>
    /// 同类型的两个不同实例分别登记
    /// </summary>
    [Fact]
    public void Subscribe_WithTwoHandlerInstances_RegistersBoth()
    {
        using var harness = LocalEventBusHarness.Create();

        harness.Bus.Subscribe(typeof(PlainNoticeEvent), new RecordingLocalHandler<PlainNoticeEvent>());
        harness.Bus.Subscribe(typeof(PlainNoticeEvent), new RecordingLocalHandler<PlainNoticeEvent>());

        Assert.Equal(2, harness.Bus.GetEventHandlerFactories(typeof(PlainNoticeEvent)).Count);
    }

    /// <summary>
    /// 按处理器类型订阅时重复订阅只登记一次
    /// </summary>
    [Fact]
    public void SubscribeByHandlerType_CalledTwice_RegistersOnce()
    {
        using var harness = LocalEventBusHarness.Create();

        harness.Bus.Subscribe<PlainNoticeEvent, ParameterlessLocalHandler>();
        harness.Bus.Subscribe<PlainNoticeEvent, ParameterlessLocalHandler>();

        Assert.Single(harness.Bus.GetEventHandlerFactories(typeof(PlainNoticeEvent)));
    }

    /// <summary>
    /// 按事件类型与处理器实例退订可移除订阅
    /// </summary>
    [Fact]
    public void Unsubscribe_WithHandlerInstance_RemovesFactory()
    {
        using var harness = LocalEventBusHarness.Create();
        var handler = new RecordingLocalHandler<PlainNoticeEvent>();
        harness.Bus.Subscribe(typeof(PlainNoticeEvent), handler);

        harness.Bus.Unsubscribe(typeof(PlainNoticeEvent), handler);

        Assert.Empty(harness.Bus.GetEventHandlerFactories(typeof(PlainNoticeEvent)));
    }

    /// <summary>
    /// 泛型退订重载同样按处理器实例移除订阅
    /// </summary>
    [Fact]
    public void UnsubscribeOfTEvent_WithLocalHandler_RemovesFactory()
    {
        using var harness = LocalEventBusHarness.Create();
        var handler = new RecordingLocalHandler<PlainNoticeEvent>();
        harness.Bus.Subscribe<PlainNoticeEvent>(handler);

        harness.Bus.Unsubscribe<PlainNoticeEvent>(handler);

        Assert.Empty(harness.Bus.GetEventHandlerFactories(typeof(PlainNoticeEvent)));
    }

    /// <summary>
    /// 按工厂对象退订可移除订阅
    /// </summary>
    [Fact]
    public void Unsubscribe_WithFactory_RemovesFactory()
    {
        using var harness = LocalEventBusHarness.Create();
        var factory = new SingleInstanceHandlerFactory(new RecordingLocalHandler<PlainNoticeEvent>());
        harness.Bus.Subscribe<PlainNoticeEvent>(factory);

        harness.Bus.Unsubscribe<PlainNoticeEvent>(factory);

        Assert.Empty(harness.Bus.GetEventHandlerFactories(typeof(PlainNoticeEvent)));
    }

    /// <summary>
    /// 退订全部会清空该事件类型的所有订阅
    /// </summary>
    [Fact]
    public void UnsubscribeAll_ClearsEveryFactoryOfEventType()
    {
        using var harness = LocalEventBusHarness.Create();
        harness.Bus.Subscribe(typeof(PlainNoticeEvent), new RecordingLocalHandler<PlainNoticeEvent>());
        harness.Bus.Subscribe(typeof(PlainNoticeEvent), new AlternateRecordingLocalHandler<PlainNoticeEvent>());

        harness.Bus.UnsubscribeAll<PlainNoticeEvent>();

        Assert.Empty(harness.Bus.GetEventHandlerFactories(typeof(PlainNoticeEvent)));
    }

    /// <summary>
    /// 退订全部只影响指定的事件类型
    /// </summary>
    [Fact]
    public void UnsubscribeAll_DoesNotAffectOtherEventTypes()
    {
        using var harness = LocalEventBusHarness.Create();
        harness.Bus.Subscribe(typeof(PlainNoticeEvent), new RecordingLocalHandler<PlainNoticeEvent>());
        harness.Bus.Subscribe(typeof(NamedNoticeEvent), new RecordingLocalHandler<NamedNoticeEvent>());

        harness.Bus.UnsubscribeAll<PlainNoticeEvent>();

        Assert.Single(harness.Bus.GetEventHandlerFactories(typeof(NamedNoticeEvent)));
    }

    /// <summary>
    /// 订阅返回的释放器被释放后自动退订
    /// </summary>
    [Fact]
    public void SubscriptionHandle_OnDispose_Unsubscribes()
    {
        using var harness = LocalEventBusHarness.Create();
        var subscription = harness.Bus.Subscribe(typeof(PlainNoticeEvent), new RecordingLocalHandler<PlainNoticeEvent>());
        Assert.IsType<EventHandlerFactoryUnregistrar>(subscription);

        subscription.Dispose();

        Assert.Empty(harness.Bus.GetEventHandlerFactories(typeof(PlainNoticeEvent)));
    }

    /// <summary>
    /// 从未订阅过的事件类型没有任何处理器工厂
    /// </summary>
    [Fact]
    public void GetEventHandlerFactories_ForUnknownEventType_ReturnsEmpty()
    {
        using var harness = LocalEventBusHarness.Create();

        Assert.Empty(harness.Bus.GetEventHandlerFactories(typeof(PlainNoticeEvent)));
    }

    /// <summary>
    /// 查询派生事件类型时会带出基类事件的处理器工厂
    /// </summary>
    [Fact]
    public void GetEventHandlerFactories_ForDerivedEventType_IncludesBaseTypeFactories()
    {
        using var harness = LocalEventBusHarness.Create();
        harness.Bus.Subscribe(typeof(PlainNoticeEvent), new RecordingLocalHandler<PlainNoticeEvent>());

        var factories = harness.Bus.GetEventHandlerFactories(typeof(DerivedNoticeEvent));

        var entry = Assert.Single(factories);
        Assert.Equal(typeof(PlainNoticeEvent), entry.EventType);
        Assert.Single(entry.EventHandlerFactories);
    }

    /// <summary>
    /// 退订后不再收到后续发布的事件
    /// </summary>
    [Fact]
    public async Task Unsubscribe_AfterFirstPublish_StopsFurtherDelivery()
    {
        using var harness = LocalEventBusHarness.Create();
        var handler = new RecordingLocalHandler<PlainNoticeEvent>();
        harness.Bus.Subscribe(typeof(PlainNoticeEvent), handler);

        await harness.Bus.PublishAsync(new PlainNoticeEvent());
        harness.Bus.Unsubscribe(typeof(PlainNoticeEvent), handler);
        await harness.Bus.PublishAsync(new PlainNoticeEvent());

        Assert.Single(handler.Received);
    }

    /// <summary>
    /// 委托为空时订阅失败
    /// </summary>
    [Fact]
    public void Subscribe_WhenActionNull_Throws()
    {
        using var harness = LocalEventBusHarness.Create();

        Assert.Throws<ArgumentNullException>(() =>
        {
            harness.Bus.Subscribe<PlainNoticeEvent>((Func<PlainNoticeEvent, Task>)null!);
        });
    }

    /// <summary>
    /// 事件类型为空时订阅失败
    /// </summary>
    [Fact]
    public void Subscribe_WhenEventTypeNull_Throws()
    {
        using var harness = LocalEventBusHarness.Create();

        Assert.Throws<ArgumentNullException>(() =>
        {
            harness.Bus.Subscribe((Type)null!, new RecordingLocalHandler<PlainNoticeEvent>());
        });
    }

    /// <summary>
    /// 处理器为空时订阅失败
    /// </summary>
    [Fact]
    public void Subscribe_WhenHandlerNull_Throws()
    {
        using var harness = LocalEventBusHarness.Create();

        Assert.Throws<ArgumentNullException>(() =>
        {
            harness.Bus.Subscribe(typeof(PlainNoticeEvent), (IEventHandler)null!);
        });
    }

    /// <summary>
    /// 工厂为空时订阅失败
    /// </summary>
    [Fact]
    public void Subscribe_WhenFactoryNull_Throws()
    {
        using var harness = LocalEventBusHarness.Create();

        Assert.Throws<ArgumentNullException>(() =>
        {
            harness.Bus.Subscribe<PlainNoticeEvent>((IEventHandlerFactory)null!);
        });
    }

    /// <summary>
    /// 委托为空时退订失败
    /// </summary>
    [Fact]
    public void Unsubscribe_WhenActionNull_Throws()
    {
        using var harness = LocalEventBusHarness.Create();

        Assert.Throws<ArgumentNullException>(() =>
        {
            harness.Bus.Unsubscribe<PlainNoticeEvent>((Func<PlainNoticeEvent, Task>)null!);
        });
    }

    /// <summary>
    /// 处理器为空时退订失败
    /// </summary>
    [Fact]
    public void Unsubscribe_WhenHandlerNull_Throws()
    {
        using var harness = LocalEventBusHarness.Create();

        Assert.Throws<ArgumentNullException>(() =>
        {
            harness.Bus.Unsubscribe<PlainNoticeEvent>((ILocalEventHandler<PlainNoticeEvent>)null!);
        });
    }

    /// <summary>
    /// 工厂为空时退订失败
    /// </summary>
    [Fact]
    public void Unsubscribe_WhenFactoryNull_Throws()
    {
        using var harness = LocalEventBusHarness.Create();

        Assert.Throws<ArgumentNullException>(() =>
        {
            harness.Bus.Unsubscribe<PlainNoticeEvent>((IEventHandlerFactory)null!);
        });
    }
}
