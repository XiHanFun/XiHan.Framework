// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using XiHan.Framework.Observability.Metrics;

namespace XiHan.Framework.Observability.Tests.Metrics;

/// <summary>
/// 指标收集器测试
/// </summary>
/// <remarks>
/// MetricsCollector 已不在进程内留存指标（GetMetrics 恒空），唯一可观测的公共契约是
/// 「往 Meter(<see cref="MetricsCollector.MeterName"/>) 上发射了什么测量事件」，
/// 因此所有断言都经 MeterListener 捕获真实测量值。
/// 每个用例的指标名带 GUID 后缀，与并行用例、其他 MetricsCollector 实例天然隔离。
/// </remarks>
public class MetricsCollectorTests
{
    /// <summary>
    /// Meter 名是对外协议的一部分（OTel 装配处 AddMeter 按此字符串订阅），不允许漂移
    /// </summary>
    [Fact]
    public void MeterName_AsExportContract_IsStable()
    {
        Assert.Equal("XiHan.Metrics", MetricsCollector.MeterName);
    }

    /// <summary>
    /// 收集器实现指标收集接口
    /// </summary>
    [Fact]
    public void MetricsCollector_Always_ImplementsCollectorContract()
    {
        using var collector = new MetricsCollector();

        Assert.IsAssignableFrom<IMetricsCollector>(collector);
        Assert.IsAssignableFrom<IDisposable>(collector);
    }

    /// <summary>
    /// 不传增量时按默认值 1 发射到计数器仪表
    /// </summary>
    [Fact]
    public void RecordCounter_WithDefaultValue_EmitsDeltaOne()
    {
        var name = NewMetricName();
        using var capture = new MeasurementCapture(name);
        using var collector = new MetricsCollector();

        collector.RecordCounter(name);

        var measurements = capture.Measurements;
        Assert.Single(measurements);
        Assert.Equal(name, measurements[0].InstrumentName);
        Assert.Equal(1d, measurements[0].Value);
        Assert.True(measurements[0].IsCounter);
        Assert.False(measurements[0].IsHistogram);
    }

    /// <summary>
    /// 连续记录时每次增量都独立发射，累加值精确
    /// </summary>
    [Fact]
    public void RecordCounter_CalledRepeatedly_EmitsEveryDeltaExactly()
    {
        var name = NewMetricName();
        using var capture = new MeasurementCapture(name);
        using var collector = new MetricsCollector();

        collector.RecordCounter(name, 5);
        collector.RecordCounter(name, 7);
        collector.RecordCounter(name, 11);

        var measurements = capture.Measurements;
        Assert.Equal(3, measurements.Count);
        Assert.Equal(5d, measurements[0].Value);
        Assert.Equal(7d, measurements[1].Value);
        Assert.Equal(11d, measurements[2].Value);
        Assert.Equal(23d, measurements.Sum(m => m.Value));
    }

    /// <summary>
    /// 计数器增量允许为 0，仍应产生一次测量事件
    /// </summary>
    [Fact]
    public void RecordCounter_WithZeroDelta_StillEmitsMeasurement()
    {
        var name = NewMetricName();
        using var capture = new MeasurementCapture(name);
        using var collector = new MetricsCollector();

        collector.RecordCounter(name, 0);

        var measurements = capture.Measurements;
        Assert.Single(measurements);
        Assert.Equal(0d, measurements[0].Value);
    }

    /// <summary>
    /// 标签字典逐项转成仪表标签
    /// </summary>
    [Fact]
    public void RecordCounter_WithTags_ForwardsEveryTagToMeasurement()
    {
        var name = NewMetricName();
        using var capture = new MeasurementCapture(name);
        using var collector = new MetricsCollector();

        collector.RecordCounter(name, 2, new Dictionary<string, string>
        {
            ["env"] = "test",
            ["tenant"] = "t1"
        });

        var measurements = capture.Measurements;
        Assert.Single(measurements);
        Assert.Equal(2, measurements[0].Tags.Count);
        Assert.Equal("test", measurements[0].Tags["env"]);
        Assert.Equal("t1", measurements[0].Tags["tenant"]);
    }

    /// <summary>
    /// 不传标签时发射的测量事件不带任何标签
    /// </summary>
    [Fact]
    public void RecordCounter_WithoutTags_EmitsMeasurementWithoutTags()
    {
        var name = NewMetricName();
        using var capture = new MeasurementCapture(name);
        using var collector = new MetricsCollector();

        collector.RecordCounter(name);

        var measurements = capture.Measurements;
        Assert.Single(measurements);
        Assert.Empty(measurements[0].Tags);
    }

    /// <summary>
    /// 同名指标复用同一个仪表实例，不会每次记录都新建
    /// </summary>
    /// <remarks>
    /// 仪表只在创建时向监听器发布一次，因此发布次数就是仪表实例数；
    /// 这里锁的是 ConcurrentDictionary 缓存生效，避免退化成「每次 Record 造一个仪表」。
    /// </remarks>
    [Fact]
    public void RecordCounter_SameNameRecordedTwice_CreatesSingleInstrument()
    {
        var name = NewMetricName();
        using var capture = new MeasurementCapture(name);
        using var collector = new MetricsCollector();

        collector.RecordCounter(name);
        collector.RecordCounter(name);

        Assert.Equal(1, capture.PublishedCount);
        Assert.Equal(2, capture.Measurements.Count);
    }

    /// <summary>
    /// 不同名的计数器互不串味
    /// </summary>
    [Fact]
    public void RecordCounter_WithDifferentNames_EmitsToSeparateInstruments()
    {
        var first = NewMetricName();
        var second = NewMetricName();
        using var capture = new MeasurementCapture(first, second);
        using var collector = new MetricsCollector();

        collector.RecordCounter(first, 3);
        collector.RecordCounter(second, 4);

        var measurements = capture.Measurements;
        Assert.Equal(2, capture.PublishedCount);
        Assert.Equal(3d, measurements.Single(m => m.InstrumentName == first).Value);
        Assert.Equal(4d, measurements.Single(m => m.InstrumentName == second).Value);
    }

    /// <summary>
    /// 直方图记录发射到直方图仪表而非计数器
    /// </summary>
    [Fact]
    public void RecordHistogram_Always_EmitsToHistogramInstrument()
    {
        var name = NewMetricName();
        using var capture = new MeasurementCapture(name);
        using var collector = new MetricsCollector();

        collector.RecordHistogram(name, 12.5);

        var measurements = capture.Measurements;
        Assert.Single(measurements);
        Assert.Equal(12.5d, measurements[0].Value);
        Assert.True(measurements[0].IsHistogram);
        Assert.False(measurements[0].IsCounter);
    }

    /// <summary>
    /// 直方图保留浮点精度，不做取整
    /// </summary>
    [Theory]
    [InlineData(0d)]
    [InlineData(-1.75d)]
    [InlineData(0.000125d)]
    [InlineData(98765.4321d)]
    public void RecordHistogram_WithVariousValues_PreservesExactValue(double value)
    {
        var name = NewMetricName();
        using var capture = new MeasurementCapture(name);
        using var collector = new MetricsCollector();

        collector.RecordHistogram(name, value);

        var measurements = capture.Measurements;
        Assert.Single(measurements);
        Assert.Equal(value, measurements[0].Value);
    }

    /// <summary>
    /// 测量值走直方图承载（无 pull 型 gauge 回调上下文）
    /// </summary>
    [Fact]
    public void RecordMeasurement_Always_RoutesToHistogramInstrument()
    {
        var name = NewMetricName();
        using var capture = new MeasurementCapture(name);
        using var collector = new MetricsCollector();

        collector.RecordMeasurement(name, 42d);

        var measurements = capture.Measurements;
        Assert.Single(measurements);
        Assert.Equal(name, measurements[0].InstrumentName);
        Assert.Equal(42d, measurements[0].Value);
        Assert.True(measurements[0].IsHistogram);
    }

    /// <summary>
    /// 同名的测量值与直方图共用同一个仪表
    /// </summary>
    [Fact]
    public void RecordMeasurement_AndRecordHistogram_ShareSameInstrument()
    {
        var name = NewMetricName();
        using var capture = new MeasurementCapture(name);
        using var collector = new MetricsCollector();

        collector.RecordMeasurement(name, 1d);
        collector.RecordHistogram(name, 2d);

        Assert.Equal(1, capture.PublishedCount);
        Assert.Equal(2, capture.Measurements.Count);
    }

    /// <summary>
    /// 计时器返回可释放句柄，且释放前不产生任何测量事件
    /// </summary>
    [Fact]
    public void BeginTimer_BeforeDispose_EmitsNothing()
    {
        var name = NewMetricName();
        using var capture = new MeasurementCapture($"{name}.duration");
        using var collector = new MetricsCollector();

        var timer = collector.BeginTimer(name);

        Assert.NotNull(timer);
        Assert.Empty(capture.Measurements);

        timer.Dispose();
    }

    /// <summary>
    /// 计时器释放时把耗时写入 名称 + .duration 的直方图
    /// </summary>
    [Fact]
    public void BeginTimer_OnDispose_RecordsElapsedMillisecondsToDurationHistogram()
    {
        var name = NewMetricName();
        using var capture = new MeasurementCapture($"{name}.duration");
        using var collector = new MetricsCollector();

        using (collector.BeginTimer(name))
        {
            Thread.Sleep(30);
        }

        var measurements = capture.Measurements;
        Assert.Single(measurements);
        Assert.Equal($"{name}.duration", measurements[0].InstrumentName);
        Assert.True(measurements[0].IsHistogram);
        // 睡眠 30ms 后的实测耗时必然大于 10ms，上界给到 60s 只为防止单位写错（秒/毫秒混用）
        Assert.InRange(measurements[0].Value, 10d, 60_000d);
    }

    /// <summary>
    /// 计时器把标签一并带到耗时测量事件上
    /// </summary>
    [Fact]
    public void BeginTimer_WithTags_ForwardsTagsToDurationMeasurement()
    {
        var name = NewMetricName();
        using var capture = new MeasurementCapture($"{name}.duration");
        using var collector = new MetricsCollector();

        using (collector.BeginTimer(name, new Dictionary<string, string> { ["stage"] = "warmup" }))
        {
        }

        var measurements = capture.Measurements;
        Assert.Single(measurements);
        Assert.Equal("warmup", measurements[0].Tags["stage"]);
    }

    /// <summary>
    /// 多线程并发累加计数器，测量事件数量与累加总量都精确无丢失
    /// </summary>
    /// <remarks>
    /// 这里不断言仪表发布次数：ConcurrentDictionary.GetOrAdd 的工厂在竞争下允许多次执行，
    /// 多余的仪表会被丢弃且从不接收测量值，因此只有「总量精确」才是真正的线程安全契约。
    /// </remarks>
    [Fact]
    public void RecordCounter_FromMultipleThreads_AccumulatesWithoutLoss()
    {
        const int ThreadCount = 8;
        const int PerThread = 250;

        var name = NewMetricName();
        using var capture = new MeasurementCapture(name);
        using var collector = new MetricsCollector();

        var threads = new Thread[ThreadCount];
        for (var i = 0; i < ThreadCount; i++)
        {
            threads[i] = new Thread(() =>
            {
                for (var j = 0; j < PerThread; j++)
                {
                    collector.RecordCounter(name);
                }
            });
        }

        foreach (var thread in threads)
        {
            thread.Start();
        }

        foreach (var thread in threads)
        {
            thread.Join();
        }

        var measurements = capture.Measurements;
        Assert.Equal(ThreadCount * PerThread, measurements.Count);
        Assert.Equal((double)(ThreadCount * PerThread), measurements.Sum(m => m.Value));
    }

    /// <summary>
    /// 多线程并发写直方图不丢测量值
    /// </summary>
    [Fact]
    public void RecordHistogram_FromMultipleThreads_EmitsEveryMeasurement()
    {
        const int ThreadCount = 6;
        const int PerThread = 200;

        var name = NewMetricName();
        using var capture = new MeasurementCapture(name);
        using var collector = new MetricsCollector();

        var threads = new Thread[ThreadCount];
        for (var i = 0; i < ThreadCount; i++)
        {
            threads[i] = new Thread(() =>
            {
                for (var j = 0; j < PerThread; j++)
                {
                    collector.RecordHistogram(name, 1.5d);
                }
            });
        }

        foreach (var thread in threads)
        {
            thread.Start();
        }

        foreach (var thread in threads)
        {
            thread.Join();
        }

        var measurements = capture.Measurements;
        Assert.Equal(ThreadCount * PerThread, measurements.Count);
        Assert.All(measurements, m => Assert.Equal(1.5d, m.Value));
    }

    /// <summary>
    /// 指标不再内存留存，获取指标恒返回空列表
    /// </summary>
    [Fact]
    public void GetMetrics_AfterRecording_ReturnsEmptyList()
    {
        using var collector = new MetricsCollector();

        collector.RecordCounter(NewMetricName());
        collector.RecordHistogram(NewMetricName(), 1d);

        var metrics = collector.GetMetrics();
        Assert.NotNull(metrics);
        Assert.Empty(metrics);
    }

    /// <summary>
    /// 清空是空操作，调用后仍可继续记录
    /// </summary>
    [Fact]
    public void Clear_AfterRecording_IsNoOpAndKeepsCollectorUsable()
    {
        var name = NewMetricName();
        using var capture = new MeasurementCapture(name);
        using var collector = new MetricsCollector();

        collector.RecordCounter(name);
        collector.Clear();
        collector.RecordCounter(name);

        Assert.Empty(collector.GetMetrics());
        Assert.Equal(2, capture.Measurements.Count);
    }

    /// <summary>
    /// 重复释放不抛异常
    /// </summary>
    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var collector = new MetricsCollector();

        collector.Dispose();
        collector.Dispose();

        Assert.Empty(collector.GetMetrics());
    }

    /// <summary>
    /// 释放后继续记录已存在的指标不抛异常
    /// </summary>
    [Fact]
    public void RecordCounter_AfterDispose_DoesNotThrow()
    {
        var name = NewMetricName();
        var collector = new MetricsCollector();
        collector.RecordCounter(name);

        collector.Dispose();
        collector.RecordCounter(name);

        Assert.Empty(collector.GetMetrics());
    }

    /// <summary>
    /// 生成本用例专属的指标名，避免与并行用例共享 Meter 时互相污染
    /// </summary>
    private static string NewMetricName()
    {
        return $"xihan.tests.{Guid.NewGuid():N}";
    }

    /// <summary>
    /// 捕获到的单条测量事件
    /// </summary>
    private sealed record CapturedMeasurement(
        string InstrumentName,
        double Value,
        bool IsCounter,
        bool IsHistogram,
        IReadOnlyDictionary<string, string?> Tags);

    /// <summary>
    /// 指标测量事件捕获器：只订阅指定名字的仪表，避免并行用例串味
    /// </summary>
    private sealed class MeasurementCapture : IDisposable
    {
        private readonly MeterListener _listener = new();
        private readonly ConcurrentQueue<CapturedMeasurement> _measurements = new();
        private readonly HashSet<string> _instrumentNames;
        private int _publishedCount;

        public MeasurementCapture(params string[] instrumentNames)
        {
            _instrumentNames = new HashSet<string>(instrumentNames, StringComparer.Ordinal);

            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name != MetricsCollector.MeterName || !_instrumentNames.Contains(instrument.Name))
                {
                    return;
                }

                Interlocked.Increment(ref _publishedCount);
                listener.EnableMeasurementEvents(instrument, null);
            };

            _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) => Capture(instrument, measurement, tags));
            _listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) => Capture(instrument, measurement, tags));
            _listener.Start();
        }

        /// <summary>
        /// 已捕获的测量事件（按发生顺序）
        /// </summary>
        public IReadOnlyList<CapturedMeasurement> Measurements => _measurements.ToArray();

        /// <summary>
        /// 被订阅的仪表实例数（仪表创建时发布一次）
        /// </summary>
        public int PublishedCount => Volatile.Read(ref _publishedCount);

        public void Dispose()
        {
            _listener.Dispose();
        }

        private void Capture(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var captured = new CapturedMeasurement(
                instrument.Name,
                value,
                instrument is Counter<long>,
                instrument is Histogram<double>,
                ToDictionary(tags));

            _measurements.Enqueue(captured);
        }

        private static Dictionary<string, string?> ToDictionary(ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var dictionary = new Dictionary<string, string?>(tags.Length, StringComparer.Ordinal);
            foreach (var tag in tags)
            {
                dictionary[tag.Key] = tag.Value?.ToString();
            }

            return dictionary;
        }
    }
}
