// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.EventBus.Tests.Fakes;

namespace XiHan.Framework.EventBus.Tests;

/// <summary>
/// 事件处理器方法执行器测试
/// </summary>
/// <remarks>
/// 执行器把「处理器对象 + 事件对象」这对弱类型入参强转回具体接口再调用，
/// 是本地通道与分布式通道分流的落点，两条通道必须各调各的接口方法。
/// </remarks>
public class EventHandlerMethodExecutorTests
{
    /// <summary>
    /// 本地执行器调用本地处理器接口
    /// </summary>
    [Fact]
    public async Task LocalExecutor_ExecutorAsync_InvokesLocalChannel()
    {
        var executor = new LocalEventHandlerMethodExecutor<PlainNoticeEvent>();
        var handler = new DualChannelHandler();

        await executor.ExecutorAsync(handler, new PlainNoticeEvent());

        Assert.Equal(1, handler.LocalCallCount);
        Assert.Equal(0, handler.DistributedCallCount);
    }

    /// <summary>
    /// 本地执行器的强类型入口与弱类型入口行为一致
    /// </summary>
    [Fact]
    public async Task LocalExecutor_ExecuteAsync_InvokesLocalChannel()
    {
        var executor = new LocalEventHandlerMethodExecutor<PlainNoticeEvent>();
        var handler = new RecordingLocalHandler<PlainNoticeEvent>();
        var eventData = new PlainNoticeEvent { Message = "强类型入口" };

        await executor.ExecuteAsync(handler, eventData);

        Assert.Same(eventData, Assert.Single(handler.Received));
    }

    /// <summary>
    /// 分布式执行器调用分布式处理器接口
    /// </summary>
    [Fact]
    public async Task DistributedExecutor_ExecutorAsync_InvokesDistributedChannel()
    {
        var executor = new DistributedEventHandlerMethodExecutor<PlainNoticeEvent>();
        var handler = new DualChannelHandler();

        await executor.ExecutorAsync(handler, new PlainNoticeEvent());

        Assert.Equal(1, handler.DistributedCallCount);
        Assert.Equal(0, handler.LocalCallCount);
    }

    /// <summary>
    /// 分布式执行器的强类型入口与弱类型入口行为一致
    /// </summary>
    [Fact]
    public async Task DistributedExecutor_ExecuteAsync_InvokesDistributedChannel()
    {
        var executor = new DistributedEventHandlerMethodExecutor<NamedNoticeEvent>();
        var handler = new RecordingDistributedHandler<NamedNoticeEvent>();
        var eventData = new NamedNoticeEvent { Message = "强类型入口" };

        await executor.ExecuteAsync(handler, eventData);

        Assert.Same(eventData, Assert.Single(handler.Received));
    }

    /// <summary>
    /// 处理器类型与执行器不匹配时强转失败
    /// </summary>
    [Fact]
    public async Task LocalExecutor_WithIncompatibleHandler_ThrowsInvalidCast()
    {
        var executor = new LocalEventHandlerMethodExecutor<PlainNoticeEvent>();

        // ExecutorAsync 返回 Task，类型转换失败发生在返回的任务里而不是调用瞬间，
        // 必须用 ThrowsAsync 等待任务完成才能捕获（xUnit2014 也会把同步版本判为错误）。
        await Assert.ThrowsAsync<InvalidCastException>(
            () => executor.ExecutorAsync(new MarkerOnlyHandler(), new PlainNoticeEvent()));
    }

    /// <summary>
    /// 分布式执行器同样拒绝不匹配的处理器
    /// </summary>
    [Fact]
    public async Task DistributedExecutor_WithIncompatibleHandler_ThrowsInvalidCast()
    {
        var executor = new DistributedEventHandlerMethodExecutor<PlainNoticeEvent>();

        // ExecutorAsync 返回 Task，类型转换失败发生在返回的任务里而不是调用瞬间，
        // 必须用 ThrowsAsync 等待任务完成才能捕获（xUnit2014 也会把同步版本判为错误）。
        await Assert.ThrowsAsync<InvalidCastException>(
            () => executor.ExecutorAsync(new MarkerOnlyHandler(), new PlainNoticeEvent()));
    }

    /// <summary>
    /// 两种执行器都实现统一的执行器契约
    /// </summary>
    [Fact]
    public void BothExecutors_ImplementExecutorContract()
    {
        Assert.IsAssignableFrom<IEventHandlerMethodExecutor>(new LocalEventHandlerMethodExecutor<PlainNoticeEvent>());
        Assert.IsAssignableFrom<IEventHandlerMethodExecutor>(new DistributedEventHandlerMethodExecutor<PlainNoticeEvent>());
    }

    /// <summary>
    /// 调用缓存项默认两条通道均为空
    /// </summary>
    [Fact]
    public void CacheItem_HasNoExecutorByDefault()
    {
        var cacheItem = new EventHandlerInvokerCacheItem();

        Assert.Null(cacheItem.Local);
        Assert.Null(cacheItem.Distributed);
    }
}
