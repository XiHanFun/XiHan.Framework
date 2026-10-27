// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Persistence.Abstractions.Identifiers;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Tests;

/// <summary>
/// <see cref="SnowflakeIdGenerator"/>：编码、同毫秒序列、序列用尽等下一毫秒、回拨等待与拒绝、没有机器位拒绝、并发唯一与吞吐。
/// </summary>
public sealed class SnowflakeIdGeneratorTests
{
    /// <summary>
    /// 用例的起始时刻：epoch 之后 5 秒。
    /// </summary>
    private static readonly DateTimeOffset _start = SnowflakeOptions.DefaultEpoch.AddSeconds(5);

    /// <summary>
    /// 等待阻塞中的生成完成的上限。
    /// </summary>
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public void NewId_EncodesTimestampWorkerAndSequence()
    {
        var clock = new FakeTimeProvider(_start);
        var generator = Create(clock, workerId: 7);

        var first = generator.Parse(generator.NewId());
        var second = generator.Parse(generator.NewId());
        clock.Advance(TimeSpan.FromMilliseconds(3));
        var third = generator.Parse(generator.NewId());

        Assert.Equal(_start, first.Timestamp);
        Assert.Equal(7, first.WorkerId);
        Assert.Equal(0, first.Sequence);
        Assert.Equal(1, second.Sequence);
        Assert.Equal(_start.AddMilliseconds(3), third.Timestamp);
        Assert.Equal(0, third.Sequence);
        Assert.True(third.Value > second.Value && second.Value > first.Value);
        Assert.Equal(7, generator.WorkerId.ValueOrThrow());
        Assert.Equal(SnowflakeOptions.DefaultEpoch, generator.Epoch);
    }

    [Fact]
    public async Task NewId_WaitsForNextMillisecond_WhenSequenceIsExhausted()
    {
        var clock = new FakeTimeProvider(_start);
        var generator = Create(clock, workerId: 1);
        var ids = new HashSet<long>();
        for (var i = 0; i <= SnowflakeId.MaxSequence; i++)
        {
            Assert.True(ids.Add(generator.NewId()));
        }

        Assert.Equal(SnowflakeId.MaxSequence, generator.Parse(ids.Max()).Sequence);

        // 第 4097 个：本毫秒用尽，生成线程在锁内等时钟走到下一毫秒
        var pending = Task.Run(generator.NewId);
        await Task.Delay(100);
        Assert.False(pending.IsCompleted);

        clock.Advance(TimeSpan.FromMilliseconds(1));
        var next = generator.Parse(await pending.WaitAsync(_timeout));

        Assert.Equal(_start.AddMilliseconds(1), next.Timestamp);
        Assert.Equal(0, next.Sequence);
        Assert.True(ids.Add(next.Value));
    }

    [Fact]
    public async Task NewId_WaitsForClock_WhenDriftIsWithinTolerance()
    {
        var clock = new ManualClock(_start);
        var generator = Create(clock, workerId: 1);
        var first = generator.NewId();

        // 回拨 50 ms（容忍 100 ms）：等时钟追平上一个时间戳，而不是发出更小的标识
        clock.Set(_start.AddMilliseconds(-50));
        var pending = Task.Run(generator.NewId);
        await Task.Delay(100);
        Assert.False(pending.IsCompleted);

        clock.Set(_start);
        var second = generator.Parse(await pending.WaitAsync(_timeout));

        Assert.Equal(_start, second.Timestamp);
        Assert.Equal(1, second.Sequence);
        Assert.True(second.Value > first);
    }

    [Fact]
    public void NewId_Throws_WhenDriftExceedsTolerance_AndRecoversWhenClockCatchesUp()
    {
        var clock = new ManualClock(_start);
        var generator = Create(clock, workerId: 1);
        var first = generator.NewId();

        clock.Set(_start.AddMilliseconds(-101));
        var exception = Assert.Throws<FrameworkException>(() => generator.NewId());
        Assert.Equal(PersistenceErrors.ClockMovedBackwards, exception.Code);
        Assert.Contains("101 ms", exception.Message, StringComparison.Ordinal);

        // 时钟追平后恢复，且不重复
        clock.Set(_start.AddMilliseconds(1));
        Assert.True(generator.NewId() > first);
    }

    [Fact]
    public void NewId_Throws_WhenNoWorkerIdIsAvailable()
    {
        var generator = new SnowflakeIdGenerator(new SnowflakeOptions(), new NoWorkerIdSource(), new FakeTimeProvider(_start));

        var exception = Assert.Throws<FrameworkException>(() => generator.NewId());

        Assert.Equal(PersistenceErrors.WorkerIdUnavailable, exception.Code);
        Assert.False(generator.WorkerId.HasValue);
    }

    [Fact]
    public void Constructor_Throws_WhenClockIsBeforeEpoch()
    {
        var clock = new FakeTimeProvider(SnowflakeOptions.DefaultEpoch.AddSeconds(-1));

        var exception = Assert.Throws<ArgumentException>(() => Create(clock, workerId: 1));

        Assert.Contains("Epoch", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NewId_IsUniqueAcrossThreads_AndSustainsThroughput()
    {
        const int threads = 8;
        const int perThread = 25_000;
        var generator = Create(TimeProvider.System, workerId: 1);
        var ids = new ConcurrentBag<long>();
        using var start = new ManualResetEventSlim(false);
        var workers = Enumerable.Range(0, threads).Select(_ => Task.Run(() =>
        {
            start.Wait();
            for (var i = 0; i < perThread; i++)
            {
                ids.Add(generator.NewId());
            }
        })).ToArray();

        var stopwatch = Stopwatch.StartNew();
        start.Set();
        await Task.WhenAll(workers);
        stopwatch.Stop();

        Assert.Equal(threads * perThread, ids.Count);
        Assert.Equal(ids.Count, ids.Distinct().Count());
        // 理论上限 4096 个 / 毫秒，20 万个至少 49 ms；门槛放宽到 5 秒（4 万个 / 秒）只守住"没有退化成逐个等待"
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Generating {ids.Count} IDs took {stopwatch.Elapsed}.");
    }

    /// <summary>
    /// 固定机器位、缺省选项的生成器。
    /// </summary>
    private static SnowflakeIdGenerator Create(TimeProvider clock, int workerId) =>
        new(new SnowflakeOptions(), new FixedWorkerIdSource(workerId), clock);

    /// <summary>
    /// 永远没有机器位的来源。
    /// </summary>
    private sealed class NoWorkerIdSource : IWorkerIdSource
    {
        /// <inheritdoc />
        public Maybe<int> Current => default;
    }
}
