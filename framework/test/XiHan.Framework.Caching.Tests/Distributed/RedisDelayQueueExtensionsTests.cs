// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Caching.Distributed;
using XiHan.Framework.Caching.Distributed.Abstracts;

namespace XiHan.Framework.Caching.Tests.Distributed;

/// <summary>
/// 延迟队列便捷扩展测试
/// </summary>
/// <remarks>
/// 用进程内回退实现驱动，覆盖批量入队、到期消费与失败重投三条语义，全程不连 Redis。
/// 消费循环用「处理器里取消令牌」的方式收尾，不依赖真实等待。
/// </remarks>
public class RedisDelayQueueExtensionsTests
{
    /// <summary>
    /// 批量入队把集合里的每条消息都放进队列
    /// </summary>
    [Fact]
    public async Task EnqueueRangeAsync_EnqueuesEveryItem()
    {
        var token = TestContext.Current.CancellationToken;
        var queue = new InMemoryDelayQueue<DelayMessage>();

        await queue.EnqueueRangeAsync([new DelayMessage(1), new DelayMessage(2), new DelayMessage(3)], TimeSpan.Zero, token);

        Assert.Equal(3, await queue.CountAsync(token));
    }

    /// <summary>
    /// 批量入队的空集合不产生任何消息
    /// </summary>
    [Fact]
    public async Task EnqueueRangeAsync_WithEmptyItems_EnqueuesNothing()
    {
        var token = TestContext.Current.CancellationToken;
        var queue = new InMemoryDelayQueue<DelayMessage>();

        await queue.EnqueueRangeAsync([], TimeSpan.Zero, token);

        Assert.Equal(0, await queue.CountAsync(token));
    }

    /// <summary>
    /// 队列为空时批量入队拒绝执行
    /// </summary>
    [Fact]
    public async Task EnqueueRangeAsync_WithNullQueue_Throws()
    {
        IRedisDelayQueue<DelayMessage>? queue = null;

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => queue!.EnqueueRangeAsync([new DelayMessage(1)], TimeSpan.Zero));
    }

    /// <summary>
    /// 消息集合为空时批量入队拒绝执行
    /// </summary>
    [Fact]
    public async Task EnqueueRangeAsync_WithNullItems_Throws()
    {
        var queue = new InMemoryDelayQueue<DelayMessage>();
        IEnumerable<DelayMessage>? items = null;

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => queue.EnqueueRangeAsync(items!, TimeSpan.Zero));
    }

    /// <summary>
    /// 到期消费逐条处理已到期消息并返回处理条数
    /// </summary>
    [Fact]
    public async Task ProcessDueAsync_HandlesDueItemsAndReturnsCount()
    {
        var token = TestContext.Current.CancellationToken;
        var queue = new InMemoryDelayQueue<DelayMessage>();
        await queue.EnqueueAsync(new DelayMessage(1), TimeSpan.Zero, token);
        await queue.EnqueueAsync(new DelayMessage(2), TimeSpan.Zero, token);
        await queue.EnqueueAsync(new DelayMessage(3), TimeSpan.FromMinutes(10), token);
        var handled = new List<int>();

        var processed = await queue.ProcessDueAsync(10, (message, _) =>
        {
            handled.Add(message.Id);
            return Task.CompletedTask;
        }, cancellationToken: token);

        Assert.Equal(2, processed);
        Assert.Equal(new[] { 1, 2 }, handled);
        // 未到期的消息不受影响，仍留在队列里
        Assert.Equal(1, await queue.CountAsync(token));
    }

    /// <summary>
    /// 处理失败且给了重投延迟时消息被重新入队
    /// </summary>
    /// <remarks>
    /// 延迟队列是取出即移除的，失败不重投等于丢消息，这条是防丢的关键契约。
    /// </remarks>
    [Fact]
    public async Task ProcessDueAsync_WhenHandlerFailsWithRetryDelay_ReEnqueuesItem()
    {
        var token = TestContext.Current.CancellationToken;
        var queue = new InMemoryDelayQueue<DelayMessage>();
        await queue.EnqueueAsync(new DelayMessage(1), TimeSpan.Zero, token);

        var processed = await queue.ProcessDueAsync(
            10,
            (message, _) => throw new InvalidOperationException("处理失败"),
            TimeSpan.FromMinutes(5),
            token);

        Assert.Equal(0, processed);
        Assert.Equal(1, await queue.CountAsync(token));
        Assert.Empty(await queue.DequeueDueAsync(10, token));
    }

    /// <summary>
    /// 处理失败且没有重投延迟时消息被丢弃
    /// </summary>
    [Fact]
    public async Task ProcessDueAsync_WhenHandlerFailsWithoutRetryDelay_DropsItem()
    {
        var token = TestContext.Current.CancellationToken;
        var queue = new InMemoryDelayQueue<DelayMessage>();
        await queue.EnqueueAsync(new DelayMessage(1), TimeSpan.Zero, token);

        var processed = await queue.ProcessDueAsync(
            10,
            (message, _) => throw new InvalidOperationException("处理失败"),
            cancellationToken: token);

        Assert.Equal(0, processed);
        Assert.Equal(0, await queue.CountAsync(token));
    }

    /// <summary>
    /// 队列或处理委托为空时到期消费拒绝执行
    /// </summary>
    [Fact]
    public async Task ProcessDueAsync_WithNullArguments_Throws()
    {
        IRedisDelayQueue<DelayMessage>? nullQueue = null;
        var queue = new InMemoryDelayQueue<DelayMessage>();
        Func<DelayMessage, CancellationToken, Task>? handler = null;

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => nullQueue!.ProcessDueAsync(10, (_, _) => Task.CompletedTask));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => queue.ProcessDueAsync(10, handler!));
    }

    /// <summary>
    /// 令牌已取消时消费循环不进入任何一轮
    /// </summary>
    [Fact]
    public async Task ConsumeDueAsync_WithCancelledToken_ReturnsWithoutHandling()
    {
        var token = TestContext.Current.CancellationToken;
        var queue = new InMemoryDelayQueue<DelayMessage>();
        await queue.EnqueueAsync(new DelayMessage(1), TimeSpan.Zero, token);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var handled = false;

        await queue.ConsumeDueAsync((message, _) =>
        {
            handled = true;
            return Task.CompletedTask;
        }, cancellationToken: cts.Token);

        Assert.False(handled);
        Assert.Equal(1, await queue.CountAsync(token));
    }

    /// <summary>
    /// 消费循环处理已到期消息直到被取消
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task ConsumeDueAsync_ProcessesDueItemsUntilCancelled()
    {
        var token = TestContext.Current.CancellationToken;
        var queue = new InMemoryDelayQueue<DelayMessage>();
        await queue.EnqueueAsync(new DelayMessage(1), TimeSpan.Zero, token);
        using var cts = new CancellationTokenSource();
        var handled = new List<int>();

        await queue.ConsumeDueAsync((message, _) =>
        {
            handled.Add(message.Id);
            cts.Cancel();
            return Task.CompletedTask;
        }, new RedisDelayConsumeOptions { BatchSize = 10 }, cts.Token);

        Assert.Equal(new[] { 1 }, handled);
        Assert.Equal(0, await queue.CountAsync(token));
    }

    /// <summary>
    /// 队列或处理委托为空时消费循环拒绝执行
    /// </summary>
    [Fact]
    public async Task ConsumeDueAsync_WithNullArguments_Throws()
    {
        IRedisDelayQueue<DelayMessage>? nullQueue = null;
        var queue = new InMemoryDelayQueue<DelayMessage>();
        Func<DelayMessage, CancellationToken, Task>? handler = null;

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => nullQueue!.ConsumeDueAsync((_, _) => Task.CompletedTask));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => queue.ConsumeDueAsync(handler!));
    }

    /// <summary>
    /// 消费选项的默认值
    /// </summary>
    /// <remarks>
    /// 轮询周期直接决定延迟精度，批次大小决定单轮吞吐，两者是运维口径的一部分，默认值不应随手改动。
    /// </remarks>
    [Fact]
    public void ConsumeOptions_HaveExpectedDefaults()
    {
        var options = new RedisDelayConsumeOptions();

        Assert.Equal(50, options.BatchSize);
        Assert.Equal(TimeSpan.FromSeconds(5), options.PollInterval);
        Assert.Null(options.RetryDelay);
    }

    /// <summary>
    /// 延迟队列测试消息
    /// </summary>
    /// <param name="Id">消息标识</param>
    private sealed record DelayMessage(int Id);
}
