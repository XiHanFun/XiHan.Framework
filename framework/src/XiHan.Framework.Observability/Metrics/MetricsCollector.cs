#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:MetricsCollector
// Guid:a6b7c8d9-e0f1-42a3-b4c5-d6e7f8a9b0c1
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/26 4:02:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using System.Diagnostics;

namespace XiHan.Framework.Observability.Metrics;

/// <summary>
/// 指标收集器实现
/// </summary>
public class MetricsCollector : IMetricsCollector
{
    private readonly ConcurrentBag<MetricData> _metrics = new();

    /// <summary>
    /// 记录计数器
    /// </summary>
    public void RecordCounter(string name, long value = 1, Dictionary<string, string>? tags = null)
    {
        _metrics.Add(new MetricData
        {
            Name = name,
            Type = MetricType.Counter,
            Value = value,
            Tags = tags ?? new Dictionary<string, string>(),
            Timestamp = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    /// 记录测量值
    /// </summary>
    public void RecordMeasurement(string name, double value, Dictionary<string, string>? tags = null)
    {
        _metrics.Add(new MetricData
        {
            Name = name,
            Type = MetricType.Gauge,
            Value = value,
            Tags = tags ?? new Dictionary<string, string>(),
            Timestamp = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    /// 记录直方图
    /// </summary>
    public void RecordHistogram(string name, double value, Dictionary<string, string>? tags = null)
    {
        _metrics.Add(new MetricData
        {
            Name = name,
            Type = MetricType.Histogram,
            Value = value,
            Tags = tags ?? new Dictionary<string, string>(),
            Timestamp = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    /// 开始计时
    /// </summary>
    public IDisposable BeginTimer(string name, Dictionary<string, string>? tags = null)
    {
        return new MetricTimer(this, name, tags);
    }

    /// <summary>
    /// 获取所有指标
    /// </summary>
    public IReadOnlyList<MetricData> GetMetrics()
    {
        return _metrics.ToArray();
    }

    /// <summary>
    /// 清空指标
    /// </summary>
    public void Clear()
    {
        _metrics.Clear();
    }

    /// <summary>
    /// 指标计时器
    /// </summary>
    private class MetricTimer : IDisposable
    {
        private readonly MetricsCollector _collector;
        private readonly string _name;
        private readonly Dictionary<string, string>? _tags;
        private readonly Stopwatch _stopwatch;

        public MetricTimer(MetricsCollector collector, string name, Dictionary<string, string>? tags)
        {
            _collector = collector;
            _name = name;
            _tags = tags;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _collector.RecordHistogram($"{_name}.duration", _stopwatch.Elapsed.TotalMilliseconds, _tags);
        }
    }
}
