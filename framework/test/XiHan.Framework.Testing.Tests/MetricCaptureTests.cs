// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.Metrics;
using System.Globalization;

namespace XiHan.Framework.Testing.Tests;

/// <summary>
/// <see cref="MetricCapture"/> 的过滤、按异步流收、条数上限、并发与释放
/// </summary>
/// <remarks>
/// 每个用例用名字唯一的 Meter，并行的别的用例记不到它上面
/// </remarks>
public sealed class MetricCaptureTests : IDisposable
{
    /// <summary>
    /// 本用例的 Meter
    /// </summary>
    private readonly Meter _meter = new($"xihan.testing.tests.metrics.{Guid.NewGuid():N}");

    /// <inheritdoc />
    public void Dispose() => _meter.Dispose();

    [Fact]
    public void Entries_KeepMeterInstrumentValueAndTags_InRecordOrder()
    {
        using var metrics = new MetricCapture([_meter.Name]);
        var placed = _meter.CreateCounter<long>("orders.placed");
        var duration = _meter.CreateHistogram<double>("orders.duration");

        placed.Add(2, new KeyValuePair<string, object?>("region", "east"));
        duration.Record(1.5);

        Assert.Collection(
            metrics.Entries,
            first =>
            {
                Assert.Equal((_meter.Name, "orders.placed", 2d), (first.Meter, first.Instrument, first.Value));
                Assert.Equal(new Dictionary<string, object?> { ["region"] = "east" }, first.Tags);
            },
            second =>
            {
                Assert.Equal((_meter.Name, "orders.duration", 1.5), (second.Meter, second.Instrument, second.Value));
                Assert.Empty(second.Tags);
            });
    }

    [Fact]
    public void EveryNumericInstrumentType_IsCaptured_AsADouble()
    {
        using var metrics = new MetricCapture([_meter.Name]);

        _meter.CreateCounter<byte>("byte").Add(1);
        _meter.CreateCounter<short>("short").Add(2);
        _meter.CreateCounter<int>("int").Add(3);
        _meter.CreateCounter<long>("long").Add(4);
        _meter.CreateCounter<float>("float").Add(5.5f);
        _meter.CreateCounter<double>("double").Add(6.25);
        _meter.CreateCounter<decimal>("decimal").Add(7.75m);

        Assert.Equal(["byte", "short", "int", "long", "float", "double", "decimal"], metrics.Entries.Select(static e => e.Instrument));
        Assert.Equal([1d, 2d, 3d, 4d, 5.5d, 6.25d, 7.75d], metrics.Entries.Select(static e => e.Value));
    }

    [Fact]
    public void OnlyTheListenedMeters_AreCaptured_AndTheirInstrumentsEnabled()
    {
        using var other = new Meter($"xihan.testing.tests.metrics.{Guid.NewGuid():N}");
        using var metrics = new MetricCapture([_meter.Name]);
        var listened = _meter.CreateCounter<int>("listened");
        var ignored = other.CreateCounter<int>("ignored");

        listened.Add(1);
        ignored.Add(1);

        Assert.True(listened.Enabled);
        Assert.False(ignored.Enabled);
        Assert.Equal(["listened"], metrics.Entries.Select(static e => e.Instrument));
    }

    [Fact]
    public void RepeatedTagKeys_KeepTheLastValue_WithoutThrowingIntoTheRecorder()
    {
        using var metrics = new MetricCapture([_meter.Name]);

        _meter.CreateCounter<int>("tagged").Add(1, new KeyValuePair<string, object?>("key", "first"), new KeyValuePair<string, object?>("key", "last"));

        Assert.Equal("last", Assert.Single(metrics.Entries).Tags["key"]);
    }

    [Fact]
    public async Task RestrictToCurrentFlow_KeepsThisFlowAndTheFlowsItStarts()
    {
        using var metrics = new MetricCapture([_meter.Name]);
        var counter = _meter.CreateCounter<int>("flows");

        // 限定之前每条流的都收
        await RunDetached(() => counter.Add(1, new KeyValuePair<string, object?>("flow", "detached before")));
        metrics.RestrictToCurrentFlow();
        counter.Add(1, new KeyValuePair<string, object?>("flow", "this"));
        await Task.Run(() => counter.Add(1, new KeyValuePair<string, object?>("flow", "child")), TestContext.Current.CancellationToken);
        await RunDetached(() => counter.Add(1, new KeyValuePair<string, object?>("flow", "detached after")));

        Assert.Equal(["detached before", "this", "child"], metrics.Entries.Select(static e => (string?)e.Tags["flow"]));
    }

    [Fact]
    public async Task RestrictToCurrentFlow_DoesNotReachTheCaller()
    {
        using var metrics = new MetricCapture([_meter.Name]);
        var counter = _meter.CreateCounter<int>("caller");

        // 异步方法里的绑定在它返回时随执行上下文还原：调用方的流没有绑定这个收集器
        await RestrictInsideAsync(metrics, counter);
        counter.Add(1, new KeyValuePair<string, object?>("flow", "caller"));

        Assert.Equal(["inside"], metrics.Entries.Select(static e => (string?)e.Tags["flow"]));
    }

    [Fact]
    public void RestrictToCurrentFlow_OnTwoCaptures_BindsBoth()
    {
        using var first = new MetricCapture([_meter.Name]);
        using var second = new MetricCapture([_meter.Name]);
        var counter = _meter.CreateCounter<int>("both");

        first.RestrictToCurrentFlow();
        second.RestrictToCurrentFlow();
        counter.Add(1);

        Assert.Single(first.Entries);
        Assert.Single(second.Entries);
    }

    [Fact]
    public void Capacity_DropsTheOldest()
    {
        using var metrics = new MetricCapture([_meter.Name], capacity: 3);
        var counter = _meter.CreateCounter<int>("bounded");

        for (var value = 1; value <= 5; value++)
        {
            counter.Add(value);
        }

        Assert.Equal(3, metrics.Capacity);
        Assert.Equal([3d, 4d, 5d], metrics.Entries.Select(static e => e.Value));
    }

    [Fact]
    public void Capacity_DefaultsToTenThousand_AndTheArgumentsAreChecked()
    {
        using var metrics = new MetricCapture([_meter.Name]);

        Assert.Equal(MetricCapture.DefaultCapacity, metrics.Capacity);
        Assert.Equal(10_000, MetricCapture.DefaultCapacity);
        Assert.Throws<ArgumentOutOfRangeException>(() => new MetricCapture([_meter.Name], 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MetricCapture([_meter.Name], -1));
        Assert.Throws<ArgumentNullException>(() => new MetricCapture(null!));
        Assert.Throws<ArgumentException>(() => new MetricCapture([]));
        Assert.Throws<ArgumentException>(() => new MetricCapture([" "]));
    }

    [Fact]
    public void Dispose_StopsListening_AndKeepsWhatWasCaptured()
    {
        var metrics = new MetricCapture([_meter.Name]);
        var counter = _meter.CreateCounter<int>("disposed");
        counter.Add(1);

        metrics.Dispose();
        counter.Add(2);

        Assert.False(counter.Enabled);
        Assert.Equal([1d], metrics.Entries.Select(static e => e.Value));
    }

    [Fact]
    public async Task ConcurrentWritersAndReaders_LoseNothing_AndNeverExceedTheCapacity()
    {
        const int Writers = 8;
        const int PerWriter = 500;
        using var bounded = new MetricCapture([_meter.Name], capacity: 100);
        using var roomy = new MetricCapture([_meter.Name]);
        using var stop = new CancellationTokenSource();
        var counter = _meter.CreateCounter<int>("concurrent");

        // 一边写一边读快照：读到的条数任何时刻都不超过上限
        var reader = Task.Run(
            () =>
            {
                var largest = 0;
                while (!stop.IsCancellationRequested)
                {
                    largest = Math.Max(largest, bounded.Entries.Count);
                }

                return largest;
            },
            TestContext.Current.CancellationToken);
        await Task.WhenAll(Enumerable.Range(0, Writers).Select(writer => Task.Run(
            () =>
            {
                var tag = new KeyValuePair<string, object?>("writer", writer.ToString(CultureInfo.InvariantCulture));
                for (var index = 0; index < PerWriter; index++)
                {
                    counter.Add(index, tag);
                }
            },
            TestContext.Current.CancellationToken)));
        await stop.CancelAsync();

        Assert.True(await reader <= 100);
        Assert.Equal(100, bounded.Entries.Count);
        // 上限够大时一条不丢，每个写者自己的顺序保持
        var entries = roomy.Entries;
        Assert.Equal(Writers * PerWriter, entries.Count);
        Assert.All(
            entries.GroupBy(static e => e.Tags["writer"]),
            group => Assert.Equal(Enumerable.Range(0, PerWriter).Select(static i => (double)i), group.Select(static e => e.Value)));
    }

    /// <summary>
    /// 在不带当前执行上下文的线程池线程上执行
    /// </summary>
    /// <param name="action">要执行的动作</param>
    private static Task RunDetached(Action action)
    {
        // 抑制执行上下文的流动：新线程上读不到这里的 AsyncLocal
        using (ExecutionContext.SuppressFlow())
        {
            return Task.Run(action, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// 在一个异步方法里限定到它的流、记一条后返回
    /// </summary>
    /// <param name="metrics">收集器</param>
    /// <param name="counter">计数器</param>
    private static async Task RestrictInsideAsync(MetricCapture metrics, Counter<int> counter)
    {
        await Task.Yield();
        metrics.RestrictToCurrentFlow();
        counter.Add(1, new KeyValuePair<string, object?>("flow", "inside"));
    }
}
