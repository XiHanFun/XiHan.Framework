#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:StopwatchHelper
// Guid:8a5b2c4d-3e1f-4a5b-9c8d-7e6f5a4b3c2d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/19 10:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using System.Diagnostics;

namespace XiHan.Framework.Utils.Timing;

/// <summary>
/// 秒表辅助工具类
/// </summary>
/// <remarks>
/// 提供方便的计时操作、性能监控、代码执行时间测量等功能，
/// 支持命名计时器、统计信息、线程安全操作
/// </remarks>
public static class StopwatchHelper
{
    #region 私有字段

    /// <summary>
    /// 命名计时器集合（线程安全）
    /// </summary>
    private static readonly ConcurrentDictionary<string, Stopwatch> NamedStopwatches = new();

    /// <summary>
    /// 计时统计信息集合（线程安全）
    /// </summary>
    private static readonly ConcurrentDictionary<string, TimingStatistics> Statistics = new();

    #endregion

    #region 基本计时操作

    /// <summary>
    /// 创建并启动一个新的计时器
    /// </summary>
    /// <returns>已启动的计时器</returns>
    public static Stopwatch StartNew()
    {
        return Stopwatch.StartNew();
    }

    /// <summary>
    /// 启动一个命名计时器
    /// </summary>
    /// <param name="name">计时器名称</param>
    /// <returns>是否成功启动（如果已存在同名计时器则返回false）</returns>
    public static bool StartNamed(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("计时器名称不能为空", nameof(name));
        }

        var stopwatch = new Stopwatch();
        var added = NamedStopwatches.TryAdd(name, stopwatch);
        if (added)
        {
            stopwatch.Start();
        }
        return added;
    }

    /// <summary>
    /// 重新启动一个命名计时器（如果存在则重置并重新开始）
    /// </summary>
    /// <param name="name">计时器名称</param>
    public static void RestartNamed(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("计时器名称不能为空", nameof(name));
        }

        var stopwatch = NamedStopwatches.AddOrUpdate(
            name,
            _ => Stopwatch.StartNew(),
            (_, existing) =>
            {
                existing.Restart();
                return existing;
            });
    }

    /// <summary>
    /// 停止指定的命名计时器
    /// </summary>
    /// <param name="name">计时器名称</param>
    /// <returns>如果计时器存在则返回经过的时间，否则返回null</returns>
    public static TimeSpan? StopNamed(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        if (NamedStopwatches.TryGetValue(name, out var stopwatch))
        {
            stopwatch.Stop();
            return stopwatch.Elapsed;
        }

        return null;
    }

    /// <summary>
    /// 获取指定命名计时器的当前经过时间（不停止计时器）
    /// </summary>
    /// <param name="name">计时器名称</param>
    /// <returns>如果计时器存在则返回经过的时间，否则返回null</returns>
    public static TimeSpan? GetElapsed(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return NamedStopwatches.TryGetValue(name, out var stopwatch) ? stopwatch.Elapsed : null;
    }

    /// <summary>
    /// 移除指定的命名计时器
    /// </summary>
    /// <param name="name">计时器名称</param>
    /// <returns>如果成功移除则返回经过的时间，否则返回null</returns>
    public static TimeSpan? RemoveNamed(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        if (NamedStopwatches.TryRemove(name, out var stopwatch))
        {
            stopwatch.Stop();
            return stopwatch.Elapsed;
        }

        return null;
    }

    /// <summary>
    /// 清除所有命名计时器
    /// </summary>
    public static void ClearAll()
    {
        foreach (var stopwatch in NamedStopwatches.Values)
        {
            stopwatch.Stop();
        }
        NamedStopwatches.Clear();
        Statistics.Clear();
    }

    #endregion

    #region 代码执行时间测量

    /// <summary>
    /// 测量操作的执行时间
    /// </summary>
    /// <param name="action">要测量的操作</param>
    /// <returns>操作执行的时间</returns>
    /// <exception cref="ArgumentNullException">操作为null时抛出</exception>
    public static TimeSpan Measure(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var stopwatch = Stopwatch.StartNew();
        action();
        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    /// <summary>
    /// 测量异步操作的执行时间
    /// </summary>
    /// <param name="asyncAction">要测量的异步操作</param>
    /// <returns>操作执行的时间</returns>
    /// <exception cref="ArgumentNullException">操作为null时抛出</exception>
    public static async Task<TimeSpan> MeasureAsync(Func<Task> asyncAction)
    {
        ArgumentNullException.ThrowIfNull(asyncAction);

        var stopwatch = Stopwatch.StartNew();
        await asyncAction();
        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    /// <summary>
    /// 测量函数的执行时间并返回结果
    /// </summary>
    /// <typeparam name="T">返回值类型</typeparam>
    /// <param name="func">要测量的函数</param>
    /// <returns>包含结果和执行时间的元组</returns>
    /// <exception cref="ArgumentNullException">函数为null时抛出</exception>
    public static (T Result, TimeSpan Elapsed) MeasureWithResult<T>(Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(func);

        var stopwatch = Stopwatch.StartNew();
        var result = func();
        stopwatch.Stop();
        return (result, stopwatch.Elapsed);
    }

    /// <summary>
    /// 测量异步函数的执行时间并返回结果
    /// </summary>
    /// <typeparam name="T">返回值类型</typeparam>
    /// <param name="asyncFunc">要测量的异步函数</param>
    /// <returns>包含结果和执行时间的元组</returns>
    /// <exception cref="ArgumentNullException">函数为null时抛出</exception>
    public static async Task<(T Result, TimeSpan Elapsed)> MeasureWithResultAsync<T>(Func<Task<T>> asyncFunc)
    {
        ArgumentNullException.ThrowIfNull(asyncFunc);

        var stopwatch = Stopwatch.StartNew();
        var result = await asyncFunc();
        stopwatch.Stop();
        return (result, stopwatch.Elapsed);
    }

    #endregion

    #region 性能监控与统计

    /// <summary>
    /// 记录操作的执行时间到统计信息中
    /// </summary>
    /// <param name="operationName">操作名称</param>
    /// <param name="action">要执行的操作</param>
    /// <returns>操作执行的时间</returns>
    public static TimeSpan MeasureAndRecord(string operationName, Action action)
    {
        if (string.IsNullOrWhiteSpace(operationName))
        {
            throw new ArgumentException("操作名称不能为空", nameof(operationName));
        }

        var elapsed = Measure(action);
        RecordTiming(operationName, elapsed);
        return elapsed;
    }

    /// <summary>
    /// 记录异步操作的执行时间到统计信息中
    /// </summary>
    /// <param name="operationName">操作名称</param>
    /// <param name="asyncAction">要执行的异步操作</param>
    /// <returns>操作执行的时间</returns>
    public static async Task<TimeSpan> MeasureAndRecordAsync(string operationName, Func<Task> asyncAction)
    {
        if (string.IsNullOrWhiteSpace(operationName))
        {
            throw new ArgumentException("操作名称不能为空", nameof(operationName));
        }

        var elapsed = await MeasureAsync(asyncAction);
        RecordTiming(operationName, elapsed);
        return elapsed;
    }

    /// <summary>
    /// 记录执行时间到统计信息中
    /// </summary>
    /// <param name="operationName">操作名称</param>
    /// <param name="elapsed">执行时间</param>
    public static void RecordTiming(string operationName, TimeSpan elapsed)
    {
        if (string.IsNullOrWhiteSpace(operationName))
        {
            throw new ArgumentException("操作名称不能为空", nameof(operationName));
        }

        Statistics.AddOrUpdate(
            operationName,
            new TimingStatistics(operationName, elapsed),
            (_, existing) =>
            {
                existing.AddTiming(elapsed);
                return existing;
            });
    }

    /// <summary>
    /// 获取指定操作的统计信息
    /// </summary>
    /// <param name="operationName">操作名称</param>
    /// <returns>统计信息，如果不存在则返回null</returns>
    public static TimingStatistics? GetStatistics(string operationName)
    {
        if (string.IsNullOrWhiteSpace(operationName))
        {
            return null;
        }

        return Statistics.TryGetValue(operationName, out var stats) ? stats : null;
    }

    /// <summary>
    /// 获取所有操作的统计信息
    /// </summary>
    /// <returns>所有统计信息的副本</returns>
    public static Dictionary<string, TimingStatistics> GetAllStatistics()
    {
        return Statistics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Clone());
    }

    /// <summary>
    /// 清除指定操作的统计信息
    /// </summary>
    /// <param name="operationName">操作名称</param>
    /// <returns>是否成功清除</returns>
    public static bool ClearStatistics(string operationName)
    {
        if (string.IsNullOrWhiteSpace(operationName))
        {
            return false;
        }

        return Statistics.TryRemove(operationName, out _);
    }

    /// <summary>
    /// 清除所有统计信息
    /// </summary>
    public static void ClearAllStatistics()
    {
        Statistics.Clear();
    }

    #endregion

    #region 格式化输出

    /// <summary>
    /// 格式化时间跨度为人类可读的字符串
    /// </summary>
    /// <param name="timeSpan">时间跨度</param>
    /// <param name="precision">精度级别</param>
    /// <returns>格式化的时间字符串</returns>
    public static string FormatElapsed(TimeSpan timeSpan, TimePrecision precision = TimePrecision.Milliseconds)
    {
        return precision switch
        {
            TimePrecision.Nanoseconds => $"{timeSpan.Ticks * 100:N0} ns",
            TimePrecision.Microseconds => $"{timeSpan.TotalMicroseconds:F2} μs",
            TimePrecision.Milliseconds => timeSpan.TotalMilliseconds switch
            {
                < 1 => $"{timeSpan.TotalMicroseconds:F2} μs",
                < 1000 => $"{timeSpan.TotalMilliseconds:F2} ms",
                _ => $"{timeSpan.TotalSeconds:F2} s"
            },
            TimePrecision.Seconds => $"{timeSpan.TotalSeconds:F2} s",
            TimePrecision.Auto => timeSpan.TotalMilliseconds switch
            {
                < 0.001 => $"{timeSpan.Ticks * 100:N0} ns",
                < 1 => $"{timeSpan.TotalMicroseconds:F2} μs",
                < 1000 => $"{timeSpan.TotalMilliseconds:F2} ms",
                < 60000 => $"{timeSpan.TotalSeconds:F2} s",
                _ => $"{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}.{timeSpan.Milliseconds:D3}"
            },
            _ => timeSpan.ToString()
        };
    }

    /// <summary>
    /// 生成性能统计报告
    /// </summary>
    /// <param name="includeDetails">是否包含详细信息</param>
    /// <returns>格式化的统计报告</returns>
    public static string GenerateReport(bool includeDetails = true)
    {
        if (Statistics.IsEmpty)
        {
            return "没有可用的性能统计数据。";
        }

        var report = new List<string>
        {
            "=== 性能统计报告 ===",
            $"统计时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            $"操作总数: {Statistics.Count}",
            ""
        };

        var sortedStats = Statistics.Values.OrderByDescending(s => s.TotalTime).ToList();

        foreach (var stat in sortedStats)
        {
            report.Add($"操作: {stat.OperationName}");
            report.Add($"  执行次数: {stat.ExecutionCount:N0}");
            report.Add($"  总时间: {FormatElapsed(stat.TotalTime)}");
            report.Add($"  平均时间: {FormatElapsed(stat.AverageTime)}");
            report.Add($"  最小时间: {FormatElapsed(stat.MinTime)}");
            report.Add($"  最大时间: {FormatElapsed(stat.MaxTime)}");

            if (includeDetails && stat.ExecutionCount > 1)
            {
                var standardDeviation = stat.CalculateStandardDeviation();
                report.Add($"  标准差: {FormatElapsed(standardDeviation)}");
            }

            report.Add("");
        }

        return string.Join(Environment.NewLine, report);
    }

    #endregion

    #region 便捷使用方法

    /// <summary>
    /// 创建一个一次性的计时器，用于using语句
    /// </summary>
    /// <param name="operationName">操作名称（可选）</param>
    /// <param name="onCompleted">完成时的回调</param>
    /// <returns>一次性计时器</returns>
    public static DisposableStopwatch CreateDisposable(string? operationName = null, Action<TimeSpan>? onCompleted = null)
    {
        return new DisposableStopwatch(operationName, onCompleted);
    }

    /// <summary>
    /// 批量执行操作并测量性能
    /// </summary>
    /// <param name="operationName">操作名称</param>
    /// <param name="action">要执行的操作</param>
    /// <param name="iterations">执行次数</param>
    /// <returns>批量执行的统计信息</returns>
    public static TimingStatistics Benchmark(string operationName, Action action, int iterations = 1000)
    {
        if (string.IsNullOrWhiteSpace(operationName))
        {
            throw new ArgumentException("操作名称不能为空", nameof(operationName));
        }

        ArgumentNullException.ThrowIfNull(action);

        if (iterations <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(iterations), "执行次数必须大于0");
        }

        var results = new List<TimeSpan>(iterations);

        // 预热
        action();

        // 正式测量
        for (var i = 0; i < iterations; i++)
        {
            var elapsed = Measure(action);
            results.Add(elapsed);
        }

        var stats = new TimingStatistics(operationName);
        foreach (var elapsed in results)
        {
            stats.AddTiming(elapsed);
        }

        return stats;
    }

    #endregion
}

/// <summary>
/// 计时统计信息
/// </summary>
public class TimingStatistics
{
    private readonly List<TimeSpan> _timings = [];
    private readonly Lock _lock = new();

    /// <summary>
    /// 初始化计时统计信息
    /// </summary>
    /// <param name="operationName">操作名称</param>
    public TimingStatistics(string operationName)
    {
        OperationName = operationName;
    }

    /// <summary>
    /// 初始化计时统计信息并添加第一个计时记录
    /// </summary>
    /// <param name="operationName">操作名称</param>
    /// <param name="initialTiming">初始计时</param>
    public TimingStatistics(string operationName, TimeSpan initialTiming) : this(operationName)
    {
        AddTiming(initialTiming);
    }

    /// <summary>
    /// 操作名称
    /// </summary>
    public string OperationName { get; }

    /// <summary>
    /// 执行次数
    /// </summary>
    public int ExecutionCount
    {
        get
        {
            lock (_lock)
            {
                return _timings.Count;
            }
        }
    }

    /// <summary>
    /// 总执行时间
    /// </summary>
    public TimeSpan TotalTime
    {
        get
        {
            lock (_lock)
            {
                return _timings.Aggregate(TimeSpan.Zero, (sum, t) => sum + t);
            }
        }
    }

    /// <summary>
    /// 平均执行时间
    /// </summary>
    public TimeSpan AverageTime
    {
        get
        {
            lock (_lock)
            {
                if (_timings.Count == 0)
                {
                    return TimeSpan.Zero;
                }

                return TimeSpan.FromTicks(TotalTime.Ticks / _timings.Count);
            }
        }
    }

    /// <summary>
    /// 最小执行时间
    /// </summary>
    public TimeSpan MinTime
    {
        get
        {
            lock (_lock)
            {
                return _timings.Count == 0 ? TimeSpan.Zero : _timings.Min();
            }
        }
    }

    /// <summary>
    /// 最大执行时间
    /// </summary>
    public TimeSpan MaxTime
    {
        get
        {
            lock (_lock)
            {
                return _timings.Count == 0 ? TimeSpan.Zero : _timings.Max();
            }
        }
    }

    /// <summary>
    /// 添加一个计时记录
    /// </summary>
    /// <param name="timing">计时</param>
    public void AddTiming(TimeSpan timing)
    {
        lock (_lock)
        {
            _timings.Add(timing);
        }
    }

    /// <summary>
    /// 计算标准差
    /// </summary>
    /// <returns>标准差</returns>
    public TimeSpan CalculateStandardDeviation()
    {
        lock (_lock)
        {
            if (_timings.Count <= 1)
            {
                return TimeSpan.Zero;
            }

            var avgTicks = AverageTime.Ticks;
            var sumSquaredDiffs = _timings.Sum(t => Math.Pow(t.Ticks - avgTicks, 2));
            var variance = sumSquaredDiffs / (_timings.Count - 1);
            var standardDeviation = Math.Sqrt(variance);

            return TimeSpan.FromTicks((long)standardDeviation);
        }
    }

    /// <summary>
    /// 获取指定百分位数的执行时间
    /// </summary>
    /// <param name="percentile">百分位数 (0-100)</param>
    /// <returns>百分位数对应的执行时间</returns>
    public TimeSpan GetPercentile(double percentile)
    {
        if (percentile is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percentile), "百分位数必须在0-100之间");
        }

        lock (_lock)
        {
            if (_timings.Count == 0)
            {
                return TimeSpan.Zero;
            }

            var sortedTimings = _timings.OrderBy(t => t).ToList();
            var index = (int)Math.Ceiling(percentile / 100.0 * sortedTimings.Count) - 1;
            index = Math.Max(0, Math.Min(index, sortedTimings.Count - 1));

            return sortedTimings[index];
        }
    }

    /// <summary>
    /// 克隆统计信息
    /// </summary>
    /// <returns>统计信息的副本</returns>
    public TimingStatistics Clone()
    {
        lock (_lock)
        {
            var clone = new TimingStatistics(OperationName);
            foreach (var timing in _timings)
            {
                clone.AddTiming(timing);
            }
            return clone;
        }
    }

    /// <summary>
    /// 转换为字符串表示
    /// </summary>
    /// <returns>格式化的统计信息</returns>
    public override string ToString()
    {
        return $"{OperationName}: {ExecutionCount}次, 平均{StopwatchHelper.FormatElapsed(AverageTime)}, 总计{StopwatchHelper.FormatElapsed(TotalTime)}";
    }
}

/// <summary>
/// 时间精度枚举
/// </summary>
public enum TimePrecision
{
    /// <summary>
    /// 纳秒
    /// </summary>
    Nanoseconds,

    /// <summary>
    /// 微秒
    /// </summary>
    Microseconds,

    /// <summary>
    /// 毫秒
    /// </summary>
    Milliseconds,

    /// <summary>
    /// 秒
    /// </summary>
    Seconds,

    /// <summary>
    /// 自动选择最合适的精度
    /// </summary>
    Auto
}

/// <summary>
/// 一次性计时器，支持using语句
/// </summary>
public class DisposableStopwatch : IDisposable
{
    private readonly Stopwatch _stopwatch;
    private readonly string? _operationName;
    private readonly Action<TimeSpan>? _onCompleted;

    /// <summary>
    /// 初始化一次性计时器
    /// </summary>
    /// <param name="operationName">操作名称</param>
    /// <param name="onCompleted">完成时的回调</param>
    public DisposableStopwatch(string? operationName = null, Action<TimeSpan>? onCompleted = null)
    {
        _operationName = operationName;
        _onCompleted = onCompleted;
        _stopwatch = Stopwatch.StartNew();
    }

    /// <summary>
    /// 经过的时间
    /// </summary>
    public TimeSpan Elapsed => _stopwatch.Elapsed;

    /// <summary>
    /// 是否正在运行
    /// </summary>
    public bool IsRunning => _stopwatch.IsRunning;

    /// <summary>
    /// 停止计时器
    /// </summary>
    public void Stop()
    {
        _stopwatch.Stop();
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_stopwatch.IsRunning)
        {
            _stopwatch.Stop();
        }

        var elapsed = _stopwatch.Elapsed;

        // 记录到统计信息
        if (!string.IsNullOrWhiteSpace(_operationName))
        {
            StopwatchHelper.RecordTiming(_operationName, elapsed);
        }

        // 执行回调
        _onCompleted?.Invoke(elapsed);
    }
}
