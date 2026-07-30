// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace XiHan.Framework.Observability.Metrics;

/// <summary>
/// 指标收集器实现
/// </summary>
/// <remarks>
/// 基于 System.Diagnostics.Metrics.Meter：Record* 直出 OTel 指标管道（经 WithMetrics().AddMeter(<see cref="MeterName"/>) + exporter 导出到 OTLP/Prometheus）。
/// 不再内存留存（消除原 ConcurrentBag 无界增长）；GetMetrics/Clear 保留接口成员向后兼容，返回空/空操作。
/// </remarks>
public class MetricsCollector : IMetricsCollector, IDisposable
{
    /// <summary>
    /// OTel Meter 名（WithMetrics().AddMeter 时使用）
    /// </summary>
    public const string MeterName = "XiHan.Metrics";

    private readonly Meter _meter = new(MeterName);
    private readonly ConcurrentDictionary<string, Counter<long>> _counters = new();
    private readonly ConcurrentDictionary<string, Histogram<double>> _histograms = new();

    /// <summary>
    /// 记录计数器
    /// </summary>
    public void RecordCounter(string name, long value = 1, Dictionary<string, string>? tags = null)
    {
        var counter = _counters.GetOrAdd(name, static (n, meter) => meter.CreateCounter<long>(n), _meter);
        counter.Add(value, ToTagList(tags));
    }

    /// <summary>
    /// 记录测量值
    /// </summary>
    public void RecordMeasurement(string name, double value, Dictionary<string, string>? tags = null)
    {
        // 无 pull 型 gauge 回调上下文，用 Histogram 承载瞬时测量值（分布/百分位由后端聚合）
        RecordHistogram(name, value, tags);
    }

    /// <summary>
    /// 记录直方图
    /// </summary>
    public void RecordHistogram(string name, double value, Dictionary<string, string>? tags = null)
    {
        var histogram = _histograms.GetOrAdd(name, static (n, meter) => meter.CreateHistogram<double>(n), _meter);
        histogram.Record(value, ToTagList(tags));
    }

    /// <summary>
    /// 开始计时
    /// </summary>
    public IDisposable BeginTimer(string name, Dictionary<string, string>? tags = null)
    {
        return new MetricTimer(this, name, tags);
    }

    /// <summary>
    /// 获取所有指标（指标已直出 OTel 导出器，不再内存留存；保留成员向后兼容）
    /// </summary>
    public IReadOnlyList<MetricData> GetMetrics()
    {
        return [];
    }

    /// <summary>
    /// 清空指标（无内存留存，空操作；保留成员向后兼容）
    /// </summary>
    public void Clear()
    {
    }

    /// <summary>
    /// 释放 Meter
    /// </summary>
    public void Dispose()
    {
        _meter.Dispose();
        GC.SuppressFinalize(this);
    }

    private static TagList ToTagList(Dictionary<string, string>? tags)
    {
        var list = new TagList();
        if (tags is not null)
        {
            foreach (var tag in tags)
            {
                list.Add(tag.Key, tag.Value);
            }
        }

        return list;
    }

    /// <summary>
    /// 指标计时器：Dispose 时把耗时记入直方图
    /// </summary>
    private sealed class MetricTimer : IDisposable
    {
        private readonly MetricsCollector _collector;
        private readonly string _name;
        private readonly Dictionary<string, string>? _tags;
        private readonly long _startTimestamp;

        public MetricTimer(MetricsCollector collector, string name, Dictionary<string, string>? tags)
        {
            _collector = collector;
            _name = name;
            _tags = tags;
            _startTimestamp = Stopwatch.GetTimestamp();
        }

        public void Dispose()
        {
            var elapsed = Stopwatch.GetElapsedTime(_startTimestamp);
            _collector.RecordHistogram($"{_name}.duration", elapsed.TotalMilliseconds, _tags);
        }
    }
}
