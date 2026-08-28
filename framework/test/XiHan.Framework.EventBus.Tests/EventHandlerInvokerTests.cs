// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.EventBus.Tests.Fakes;

namespace XiHan.Framework.EventBus.Tests;

/// <summary>
/// 事件处理器调用器测试
/// </summary>
/// <remarks>
/// 调用器负责按处理器实现的接口挑选执行通道并缓存反射结果，
/// 覆盖重点是「挑对通道」「两条通道都实现时都要走」「不是处理器要报错」。
/// </remarks>
public class EventHandlerInvokerTests
{
    /// <summary>
    /// 本地处理器走本地通道
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithLocalHandler_InvokesLocalChannel()
    {
        var invoker = new EventHandlerInvoker();
        var handler = new RecordingLocalHandler<PlainNoticeEvent>();
        var eventData = new PlainNoticeEvent { Message = "本地" };

        await invoker.InvokeAsync(handler, eventData, typeof(PlainNoticeEvent));

        Assert.Same(eventData, Assert.Single(handler.Received));
    }

    /// <summary>
    /// 分布式处理器走分布式通道
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithDistributedHandler_InvokesDistributedChannel()
    {
        var invoker = new EventHandlerInvoker();
        var handler = new RecordingDistributedHandler<NamedNoticeEvent>();
        var eventData = new NamedNoticeEvent { Message = "分布式" };

        await invoker.InvokeAsync(handler, eventData, typeof(NamedNoticeEvent));

        Assert.Same(eventData, Assert.Single(handler.Received));
    }

    /// <summary>
    /// 同时实现两种处理器接口时两条通道都会被调用
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithDualChannelHandler_InvokesBothChannels()
    {
        var invoker = new EventHandlerInvoker();
        var handler = new DualChannelHandler();

        await invoker.InvokeAsync(handler, new PlainNoticeEvent(), typeof(PlainNoticeEvent));

        Assert.Equal(1, handler.LocalCallCount);
        Assert.Equal(1, handler.DistributedCallCount);
    }

    /// <summary>
    /// 缓存执行器后重复调用仍然每次都真正执行
    /// </summary>
    [Fact]
    public async Task InvokeAsync_CalledRepeatedly_ExecutesEveryTime()
    {
        var invoker = new EventHandlerInvoker();
        var handler = new RecordingLocalHandler<PlainNoticeEvent>();

        await invoker.InvokeAsync(handler, new PlainNoticeEvent(), typeof(PlainNoticeEvent));
        await invoker.InvokeAsync(handler, new PlainNoticeEvent(), typeof(PlainNoticeEvent));
        await invoker.InvokeAsync(handler, new PlainNoticeEvent(), typeof(PlainNoticeEvent));

        Assert.Equal(3, handler.Received.Count);
    }

    /// <summary>
    /// 同一个调用器可服务多个事件类型，缓存互不串扰
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ForDifferentEventTypes_KeepsChannelsSeparate()
    {
        var invoker = new EventHandlerInvoker();
        var localHandler = new RecordingLocalHandler<PlainNoticeEvent>();
        var distributedHandler = new RecordingDistributedHandler<NamedNoticeEvent>();

        await invoker.InvokeAsync(localHandler, new PlainNoticeEvent(), typeof(PlainNoticeEvent));
        await invoker.InvokeAsync(distributedHandler, new NamedNoticeEvent(), typeof(NamedNoticeEvent));

        Assert.Single(localHandler.Received);
        Assert.Single(distributedHandler.Received);
    }

    /// <summary>
    /// 传入派生事件实例、按基类事件类型调用时仍能正常投递
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithDerivedEventData_UsesDeclaredEventType()
    {
        var invoker = new EventHandlerInvoker();
        var handler = new RecordingLocalHandler<PlainNoticeEvent>();
        var eventData = new DerivedNoticeEvent { Message = "派生" };

        await invoker.InvokeAsync(handler, eventData, typeof(PlainNoticeEvent));

        Assert.Same(eventData, Assert.Single(handler.Received));
    }

    /// <summary>
    /// 处理器抛出的异常原样冒泡，由调用方决定收集还是中断
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenHandlerFails_PropagatesOriginalException()
    {
        var invoker = new EventHandlerInvoker();
        var handler = new ThrowingLocalHandler<PlainNoticeEvent> { FailureMessage = "调用器不吞异常" };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => invoker.InvokeAsync(handler, new PlainNoticeEvent(), typeof(PlainNoticeEvent)));

        Assert.Equal("调用器不吞异常", exception.Message);
    }

    /// <summary>
    /// 只实现标记接口的对象不是合法处理器
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithMarkerOnlyHandler_Throws()
    {
        var invoker = new EventHandlerInvoker();

        var exception = await Assert.ThrowsAsync<XiHanException>(
            () => invoker.InvokeAsync(new MarkerOnlyHandler(), new PlainNoticeEvent(), typeof(PlainNoticeEvent)));

        Assert.Contains("不是事件处理程序", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 处理器与事件类型不匹配时不会被误判为可处理
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenHandlerDoesNotHandleEventType_Throws()
    {
        var invoker = new EventHandlerInvoker();
        var handler = new RecordingLocalHandler<PlainNoticeEvent>();

        await Assert.ThrowsAsync<XiHanException>(
            () => invoker.InvokeAsync(handler, new NamedNoticeEvent(), typeof(NamedNoticeEvent)));
    }
}
