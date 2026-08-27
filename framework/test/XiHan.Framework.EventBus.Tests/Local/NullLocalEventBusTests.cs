// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.EventBus.Abstractions;
using XiHan.Framework.EventBus.Abstractions.Local;
using XiHan.Framework.EventBus.Local;
using XiHan.Framework.EventBus.Tests.Fakes;
using XiHan.Framework.Utils.Threading;

namespace XiHan.Framework.EventBus.Tests.Local;

/// <summary>
/// 空本地事件总线测试
/// </summary>
/// <remarks>
/// 空实现是「未装配事件总线时的安全默认值」，契约是：任何调用都不抛异常、订阅返回可安全释放的空释放器、
/// 且不产生任何投递。用它替换真实总线时上层代码不需要判空。
/// </remarks>
public class NullLocalEventBusTests
{
    /// <summary>
    /// 单例入口每次返回同一个实例
    /// </summary>
    [Fact]
    public void Instance_IsSingleton()
    {
        Assert.Same(NullLocalEventBus.Instance, NullLocalEventBus.Instance);
    }

    /// <summary>
    /// 空实现满足本地事件总线契约
    /// </summary>
    [Fact]
    public void Instance_ImplementsLocalEventBusContract()
    {
        Assert.IsAssignableFrom<ILocalEventBus>(NullLocalEventBus.Instance);
    }

    /// <summary>
    /// 各种订阅重载都返回空释放器
    /// </summary>
    [Fact]
    public void Subscribe_AllOverloads_ReturnNullDisposable()
    {
        var bus = NullLocalEventBus.Instance;
        var handler = new RecordingLocalHandler<PlainNoticeEvent>();
        var factory = new SingleInstanceHandlerFactory(handler);

        Assert.Same(NullDisposable.Instance, bus.Subscribe<PlainNoticeEvent>(_ => Task.CompletedTask));
        Assert.Same(NullDisposable.Instance, bus.Subscribe<PlainNoticeEvent>(handler));
        Assert.Same(NullDisposable.Instance, bus.Subscribe<PlainNoticeEvent, ParameterlessLocalHandler>());
        Assert.Same(NullDisposable.Instance, bus.Subscribe(typeof(PlainNoticeEvent), (IEventHandler)handler));
        Assert.Same(NullDisposable.Instance, bus.Subscribe<PlainNoticeEvent>(factory));
        Assert.Same(NullDisposable.Instance, bus.Subscribe(typeof(PlainNoticeEvent), factory));
    }

    /// <summary>
    /// 订阅后仍然查不到任何处理器工厂
    /// </summary>
    [Fact]
    public void GetEventHandlerFactories_AfterSubscribe_StaysEmpty()
    {
        var bus = NullLocalEventBus.Instance;
        bus.Subscribe<PlainNoticeEvent>(new RecordingLocalHandler<PlainNoticeEvent>());

        Assert.Empty(bus.GetEventHandlerFactories(typeof(PlainNoticeEvent)));
    }

    /// <summary>
    /// 发布事件不会触达任何处理器且正常完成
    /// </summary>
    [Fact]
    public async Task PublishAsync_DoesNotDeliverAndCompletes()
    {
        var bus = NullLocalEventBus.Instance;
        var handler = new RecordingLocalHandler<PlainNoticeEvent>();
        bus.Subscribe<PlainNoticeEvent>(handler);

        await bus.PublishAsync(new PlainNoticeEvent());
        await bus.PublishAsync(typeof(PlainNoticeEvent), new PlainNoticeEvent(), false);

        Assert.Empty(handler.Received);
    }

    /// <summary>
    /// 各种退订重载都不抛异常
    /// </summary>
    [Fact]
    public void Unsubscribe_AllOverloads_DoNotThrow()
    {
        var bus = NullLocalEventBus.Instance;
        var handler = new RecordingLocalHandler<PlainNoticeEvent>();
        var factory = new SingleInstanceHandlerFactory(handler);

        bus.Unsubscribe<PlainNoticeEvent>(_ => Task.CompletedTask);
        bus.Unsubscribe<PlainNoticeEvent>((ILocalEventHandler<PlainNoticeEvent>)handler);
        bus.Unsubscribe(typeof(PlainNoticeEvent), (IEventHandler)handler);
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
        var subscription = NullLocalEventBus.Instance.Subscribe<PlainNoticeEvent>(_ => Task.CompletedTask);

        subscription.Dispose();
        subscription.Dispose();
    }
}
