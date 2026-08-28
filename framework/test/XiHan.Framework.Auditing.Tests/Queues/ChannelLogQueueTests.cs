// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Auditing.Options;
using XiHan.Framework.Auditing.Queues;

namespace XiHan.Framework.Auditing.Tests.Queues;

/// <summary>
/// 基于 Channel 的日志队列测试
/// </summary>
/// <remarks>
/// 队列固定使用 <c>BoundedChannelFullMode.Wait</c>，这一选择决定了两个方法的语义差别：
/// <c>TryEnqueue</c> 满时必须诚实返回 false（若底层改成 DropWrite 就会假报成功，上层再也无从得知日志被丢了），
/// <c>EnqueueAsync</c> 满时必须挂起等待空位并可被取消。这两条＋容量边界是本类型的全部契约。
/// <para>
/// 注意：这里刻意用 <c>Microsoft.Extensions.Options.Options</c> 全名——测试工程存在
/// <c>XiHan.Framework.Auditing.Tests.Options</c> 命名空间，裸写 <c>Options</c> 会被它遮蔽。
/// </para>
/// </remarks>
public class ChannelLogQueueTests
{
    /// <summary>
    /// 新建队列为空
    /// </summary>
    [Fact]
    public void Count_WhenNewQueue_IsZero()
    {
        var queue = CreateQueue(4);

        Assert.Equal(0, queue.Count);
    }

    /// <summary>
    /// 有空位时尝试入队成功并增加队列数量
    /// </summary>
    [Fact]
    public void TryEnqueue_WhenCapacityAvailable_ReturnsTrueAndCounts()
    {
        var queue = CreateQueue(3);

        Assert.True(queue.TryEnqueue(1));
        Assert.True(queue.TryEnqueue(2));

        Assert.Equal(2, queue.Count);
    }

    /// <summary>
    /// 队列满时尝试入队返回 false 且记录未入队（不允许假报成功）
    /// </summary>
    [Fact]
    public void TryEnqueue_WhenFull_ReturnsFalseAndDoesNotAccept()
    {
        var queue = CreateQueue(2);

        Assert.True(queue.TryEnqueue(1));
        Assert.True(queue.TryEnqueue(2));

        Assert.False(queue.TryEnqueue(3));
        Assert.Equal(2, queue.Count);
    }

    /// <summary>
    /// 容量小于 1 的选项在构造期就被底层 Channel 拒绝，属快速失败
    /// </summary>
    /// <param name="capacity">配置的队列容量</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Ctor_WhenCapacityBelowOne_Throws(int capacity)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
        {
            _ = CreateQueue(capacity);
        });
    }

    /// <summary>
    /// 使用默认选项时队列容量为 10000，第 10001 条才会被拒
    /// </summary>
    [Fact]
    public void Ctor_WithDefaultOptions_UsesTenThousandCapacity()
    {
        var queue = new ChannelLogQueue<int>(
            Microsoft.Extensions.Options.Options.Create(new XiHanAuditingLogQueueOptions()));

        var accepted = 0;
        for (var i = 0; i < 10000; i++)
        {
            if (queue.TryEnqueue(i))
            {
                accepted++;
            }
        }

        Assert.Equal(10000, accepted);
        Assert.False(queue.TryEnqueue(10000));
    }

    /// <summary>
    /// 有空位时入队同步完成，不产生真正的异步等待
    /// </summary>
    [Fact]
    public void EnqueueAsync_WhenCapacityAvailable_CompletesSynchronously()
    {
        var queue = CreateQueue(2);

        var pending = queue.EnqueueAsync(1, TestContext.Current.CancellationToken);

        Assert.True(pending.IsCompleted);
        Assert.Equal(1, queue.Count);
    }

    /// <summary>
    /// 出队按先进先出顺序产出记录
    /// </summary>
    [Fact]
    public async Task DequeueAllAsync_YieldsRecordsInFifoOrder()
    {
        var queue = CreateQueue(8);
        Assert.True(queue.TryEnqueue(10));
        Assert.True(queue.TryEnqueue(20));
        Assert.True(queue.TryEnqueue(30));

        await using var enumerator = queue
            .DequeueAllAsync(TestContext.Current.CancellationToken)
            .GetAsyncEnumerator();

        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal(10, enumerator.Current);
        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal(20, enumerator.Current);
        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal(30, enumerator.Current);

        Assert.Equal(0, queue.Count);
    }

    /// <summary>
    /// 令牌已取消时出队立即抛出取消异常，即便队列里还有存量
    /// </summary>
    [Fact]
    public async Task DequeueAllAsync_WhenTokenAlreadyCanceled_Throws()
    {
        var queue = CreateQueue(4);
        Assert.True(queue.TryEnqueue(1));

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var item in queue.DequeueAllAsync(cts.Token))
            {
                Assert.Fail($"令牌已取消不应产出记录，却拿到 {item}。");
            }
        });
    }

    /// <summary>
    /// 队列满时入队挂起等待，读走一条腾出空位后自动完成（反压语义）
    /// </summary>
    [Fact]
    public async Task EnqueueAsync_WhenFull_WaitsUntilSpaceAvailable()
    {
        var queue = CreateQueue(1);
        Assert.True(queue.TryEnqueue(1));
        Assert.False(queue.TryEnqueue(2));

        var pending = queue.EnqueueAsync(2, TestContext.Current.CancellationToken).AsTask();

        Assert.False(pending.IsCompleted);

        await using var enumerator = queue
            .DequeueAllAsync(TestContext.Current.CancellationToken)
            .GetAsyncEnumerator();

        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal(1, enumerator.Current);

        // 读走一条后，阻塞中的写入应当被放行
        await pending;

        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal(2, enumerator.Current);
    }

    /// <summary>
    /// 队列满时入队挂起，令牌取消后抛出取消异常而不是无限等待
    /// </summary>
    [Fact]
    public async Task EnqueueAsync_WhenFullAndCanceled_Throws()
    {
        var queue = CreateQueue(1);
        Assert.True(queue.TryEnqueue(1));

        using var cts = new CancellationTokenSource();
        var pending = queue.EnqueueAsync(2, cts.Token).AsTask();

        Assert.False(pending.IsCompleted);

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(1, queue.Count);
    }

    /// <summary>
    /// 多线程并发写入不丢记录、不重复，队列自身线程安全
    /// </summary>
    [Fact]
    public async Task TryEnqueue_WhenConcurrentWriters_KeepsEveryRecordExactlyOnce()
    {
        const int WriterCount = 4;
        const int PerWriter = 250;

        var queue = CreateQueue(WriterCount * PerWriter);

        var writers = Enumerable.Range(0, WriterCount)
            .Select(writerIndex => Task.Run(
                () =>
                {
                    for (var i = 0; i < PerWriter; i++)
                    {
                        Assert.True(queue.TryEnqueue((writerIndex * PerWriter) + i));
                    }
                },
                TestContext.Current.CancellationToken))
            .ToArray();

        await Task.WhenAll(writers);

        Assert.Equal(WriterCount * PerWriter, queue.Count);

        var received = new HashSet<int>();
        await using var enumerator = queue
            .DequeueAllAsync(TestContext.Current.CancellationToken)
            .GetAsyncEnumerator();

        for (var i = 0; i < WriterCount * PerWriter; i++)
        {
            Assert.True(await enumerator.MoveNextAsync());
            Assert.True(received.Add(enumerator.Current), $"记录 {enumerator.Current} 被重复产出。");
        }

        Assert.Equal(WriterCount * PerWriter, received.Count);
    }

    private static ChannelLogQueue<int> CreateQueue(int capacity)
    {
        var options = new XiHanAuditingLogQueueOptions { QueueCapacity = capacity };
        return new ChannelLogQueue<int>(Microsoft.Extensions.Options.Options.Create(options));
    }
}
