// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Caching.Distributed;
using XiHan.Framework.Caching.Distributed.Abstracts;

namespace XiHan.Framework.Caching.Tests;

/// <summary>
/// Stream 可靠队列便捷扩展测试
/// </summary>
/// <remarks>
/// 队列本体依赖真实 Redis，这里用替身驱动扩展方法自己的编排逻辑：
/// 成功即确认、失败不确认留待重投、投递次数超限转死信、无活可干才等唤醒。
/// </remarks>
public class RedisStreamQueueExtensionsTests
{
    /// <summary>
    /// 批量入队把集合里的每条消息都投进队列
    /// </summary>
    [Fact]
    public async Task EnqueueRangeAsync_EnqueuesEveryItem()
    {
        var token = TestContext.Current.CancellationToken;
        var queue = new FakeRedisStreamQueue<StreamPayload>();

        await queue.EnqueueRangeAsync([new StreamPayload(1), new StreamPayload(2)], token);

        Assert.Equal([1, 2], queue.Enqueued.Select(item => item.Id));
    }

    /// <summary>
    /// 队列为空时批量入队拒绝执行
    /// </summary>
    [Fact]
    public async Task EnqueueRangeAsync_WithNullQueue_Throws()
    {
        IRedisStreamQueue<StreamPayload>? queue = null;

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => queue!.EnqueueRangeAsync([new StreamPayload(1)]));
    }

    /// <summary>
    /// 单条确认会被包装成集合下推
    /// </summary>
    [Fact]
    public async Task AckAsync_ForSingleMessage_ForwardsToBatchApi()
    {
        var token = TestContext.Current.CancellationToken;
        var queue = new FakeRedisStreamQueue<StreamPayload>();

        await queue.AckAsync("1-0", token);

        Assert.Equal(new[] { "1-0" }, queue.Acked);
    }

    /// <summary>
    /// 处理成功的消息被确认，并返回成功条数
    /// </summary>
    [Fact]
    public async Task ProcessBatchAsync_AcksHandledMessages()
    {
        var token = TestContext.Current.CancellationToken;
        var queue = new FakeRedisStreamQueue<StreamPayload>();
        queue.EnqueueReadBatch(
            new RedisStreamMessage<StreamPayload>("1-0", new StreamPayload(1), 1),
            new RedisStreamMessage<StreamPayload>("2-0", new StreamPayload(2), 1));
        var handled = new List<int>();

        var processed = await queue.ProcessBatchAsync("consumer-1", 10, (payload, _) =>
        {
            handled.Add(payload.Id);
            return Task.CompletedTask;
        }, token);

        Assert.Equal(2, processed);
        Assert.Equal(new[] { 1, 2 }, handled);
        Assert.Equal(new[] { "1-0", "2-0" }, queue.Acked);
        Assert.Equal("consumer-1", queue.LastConsumer);
        Assert.Equal(10, queue.LastBatchSize);
    }

    /// <summary>
    /// 处理失败的消息不确认，留在待确认列表等待重投
    /// </summary>
    [Fact]
    public async Task ProcessBatchAsync_WhenHandlerFails_DoesNotAck()
    {
        var token = TestContext.Current.CancellationToken;
        var queue = new FakeRedisStreamQueue<StreamPayload>();
        queue.EnqueueReadBatch(new RedisStreamMessage<StreamPayload>("1-0", new StreamPayload(1), 1));

        var processed = await queue.ProcessBatchAsync(
            "consumer-1",
            10,
            (payload, _) => throw new InvalidOperationException("处理失败"),
            token);

        Assert.Equal(0, processed);
        Assert.Empty(queue.Acked);
    }

    /// <summary>
    /// 反序列化失败的坏消息被直接确认丢弃
    /// </summary>
    /// <remarks>
    /// 坏消息永远处理不成功，不确认就会被无限重投，把消费者卡死在同一条消息上。
    /// </remarks>
    [Fact]
    public async Task ProcessBatchAsync_WithNullValue_AcksAndSkipsHandler()
    {
        var token = TestContext.Current.CancellationToken;
        var queue = new FakeRedisStreamQueue<StreamPayload>();
        queue.EnqueueReadBatch(new RedisStreamMessage<StreamPayload>("1-0", null, 1));
        var invoked = false;

        var processed = await queue.ProcessBatchAsync("consumer-1", 10, (payload, _) =>
        {
            invoked = true;
            return Task.CompletedTask;
        }, token);

        Assert.Equal(0, processed);
        Assert.False(invoked);
        Assert.Equal(new[] { "1-0" }, queue.Acked);
    }

    /// <summary>
    /// 队列或处理委托为空时批量处理拒绝执行
    /// </summary>
    [Fact]
    public async Task ProcessBatchAsync_WithNullArguments_Throws()
    {
        IRedisStreamQueue<StreamPayload>? nullQueue = null;
        var queue = new FakeRedisStreamQueue<StreamPayload>();
        Func<StreamPayload, CancellationToken, Task>? handler = null;

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => nullQueue!.ProcessBatchAsync("consumer-1", 10, (_, _) => Task.CompletedTask));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => queue.ProcessBatchAsync("consumer-1", 10, handler!));
    }

    /// <summary>
    /// 消费循环先认领残留的未确认消息，再读取新消息
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task ConsumeAsync_ClaimsStaleBeforeReadingNew()
    {
        var queue = new FakeRedisStreamQueue<StreamPayload>();
        queue.EnqueueClaimBatch(new RedisStreamMessage<StreamPayload>("1-0", new StreamPayload(1), 2));
        queue.EnqueueReadBatch(new RedisStreamMessage<StreamPayload>("2-0", new StreamPayload(2), 1));
        using var cts = new CancellationTokenSource();
        queue.WaitCallback = cts.Cancel;
        var handled = new List<int>();

        await queue.ConsumeAsync("consumer-1", (payload, _) =>
        {
            handled.Add(payload.Id);
            return Task.CompletedTask;
        }, cancellationToken: cts.Token);

        Assert.Equal(new[] { 1, 2 }, handled);
        Assert.Equal(new[] { "1-0", "2-0" }, queue.Acked);
    }

    /// <summary>
    /// 消费循环无活可干时才阻塞等待唤醒
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task ConsumeAsync_WhenNothingToDo_WaitsForSignal()
    {
        var queue = new FakeRedisStreamQueue<StreamPayload>();
        using var cts = new CancellationTokenSource();
        queue.WaitCallback = cts.Cancel;

        await queue.ConsumeAsync("consumer-1", (payload, _) => Task.CompletedTask, cancellationToken: cts.Token);

        Assert.Equal(1, queue.WaitCount);
        Assert.Equal(1, queue.ReadCount);
        Assert.Equal(1, queue.ClaimCount);
        Assert.Empty(queue.Acked);
    }

    /// <summary>
    /// 投递次数超过上限的消息转死信并确认丢弃
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task ConsumeAsync_WhenDeliveryCountExceedsMax_RoutesToDeadLetter()
    {
        var queue = new FakeRedisStreamQueue<StreamPayload>();
        queue.EnqueueClaimBatch(new RedisStreamMessage<StreamPayload>("1-0", new StreamPayload(1), 9));
        using var cts = new CancellationTokenSource();
        queue.WaitCallback = cts.Cancel;
        var handled = false;
        var deadLettered = new List<string>();

        await queue.ConsumeAsync(
            "consumer-1",
            (payload, _) =>
            {
                handled = true;
                return Task.CompletedTask;
            },
            new RedisStreamConsumeOptions { MaxDeliveryCount = 5 },
            (message, _) =>
            {
                deadLettered.Add(message.Id);
                return Task.CompletedTask;
            },
            cts.Token);

        Assert.False(handled);
        Assert.Equal(new[] { "1-0" }, deadLettered);
        Assert.Equal(new[] { "1-0" }, queue.Acked);
    }

    /// <summary>
    /// 上限为零表示不限投递次数，消息始终交给处理委托
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task ConsumeAsync_WithUnlimitedDeliveryCount_KeepsHandling()
    {
        var queue = new FakeRedisStreamQueue<StreamPayload>();
        queue.EnqueueClaimBatch(new RedisStreamMessage<StreamPayload>("1-0", new StreamPayload(1), 99));
        using var cts = new CancellationTokenSource();
        queue.WaitCallback = cts.Cancel;
        var handled = false;

        await queue.ConsumeAsync(
            "consumer-1",
            (payload, _) =>
            {
                handled = true;
                return Task.CompletedTask;
            },
            new RedisStreamConsumeOptions { MaxDeliveryCount = 0 },
            cancellationToken: cts.Token);

        Assert.True(handled);
        Assert.Equal(new[] { "1-0" }, queue.Acked);
    }

    /// <summary>
    /// 消费循环把认领空闲阈值透传给队列
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task ConsumeAsync_PassesConsumeOptionsToQueue()
    {
        var queue = new FakeRedisStreamQueue<StreamPayload>();
        using var cts = new CancellationTokenSource();
        queue.WaitCallback = cts.Cancel;

        await queue.ConsumeAsync(
            "consumer-1",
            (payload, _) => Task.CompletedTask,
            new RedisStreamConsumeOptions { BatchSize = 7, MinIdle = TimeSpan.FromMinutes(3) },
            cancellationToken: cts.Token);

        Assert.Equal(7, queue.LastBatchSize);
        Assert.Equal(TimeSpan.FromMinutes(3), queue.LastMinIdle);
    }

    /// <summary>
    /// 队列或处理委托为空时消费循环拒绝执行
    /// </summary>
    [Fact]
    public async Task ConsumeAsync_WithNullArguments_Throws()
    {
        IRedisStreamQueue<StreamPayload>? nullQueue = null;
        var queue = new FakeRedisStreamQueue<StreamPayload>();
        Func<StreamPayload, CancellationToken, Task>? handler = null;

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => nullQueue!.ConsumeAsync("consumer-1", (_, _) => Task.CompletedTask));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => queue.ConsumeAsync("consumer-1", handler!));
    }

    /// <summary>
    /// 消费选项的默认值
    /// </summary>
    [Fact]
    public void ConsumeOptions_HaveExpectedDefaults()
    {
        var options = new RedisStreamConsumeOptions();

        Assert.Equal(10, options.BatchSize);
        Assert.Equal(TimeSpan.FromSeconds(30), options.IdleWait);
        Assert.Equal(TimeSpan.FromMinutes(1), options.MinIdle);
        Assert.Equal(5, options.MaxDeliveryCount);
    }

    /// <summary>
    /// Stream 消息载荷记录
    /// </summary>
    /// <param name="Id">消息标识</param>
    public sealed record StreamPayload(int Id);
}
