// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.EventBus.Abstractions;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.Abstractions.Local;
using XiHan.Framework.EventBus.Distributed;
using XiHan.Framework.EventBus.Tests.Fakes;
using XiHan.Framework.Utils.Threading;

namespace XiHan.Framework.EventBus.Tests.Distributed;

/// <summary>
/// 空分布式事件总线测试
/// </summary>
/// <remarks>
/// 未接入任何消息中间件时用它兜底：所有调用静默成功，绝不能抛异常把业务流程带崩。
/// </remarks>
public class NullDistributedEventBusTests
{
    /// <summary>
    /// 单例入口每次返回同一个实例
    /// </summary>
    [Fact]
    public void Instance_IsSingleton()
    {
        Assert.Same(NullDistributedEventBus.Instance, NullDistributedEventBus.Instance);
    }

    /// <summary>
    /// 空实现满足分布式事件总线契约
    /// </summary>
    [Fact]
    public void Instance_ImplementsDistributedEventBusContract()
    {
        Assert.IsAssignableFrom<IDistributedEventBus>(NullDistributedEventBus.Instance);
    }

    /// <summary>
    /// 各种订阅重载都返回空释放器
    /// </summary>
    [Fact]
    public void Subscribe_AllOverloads_ReturnNullDisposable()
    {
        var bus = NullDistributedEventBus.Instance;
        var handler = new RecordingDistributedHandler<NamedNoticeEvent>();
        var factory = new SingleInstanceHandlerFactory(handler);

        Assert.Same(NullDisposable.Instance, bus.Subscribe<NamedNoticeEvent>(_ => Task.CompletedTask));
        Assert.Same(NullDisposable.Instance, bus.Subscribe<NamedNoticeEvent>(handler));
        Assert.Same(NullDisposable.Instance, bus.Subscribe<PlainNoticeEvent, ParameterlessLocalHandler>());
        Assert.Same(NullDisposable.Instance, bus.Subscribe(typeof(NamedNoticeEvent), (IEventHandler)handler));
        Assert.Same(NullDisposable.Instance, bus.Subscribe<NamedNoticeEvent>(factory));
        Assert.Same(NullDisposable.Instance, bus.Subscribe(typeof(NamedNoticeEvent), factory));
    }

    /// <summary>
    /// 发布事件不会触达任何处理器且正常完成
    /// </summary>
    [Fact]
    public async Task PublishAsync_DoesNotDeliverAndCompletes()
    {
        var bus = NullDistributedEventBus.Instance;
        var handler = new RecordingDistributedHandler<NamedNoticeEvent>();
        bus.Subscribe<NamedNoticeEvent>(handler);

        // 四个发布重载各调一次；均显式传满参数，避免可选参数重载在同一类型上产生歧义
        await bus.PublishAsync(new NamedNoticeEvent(), false);
        await bus.PublishAsync(typeof(NamedNoticeEvent), new NamedNoticeEvent(), false);
        await bus.PublishAsync(new NamedNoticeEvent(), false, false);
        await bus.PublishAsync(typeof(NamedNoticeEvent), new NamedNoticeEvent(), false, false);

        Assert.Empty(handler.Received);
    }

    /// <summary>
    /// 各种退订重载都不抛异常
    /// </summary>
    [Fact]
    public void Unsubscribe_AllOverloads_DoNotThrow()
    {
        var bus = NullDistributedEventBus.Instance;
        var localHandler = new RecordingLocalHandler<PlainNoticeEvent>();
        var factory = new SingleInstanceHandlerFactory(localHandler);

        bus.Unsubscribe<PlainNoticeEvent>(_ => Task.CompletedTask);
        bus.Unsubscribe<PlainNoticeEvent>((ILocalEventHandler<PlainNoticeEvent>)localHandler);
        bus.Unsubscribe(typeof(PlainNoticeEvent), (IEventHandler)localHandler);
        bus.Unsubscribe<PlainNoticeEvent>(factory);
        bus.Unsubscribe(typeof(PlainNoticeEvent), factory);
        bus.UnsubscribeAll<PlainNoticeEvent>();
        bus.UnsubscribeAll(typeof(PlainNoticeEvent));
    }

    /// <summary>
    /// 订阅返回的释放器可重复释放
    /// </summary>
    [Fact]
    public void SubscriptionHandle_CanBeDisposedRepeatedly()
    {
        var subscription = NullDistributedEventBus.Instance.Subscribe<NamedNoticeEvent>(_ => Task.CompletedTask);

        subscription.Dispose();
        subscription.Dispose();
    }
}
