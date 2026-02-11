#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PerformanceMonitor
// Guid:d9e0f1a2-b3c4-45d6-e7f8-a9b0c1d2e3f4
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/26 04:04:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using System.Diagnostics;

namespace XiHan.Framework.Observability.Performance;

/// <summary>
/// 性能监控实现
/// </summary>
public class PerformanceMonitor : IPerformanceMonitor
{
    private readonly ConcurrentBag<PerformanceRecord> _records = new();

    /// <summary>
    /// 开始监控操作
    /// </summary>
    public IPerformanceTracker BeginOperation(string operationName)
    {
        return new PerformanceTracker(this, operationName);
    }

    /// <summary>
    /// 获取性能统计
    /// </summary>
    public PerformanceStatistics GetStatistics()
    {
        var records = _records.ToArray();
        if (records.Length == 0)
        {
            return new PerformanceStatistics();
        }

        var durations = records.Select(r => r.DurationMs).OrderBy(d => d).ToArray();
        var operationGroups = records.GroupBy(r => r.OperationName);

        return new PerformanceStatistics
        {
            TotalOperations = records.Length,
            SuccessfulOperations = records.Count(r => r.Success),
            FailedOperations = records.Count(r => !r.Success),
            AverageDurationMs = durations.Average(),
            MinDurationMs = durations.Min(),
            MaxDurationMs = durations.Max(),
            P50DurationMs = GetPercentile(durations, 0.5),
            P95DurationMs = GetPercentile(durations, 0.95),
            P99DurationMs = GetPercentile(durations, 0.99),
            OperationStats = operationGroups.ToDictionary(
                g => g.Key,
                g => new OperationStatistics
                {
                    OperationName = g.Key,
                    Count = g.Count(),
                    AverageDurationMs = g.Average(r => r.DurationMs),
                    MinDurationMs = g.Min(r => r.DurationMs),
                    MaxDurationMs = g.Max(r => r.DurationMs)
                })
        };
    }

    /// <summary>
    /// 获取慢操作列表
    /// </summary>
    public IReadOnlyList<PerformanceRecord> GetSlowOperations(double thresholdMs = 1000)
    {
        return _records.Where(r => r.DurationMs >= thresholdMs)
            .OrderByDescending(r => r.DurationMs)
            .ToArray();
    }

    /// <summary>
    /// 清空性能记录
    /// </summary>
    public void Clear()
    {
        _records.Clear();
    }

    /// <summary>
    /// 添加性能记录
    /// </summary>
    internal void AddRecord(PerformanceRecord record)
    {
        _records.Add(record);
    }

    /// <summary>
    /// 计算百分位数
    /// </summary>
    private static double GetPercentile(double[] sortedData, double percentile)
    {
        if (sortedData.Length == 0)
        {
            return 0;
        }

        var index = (int)Math.Ceiling(percentile * sortedData.Length) - 1;
        index = Math.Max(0, Math.Min(index, sortedData.Length - 1));
        return sortedData[index];
    }

    /// <summary>
    /// 性能追踪器实现
    /// </summary>
    private class PerformanceTracker : IPerformanceTracker
    {
        private readonly PerformanceMonitor _monitor;
        private readonly PerformanceRecord _record;
        private readonly Stopwatch _stopwatch;

        public string OperationName => _record.OperationName;

        public PerformanceTracker(PerformanceMonitor monitor, string operationName)
        {
            _monitor = monitor;
            _record = new PerformanceRecord
            {
                OperationName = operationName,
                StartTime = DateTimeOffset.UtcNow
            };
            _stopwatch = Stopwatch.StartNew();
        }

        public void AddTag(string key, string value)
        {
            _record.Tags[key] = value;
        }

        public void Checkpoint(string checkpointName)
        {
            _record.Checkpoints.Add(new Checkpoint
            {
                Name = checkpointName,
                Timestamp = DateTimeOffset.UtcNow,
                ElapsedMs = _stopwatch.Elapsed.TotalMilliseconds
            });
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _record.EndTime = DateTimeOffset.UtcNow;
            _record.DurationMs = _stopwatch.Elapsed.TotalMilliseconds;
            _monitor.AddRecord(_record);
        }
    }
}
