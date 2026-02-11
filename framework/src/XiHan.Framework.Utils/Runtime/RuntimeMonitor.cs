#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:RuntimeMonitor
// Guid:a5b3c2d1-e4f6-7890-abcd-ef1234567890
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/07/27 10:21:25
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using System.Diagnostics;

namespace XiHan.Framework.Utils.Runtime;

/// <summary>
/// 运行时监控器
/// </summary>
public class RuntimeMonitor : IDisposable
{
    private readonly Process _currentProcess;
    private readonly ConcurrentQueue<PerformanceSnapshot> _snapshots;
    private readonly Timer? _monitorTimer;
    private readonly Lock _lockObject = new();
    private readonly int _maxSnapshotCount;
    private volatile bool _isDisposed;
    private DateTime _lastCpuTime;
    private TimeSpan _lastTotalProcessorTime;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="monitorInterval">监控间隔，默认5秒</param>
    /// <param name="maxSnapshotCount">最大快照数量，默认1000</param>
    public RuntimeMonitor(TimeSpan? monitorInterval = null, int maxSnapshotCount = 1000)
    {
        _currentProcess = Process.GetCurrentProcess();
        _snapshots = new ConcurrentQueue<PerformanceSnapshot>();
        _maxSnapshotCount = maxSnapshotCount;
        MonitorInterval = monitorInterval ?? TimeSpan.FromSeconds(5);

        // 初始化CPU时间基准
        _lastCpuTime = DateTime.UtcNow;
        _lastTotalProcessorTime = _currentProcess.TotalProcessorTime;

        // 创建定时器但不立即启动
        _monitorTimer = new Timer(CollectSnapshot, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    /// 监控间隔
    /// </summary>
    public TimeSpan MonitorInterval { get; }

    /// <summary>
    /// 是否正在监控
    /// </summary>
    public bool IsMonitoring { get; private set; }

    /// <summary>
    /// 快照历史记录数量
    /// </summary>
    public int SnapshotCount => _snapshots.Count;

    /// <summary>
    /// 监控开始时间
    /// </summary>
    public DateTime? MonitorStartTime { get; private set; }

    /// <summary>
    /// 强制垃圾回收
    /// </summary>
    /// <param name="generation">GC代数，-1表示全部代</param>
    /// <param name="mode">GC模式</param>
    public static void ForceGarbageCollection(int generation = -1, GCCollectionMode mode = GCCollectionMode.Default)
    {
        if (generation == -1)
        {
            GC.Collect();
        }
        else
        {
            GC.Collect(generation, mode);
        }

        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    /// <summary>
    /// 获取内存压力信息
    /// </summary>
    /// <returns>内存压力信息</returns>
    public static string GetMemoryPressureInfo()
    {
        var info = GC.GetGCMemoryInfo();
        return $"总内存: {info.TotalAvailableMemoryBytes / 1024 / 1024} MB, " +
               $"高内存负载阈值: {info.HighMemoryLoadThresholdBytes / 1024 / 1024} MB, " +
               $"堆大小: {info.HeapSizeBytes / 1024 / 1024} MB, " +
               $"内存负载: {info.MemoryLoadBytes / 1024 / 1024} MB";
    }

    /// <summary>
    /// 开始监控
    /// </summary>
    public void StartMonitoring()
    {
        if (IsMonitoring || _isDisposed)
        {
            return;
        }

        lock (_lockObject)
        {
            if (IsMonitoring || _isDisposed)
            {
                return;
            }

            IsMonitoring = true;
            MonitorStartTime = DateTime.Now;

            // 立即收集一次快照
            CollectSnapshot(null);

            // 启动定时器
            _monitorTimer?.Change(MonitorInterval, MonitorInterval);
        }
    }

    /// <summary>
    /// 停止监控
    /// </summary>
    public void StopMonitoring()
    {
        if (!IsMonitoring)
        {
            return;
        }

        lock (_lockObject)
        {
            if (!IsMonitoring)
            {
                return;
            }

            IsMonitoring = false;
            _monitorTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    /// <summary>
    /// 获取当前性能快照
    /// </summary>
    /// <returns>当前性能快照</returns>
    public PerformanceSnapshot GetCurrentSnapshot()
    {
        return CreateSnapshot();
    }

    /// <summary>
    /// 获取最新的性能快照
    /// </summary>
    /// <returns>最新的性能快照，如果没有则返回null</returns>
    public PerformanceSnapshot? GetLatestSnapshot()
    {
        return _snapshots.TryPeek(out var snapshot) ? snapshot : null;
    }

    /// <summary>
    /// 获取所有性能快照
    /// </summary>
    /// <returns>性能快照列表</returns>
    public List<PerformanceSnapshot> GetAllSnapshots()
    {
        return [.. _snapshots];
    }

    /// <summary>
    /// 获取指定时间范围内的快照
    /// </summary>
    /// <param name="timeRange">时间范围</param>
    /// <returns>快照列表</returns>
    public List<PerformanceSnapshot> GetSnapshotsInTimeRange(TimeSpan timeRange)
    {
        var cutoffTime = DateTime.Now - timeRange;
        return [.. _snapshots.Where(s => s.Timestamp >= cutoffTime)];
    }

    /// <summary>
    /// 分析性能趋势
    /// </summary>
    /// <param name="counterType">计数器类型</param>
    /// <param name="timeRange">分析时间范围，null表示分析所有数据</param>
    /// <returns>性能趋势分析结果</returns>
    public PerformanceTrend AnalyzeTrend(PerformanceCounterType counterType, TimeSpan? timeRange = null)
    {
        var snapshots = timeRange.HasValue
            ? GetSnapshotsInTimeRange(timeRange.Value)
            : GetAllSnapshots();

        if (snapshots.Count == 0)
        {
            return new PerformanceTrend
            {
                CounterType = counterType,
                SampleCount = 0,
                AnalysisTimespan = timeRange ?? TimeSpan.Zero
            };
        }

        var values = snapshots.Select(s => GetCounterValue(s, counterType)).ToList();

        var trend = new PerformanceTrend
        {
            CounterType = counterType,
            CurrentValue = values.LastOrDefault(),
            AverageValue = values.Average(),
            MinValue = values.Min(),
            MaxValue = values.Max(),
            SampleCount = values.Count,
            AnalysisTimespan = timeRange ?? (snapshots.Last().Timestamp - snapshots.First().Timestamp)
        };

        // 计算标准差
        var variance = values.Select(v => Math.Pow(v - trend.AverageValue, 2)).Average();
        trend.StandardDeviation = Math.Sqrt(variance);

        // 计算趋势（简单线性回归的斜率）
        if (values.Count >= 2)
        {
            var n = values.Count;
            var sumX = Enumerable.Range(0, n).Sum();
            var sumY = values.Sum();
            var sumXy = values.Select((v, i) => i * v).Sum();
            var sumX2 = Enumerable.Range(0, n).Select(i => i * i).Sum();

            trend.Trend = ((n * sumXy) - (sumX * sumY)) / ((n * sumX2) - (sumX * sumX));
        }

        return trend;
    }

    /// <summary>
    /// 清空所有快照
    /// </summary>
    public void ClearSnapshots()
    {
        while (_snapshots.TryDequeue(out _)) { }
    }

    /// <summary>
    /// 获取性能摘要报告
    /// </summary>
    /// <returns>性能摘要字符串</returns>
    public string GetPerformanceSummary()
    {
        var current = GetCurrentSnapshot();
        var cpuTrend = AnalyzeTrend(PerformanceCounterType.CpuUsage, TimeSpan.FromMinutes(5));
        var memoryTrend = AnalyzeTrend(PerformanceCounterType.MemoryUsage, TimeSpan.FromMinutes(5));

        return $"运行时性能摘要:\n" +
               $"  CPU使用率: {current.CpuUsage:F1}% (5分钟平均: {cpuTrend.AverageValue:F1}%)\n" +
               $"  内存使用: {current.MemoryUsage / 1024 / 1024} MB (托管内存: {current.ManagedMemory / 1024 / 1024} MB)\n" +
               $"  GC收集: Gen0={current.GcGen0Collections}, Gen1={current.GcGen1Collections}, Gen2={current.GcGen2Collections}\n" +
               $"  线程数: {current.ThreadCount}, 句柄数: {current.HandleCount}\n" +
               $"  运行时间: {current.ProcessUptime:dd\\.hh\\:mm\\:ss}\n" +
               $"  监控状态: {(IsMonitoring ? "运行中" : "已停止")}, 快照数: {SnapshotCount}";
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        StopMonitoring();
        _monitorTimer?.Dispose();
        _currentProcess?.Dispose();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 获取计数器值
    /// </summary>
    /// <param name="snapshot">性能快照</param>
    /// <param name="counterType">计数器类型</param>
    /// <returns>计数器值</returns>
    private static double GetCounterValue(PerformanceSnapshot snapshot, PerformanceCounterType counterType)
    {
        return counterType switch
        {
            PerformanceCounterType.CpuUsage => snapshot.CpuUsage,
            PerformanceCounterType.MemoryUsage => snapshot.MemoryUsage,
            PerformanceCounterType.GcCollections => snapshot.GcGen0Collections + snapshot.GcGen1Collections + snapshot.GcGen2Collections,
            PerformanceCounterType.ThreadCount => snapshot.ThreadCount,
            PerformanceCounterType.HandleCount => snapshot.HandleCount,
            _ => 0
        };
    }

    /// <summary>
    /// 收集性能快照
    /// </summary>
    /// <param name="state">定时器状态</param>
    private void CollectSnapshot(object? state)
    {
        if (_isDisposed)
        {
            return;
        }

        try
        {
            var snapshot = CreateSnapshot();
            _snapshots.Enqueue(snapshot);

            // 限制快照数量
            while (_snapshots.Count > _maxSnapshotCount)
            {
                _snapshots.TryDequeue(out _);
            }
        }
        catch (Exception ex)
        {
            // 记录异常但继续监控
            Debug.WriteLine($"Failed to collect performance snapshot: {ex.Message}");
        }
    }

    /// <summary>
    /// 创建性能快照
    /// </summary>
    /// <returns>性能快照</returns>
    private PerformanceSnapshot CreateSnapshot()
    {
        _currentProcess.Refresh();

        // 计算CPU使用率
        var currentTime = DateTime.UtcNow;
        var currentTotalProcessorTime = _currentProcess.TotalProcessorTime;

        var cpuUsage = 0.0;
        var timeDelta = currentTime - _lastCpuTime;
        var cpuTimeDelta = currentTotalProcessorTime - _lastTotalProcessorTime;

        if (timeDelta.TotalMilliseconds > 0)
        {
            cpuUsage = cpuTimeDelta.TotalMilliseconds / (Environment.ProcessorCount * timeDelta.TotalMilliseconds) * 100;
            cpuUsage = Math.Max(0, Math.Min(100, cpuUsage)); // 限制在0-100%范围内
        }

        _lastCpuTime = currentTime;
        _lastTotalProcessorTime = currentTotalProcessorTime;

        return new PerformanceSnapshot
        {
            Timestamp = currentTime,
            CpuUsage = cpuUsage,
            MemoryUsage = _currentProcess.WorkingSet64,
            PrivateMemorySize = _currentProcess.PrivateMemorySize64,
            VirtualMemorySize = _currentProcess.VirtualMemorySize64,
            WorkingSet = _currentProcess.WorkingSet64,
            GcGen0Collections = GC.CollectionCount(0),
            GcGen1Collections = GC.CollectionCount(1),
            GcGen2Collections = GC.CollectionCount(2),
            ManagedMemory = GC.GetTotalMemory(false),
            ThreadCount = _currentProcess.Threads.Count,
            HandleCount = _currentProcess.HandleCount,
            UserProcessorTime = _currentProcess.UserProcessorTime,
            PrivilegedProcessorTime = _currentProcess.PrivilegedProcessorTime,
            TotalProcessorTime = _currentProcess.TotalProcessorTime,
            ProcessUptime = DateTime.Now - _currentProcess.StartTime
        };
    }
}

/// <summary>
/// 性能指标快照
/// </summary>
public record PerformanceSnapshot
{
    /// <summary>
    /// 快照时间
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// CPU使用率百分比
    /// </summary>
    public double CpuUsage { get; set; }

    /// <summary>
    /// 内存使用量（字节）
    /// </summary>
    public long MemoryUsage { get; set; }

    /// <summary>
    /// 私有内存使用量（字节）
    /// </summary>
    public long PrivateMemorySize { get; set; }

    /// <summary>
    /// 虚拟内存使用量（字节）
    /// </summary>
    public long VirtualMemorySize { get; set; }

    /// <summary>
    /// 工作集大小（字节）
    /// </summary>
    public long WorkingSet { get; set; }

    /// <summary>
    /// GC代0收集次数
    /// </summary>
    public int GcGen0Collections { get; set; }

    /// <summary>
    /// GC代1收集次数
    /// </summary>
    public int GcGen1Collections { get; set; }

    /// <summary>
    /// GC代2收集次数
    /// </summary>
    public int GcGen2Collections { get; set; }

    /// <summary>
    /// 托管内存使用量（字节）
    /// </summary>
    public long ManagedMemory { get; set; }

    /// <summary>
    /// 线程数量
    /// </summary>
    public int ThreadCount { get; set; }

    /// <summary>
    /// 句柄数量
    /// </summary>
    public int HandleCount { get; set; }

    /// <summary>
    /// 用户处理器时间
    /// </summary>
    public TimeSpan UserProcessorTime { get; set; }

    /// <summary>
    /// 特权处理器时间
    /// </summary>
    public TimeSpan PrivilegedProcessorTime { get; set; }

    /// <summary>
    /// 总处理器时间
    /// </summary>
    public TimeSpan TotalProcessorTime { get; set; }

    /// <summary>
    /// 进程运行时间
    /// </summary>
    public TimeSpan ProcessUptime { get; set; }
}

/// <summary>
/// 性能趋势分析结果
/// </summary>
public record PerformanceTrend
{
    /// <summary>
    /// 计数器类型
    /// </summary>
    public PerformanceCounterType CounterType { get; set; }

    /// <summary>
    /// 当前值
    /// </summary>
    public double CurrentValue { get; set; }

    /// <summary>
    /// 平均值
    /// </summary>
    public double AverageValue { get; set; }

    /// <summary>
    /// 最小值
    /// </summary>
    public double MinValue { get; set; }

    /// <summary>
    /// 最大值
    /// </summary>
    public double MaxValue { get; set; }

    /// <summary>
    /// 变化趋势（正数表示上升，负数表示下降）
    /// </summary>
    public double Trend { get; set; }

    /// <summary>
    /// 标准差
    /// </summary>
    public double StandardDeviation { get; set; }

    /// <summary>
    /// 样本数量
    /// </summary>
    public int SampleCount { get; set; }

    /// <summary>
    /// 分析时间范围
    /// </summary>
    public TimeSpan AnalysisTimespan { get; set; }
}

/// <summary>
/// 性能计数器类型枚举
/// </summary>
public enum PerformanceCounterType
{
    /// <summary>
    /// CPU使用率
    /// </summary>
    CpuUsage = 1,

    /// <summary>
    /// 内存使用量
    /// </summary>
    MemoryUsage = 2,

    /// <summary>
    /// GC收集次数
    /// </summary>
    GcCollections = 3,

    /// <summary>
    /// 线程数量
    /// </summary>
    ThreadCount = 4,

    /// <summary>
    /// 句柄数量
    /// </summary>
    HandleCount = 5
}
