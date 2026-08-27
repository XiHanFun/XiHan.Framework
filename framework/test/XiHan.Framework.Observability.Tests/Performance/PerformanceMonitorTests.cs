// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Observability.Performance;

namespace XiHan.Framework.Observability.Tests.Performance;

/// <summary>
/// 性能监控测试
/// </summary>
/// <remarks>
/// 记录只在追踪器 Dispose 时落袋，所以「区间边界」的断言都围绕 Dispose 前后的可见性做。
/// 真实耗时无法预设，凡涉及具体毫秒数的断言一律留足数量级余量（慢操作 150ms 对阈值 50ms），
/// 百分位则改用「与实际记录集合的次序统计对齐」的方式断言，不依赖任何绝对时长。
/// </remarks>
public class PerformanceMonitorTests
{
    /// <summary>
    /// 性能监控实现监控接口
    /// </summary>
    [Fact]
    public void PerformanceMonitor_Always_ImplementsMonitorContract()
    {
        var monitor = new PerformanceMonitor();

        Assert.IsAssignableFrom<IPerformanceMonitor>(monitor);
    }

    /// <summary>
    /// 开始操作返回带操作名的追踪器
    /// </summary>
    [Fact]
    public void BeginOperation_WithName_ReturnsTrackerCarryingThatName()
    {
        var monitor = new PerformanceMonitor();

        using var tracker = monitor.BeginOperation("load-user");

        Assert.NotNull(tracker);
        Assert.Equal("load-user", tracker.OperationName);
        Assert.IsAssignableFrom<IDisposable>(tracker);
    }

    /// <summary>
    /// 每次开始操作返回独立的追踪器实例
    /// </summary>
    [Fact]
    public void BeginOperation_CalledTwice_ReturnsDistinctTrackers()
    {
        var monitor = new PerformanceMonitor();

        using var first = monitor.BeginOperation("op");
        using var second = monitor.BeginOperation("op");

        Assert.NotSame(first, second);
    }

    /// <summary>
    /// 没有任何记录时统计返回全零快照
    /// </summary>
    [Fact]
    public void GetStatistics_WhenNoRecords_ReturnsZeroedSnapshot()
    {
        var monitor = new PerformanceMonitor();

        var statistics = monitor.GetStatistics();

        Assert.Equal(0, statistics.TotalOperations);
        Assert.Equal(0, statistics.SuccessfulOperations);
        Assert.Equal(0, statistics.FailedOperations);
        Assert.Equal(0d, statistics.AverageDurationMs);
        Assert.Equal(0d, statistics.MinDurationMs);
        Assert.Equal(0d, statistics.MaxDurationMs);
        Assert.Equal(0d, statistics.P50DurationMs);
        Assert.Equal(0d, statistics.P95DurationMs);
        Assert.Equal(0d, statistics.P99DurationMs);
        Assert.NotNull(statistics.OperationStats);
        Assert.Empty(statistics.OperationStats);
    }

    /// <summary>
    /// 没有任何记录时慢操作列表为空
    /// </summary>
    [Fact]
    public void GetSlowOperations_WhenNoRecords_ReturnsEmpty()
    {
        var monitor = new PerformanceMonitor();

        Assert.Empty(monitor.GetSlowOperations());
        Assert.Empty(monitor.GetSlowOperations(0));
    }

    /// <summary>
    /// 追踪器未释放前操作不计入统计
    /// </summary>
    [Fact]
    public void GetStatistics_BeforeTrackerDisposed_DoesNotCountOperation()
    {
        var monitor = new PerformanceMonitor();

        var tracker = monitor.BeginOperation("pending");

        Assert.Equal(0, monitor.GetStatistics().TotalOperations);

        tracker.Dispose();

        Assert.Equal(1, monitor.GetStatistics().TotalOperations);
    }

    /// <summary>
    /// 追踪器释放后记录进入统计并带上完整的时间区间
    /// </summary>
    [Fact]
    public void Dispose_OnTracker_ClosesTimeRangeAndPersistsRecord()
    {
        var monitor = new PerformanceMonitor();
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        using (monitor.BeginOperation("save-order"))
        {
            Thread.Sleep(5);
        }

        var records = monitor.GetSlowOperations(0);
        Assert.Single(records);

        var perfRecord = records[0];
        Assert.Equal("save-order", perfRecord.OperationName);
        Assert.InRange(perfRecord.StartTime, before, DateTimeOffset.UtcNow.AddSeconds(1));
        Assert.NotNull(perfRecord.EndTime);
        Assert.True(perfRecord.EndTime.Value >= perfRecord.StartTime);
        Assert.True(perfRecord.DurationMs > 0d);
        Assert.True(perfRecord.Success);
        Assert.Null(perfRecord.Exception);
    }

    /// <summary>
    /// 同名操作聚合成一条操作统计，不同名分开统计
    /// </summary>
    [Fact]
    public void GetStatistics_WithMixedOperationNames_GroupsByName()
    {
        var monitor = new PerformanceMonitor();

        monitor.BeginOperation("alpha").Dispose();
        monitor.BeginOperation("alpha").Dispose();
        monitor.BeginOperation("beta").Dispose();

        var statistics = monitor.GetStatistics();

        Assert.Equal(3, statistics.TotalOperations);
        Assert.Equal(2, statistics.OperationStats.Count);
        Assert.Equal(2, statistics.OperationStats["alpha"].Count);
        Assert.Equal(1, statistics.OperationStats["beta"].Count);
        Assert.Equal("alpha", statistics.OperationStats["alpha"].OperationName);
        Assert.Equal("beta", statistics.OperationStats["beta"].OperationName);
    }

    /// <summary>
    /// 单个操作分组内的最小、平均、最大耗时相互一致
    /// </summary>
    [Fact]
    public void GetStatistics_OperationStats_KeepMinAverageMaxConsistent()
    {
        var monitor = new PerformanceMonitor();

        for (var i = 0; i < 5; i++)
        {
            using (monitor.BeginOperation("query"))
            {
                Thread.Sleep(i);
            }
        }

        var operation = monitor.GetStatistics().OperationStats["query"];

        Assert.Equal(5, operation.Count);
        Assert.True(operation.MinDurationMs <= operation.AverageDurationMs);
        Assert.True(operation.AverageDurationMs <= operation.MaxDurationMs);
    }

    /// <summary>
    /// 追踪器没有失败入口，所有记录默认按成功计
    /// </summary>
    /// <remarks>
    /// IPerformanceTracker 没有暴露标记失败的成员，因此 FailedOperations 在当前公共契约下恒为 0；
    /// 这条断言是对该契约的显式锁定，若将来补上失败入口，这里必须一起改。
    /// </remarks>
    [Fact]
    public void GetStatistics_ThroughPublicApi_ReportsEveryRecordAsSuccessful()
    {
        var monitor = new PerformanceMonitor();

        monitor.BeginOperation("a").Dispose();
        monitor.BeginOperation("b").Dispose();

        var statistics = monitor.GetStatistics();

        Assert.Equal(2, statistics.TotalOperations);
        Assert.Equal(2, statistics.SuccessfulOperations);
        Assert.Equal(0, statistics.FailedOperations);
        Assert.Equal(statistics.TotalOperations, statistics.SuccessfulOperations + statistics.FailedOperations);
    }

    /// <summary>
    /// 全局最小、平均、最大耗时相互一致，且与实际记录集合对齐
    /// </summary>
    [Fact]
    public void GetStatistics_GlobalDurations_MatchRecordSet()
    {
        var monitor = new PerformanceMonitor();

        for (var i = 0; i < 6; i++)
        {
            using (monitor.BeginOperation($"op-{i}"))
            {
                Thread.Sleep(i);
            }
        }

        // 与被测实现同样按升序求和，避免浮点累加次序不同带来的末位差
        var durations = monitor.GetSlowOperations(0).Select(r => r.DurationMs).OrderBy(d => d).ToArray();
        var statistics = monitor.GetStatistics();

        Assert.Equal(6, statistics.TotalOperations);
        Assert.Equal(durations.Min(), statistics.MinDurationMs);
        Assert.Equal(durations.Max(), statistics.MaxDurationMs);
        Assert.Equal(durations.Average(), statistics.AverageDurationMs, 6);
        Assert.True(statistics.MinDurationMs <= statistics.AverageDurationMs);
        Assert.True(statistics.AverageDurationMs <= statistics.MaxDurationMs);
    }

    /// <summary>
    /// 百分位按「升序排序后的最近排名」取值，且 P50 ≤ P95 ≤ P99
    /// </summary>
    /// <remarks>
    /// 实际耗时不可预设，因此期望值由本用例读回的真实记录集合按同一口径（ceil(p*n)-1 索引）推出，
    /// 断言的是排序方向、样本取全、以及 0.5/0.95/0.99 三个分位与属性的对应关系没有接错。
    /// </remarks>
    [Fact]
    public void GetStatistics_Percentiles_UseNearestRankOnAscendingDurations()
    {
        const int SampleCount = 10;
        var monitor = new PerformanceMonitor();

        for (var i = 0; i < SampleCount; i++)
        {
            using (monitor.BeginOperation("percentile"))
            {
                Thread.Sleep(i);
            }
        }

        var sorted = monitor.GetSlowOperations(0).Select(r => r.DurationMs).OrderBy(d => d).ToArray();
        var statistics = monitor.GetStatistics();

        Assert.Equal(SampleCount, sorted.Length);
        Assert.Equal(sorted[NearestRankIndex(0.5, sorted.Length)], statistics.P50DurationMs);
        Assert.Equal(sorted[NearestRankIndex(0.95, sorted.Length)], statistics.P95DurationMs);
        Assert.Equal(sorted[NearestRankIndex(0.99, sorted.Length)], statistics.P99DurationMs);
        Assert.True(statistics.P50DurationMs <= statistics.P95DurationMs);
        Assert.True(statistics.P95DurationMs <= statistics.P99DurationMs);
        Assert.True(statistics.MinDurationMs <= statistics.P50DurationMs);
        Assert.True(statistics.P99DurationMs <= statistics.MaxDurationMs);
    }

    /// <summary>
    /// 只有一条记录时三个百分位都落在这条记录上
    /// </summary>
    [Fact]
    public void GetStatistics_WithSingleRecord_AllPercentilesEqualThatDuration()
    {
        var monitor = new PerformanceMonitor();

        monitor.BeginOperation("only").Dispose();

        var statistics = monitor.GetStatistics();
        var duration = monitor.GetSlowOperations(0)[0].DurationMs;

        Assert.Equal(duration, statistics.P50DurationMs);
        Assert.Equal(duration, statistics.P95DurationMs);
        Assert.Equal(duration, statistics.P99DurationMs);
        Assert.Equal(duration, statistics.MinDurationMs);
        Assert.Equal(duration, statistics.MaxDurationMs);
        Assert.Equal(duration, statistics.AverageDurationMs);
    }

    /// <summary>
    /// 阈值为 0 时返回全部记录，并按耗时降序排列
    /// </summary>
    [Fact]
    public void GetSlowOperations_WithZeroThreshold_ReturnsAllOrderedByDurationDescending()
    {
        var monitor = new PerformanceMonitor();

        for (var i = 0; i < 5; i++)
        {
            using (monitor.BeginOperation($"op-{i}"))
            {
                Thread.Sleep(i * 3);
            }
        }

        var records = monitor.GetSlowOperations(0);

        Assert.Equal(5, records.Count);
        for (var i = 1; i < records.Count; i++)
        {
            Assert.True(records[i - 1].DurationMs >= records[i].DurationMs);
        }
    }

    /// <summary>
    /// 阈值高到不可能达到时返回空列表
    /// </summary>
    [Fact]
    public void GetSlowOperations_WithUnreachableThreshold_ReturnsEmpty()
    {
        var monitor = new PerformanceMonitor();

        monitor.BeginOperation("fast").Dispose();

        Assert.Empty(monitor.GetSlowOperations(double.MaxValue));
    }

    /// <summary>
    /// 阈值落在快慢操作之间时只返回慢操作
    /// </summary>
    [Fact]
    public void GetSlowOperations_WithThresholdBetweenFastAndSlow_ReturnsOnlySlowOne()
    {
        var monitor = new PerformanceMonitor();

        monitor.BeginOperation("fast").Dispose();
        using (monitor.BeginOperation("slow"))
        {
            Thread.Sleep(150);
        }

        var records = monitor.GetSlowOperations(50);

        Assert.Single(records);
        Assert.Equal("slow", records[0].OperationName);
    }

    /// <summary>
    /// 慢操作阈值默认 1000 毫秒，瞬时操作不会被判为慢操作
    /// </summary>
    [Fact]
    public void GetSlowOperations_WithDefaultThreshold_ExcludesInstantOperations()
    {
        var monitor = new PerformanceMonitor();

        monitor.BeginOperation("instant").Dispose();

        Assert.Empty(monitor.GetSlowOperations());
        Assert.Single(monitor.GetSlowOperations(0));
    }

    /// <summary>
    /// 清空后统计与慢操作列表一并归零，且监控器仍可继续使用
    /// </summary>
    [Fact]
    public void Clear_AfterRecords_ResetsEverythingAndKeepsMonitorUsable()
    {
        var monitor = new PerformanceMonitor();
        monitor.BeginOperation("before").Dispose();

        monitor.Clear();

        Assert.Equal(0, monitor.GetStatistics().TotalOperations);
        Assert.Empty(monitor.GetSlowOperations(0));

        monitor.BeginOperation("after").Dispose();

        Assert.Equal(1, monitor.GetStatistics().TotalOperations);
        Assert.Equal("after", monitor.GetSlowOperations(0)[0].OperationName);
    }

    /// <summary>
    /// 标签在追踪器释放后随记录一起保留
    /// </summary>
    [Fact]
    public void AddTag_BeforeDispose_PersistsTagsOnRecord()
    {
        var monitor = new PerformanceMonitor();

        using (var tracker = monitor.BeginOperation("tagged"))
        {
            tracker.AddTag("tenant", "t1");
            tracker.AddTag("region", "cn");
        }

        var perfRecord = monitor.GetSlowOperations(0)[0];

        Assert.Equal(2, perfRecord.Tags.Count);
        Assert.Equal("t1", perfRecord.Tags["tenant"]);
        Assert.Equal("cn", perfRecord.Tags["region"]);
    }

    /// <summary>
    /// 同键标签重复添加时后写覆盖先写
    /// </summary>
    [Fact]
    public void AddTag_WithDuplicateKey_KeepsLastValue()
    {
        var monitor = new PerformanceMonitor();

        using (var tracker = monitor.BeginOperation("tagged"))
        {
            tracker.AddTag("stage", "first");
            tracker.AddTag("stage", "second");
        }

        var perfRecord = monitor.GetSlowOperations(0)[0];

        Assert.Single(perfRecord.Tags);
        Assert.Equal("second", perfRecord.Tags["stage"]);
    }

    /// <summary>
    /// 检查点按调用顺序累积，相对耗时单调不减且不超过整体耗时
    /// </summary>
    [Fact]
    public void Checkpoint_CalledInSequence_RecordsMonotonicElapsedWithinDuration()
    {
        var monitor = new PerformanceMonitor();

        using (var tracker = monitor.BeginOperation("pipeline"))
        {
            tracker.Checkpoint("parsed");
            Thread.Sleep(10);
            tracker.Checkpoint("validated");
            Thread.Sleep(10);
            tracker.Checkpoint("persisted");
        }

        var perfRecord = monitor.GetSlowOperations(0)[0];

        Assert.Equal(3, perfRecord.Checkpoints.Count);
        Assert.Equal("parsed", perfRecord.Checkpoints[0].Name);
        Assert.Equal("validated", perfRecord.Checkpoints[1].Name);
        Assert.Equal("persisted", perfRecord.Checkpoints[2].Name);
        Assert.True(perfRecord.Checkpoints[0].ElapsedMs <= perfRecord.Checkpoints[1].ElapsedMs);
        Assert.True(perfRecord.Checkpoints[1].ElapsedMs <= perfRecord.Checkpoints[2].ElapsedMs);
        Assert.True(perfRecord.Checkpoints[2].ElapsedMs <= perfRecord.DurationMs);
        Assert.All(perfRecord.Checkpoints, checkpoint => Assert.True(checkpoint.Timestamp >= perfRecord.StartTime.AddSeconds(-1)));
    }

    /// <summary>
    /// 没有调用检查点时记录的检查点列表为空而非 null
    /// </summary>
    [Fact]
    public void Checkpoint_NeverCalled_LeavesEmptyCheckpointList()
    {
        var monitor = new PerformanceMonitor();

        monitor.BeginOperation("plain").Dispose();

        var perfRecord = monitor.GetSlowOperations(0)[0];

        Assert.NotNull(perfRecord.Checkpoints);
        Assert.Empty(perfRecord.Checkpoints);
        Assert.Empty(perfRecord.Tags);
    }

    /// <summary>
    /// 统计返回的是当次快照，后续新增记录不会回写到旧快照上
    /// </summary>
    [Fact]
    public void GetStatistics_ReturnsSnapshot_NotLiveView()
    {
        var monitor = new PerformanceMonitor();
        monitor.BeginOperation("first").Dispose();

        var snapshot = monitor.GetStatistics();
        monitor.BeginOperation("second").Dispose();

        Assert.Equal(1, snapshot.TotalOperations);
        Assert.Equal(2, monitor.GetStatistics().TotalOperations);
    }

    /// <summary>
    /// 多线程并发开始并结束操作时记录不丢失
    /// </summary>
    [Fact]
    public void BeginOperation_FromMultipleThreads_RecordsEveryOperation()
    {
        const int ThreadCount = 8;
        const int PerThread = 25;

        var monitor = new PerformanceMonitor();
        var threads = new Thread[ThreadCount];

        for (var i = 0; i < ThreadCount; i++)
        {
            threads[i] = new Thread(() =>
            {
                for (var j = 0; j < PerThread; j++)
                {
                    using (monitor.BeginOperation("concurrent"))
                    {
                    }
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

        var statistics = monitor.GetStatistics();

        Assert.Equal(ThreadCount * PerThread, statistics.TotalOperations);
        Assert.Equal(ThreadCount * PerThread, statistics.OperationStats["concurrent"].Count);
        Assert.Equal(ThreadCount * PerThread, monitor.GetSlowOperations(0).Count);
    }

    /// <summary>
    /// 与被测实现一致的最近排名索引口径
    /// </summary>
    private static int NearestRankIndex(double percentile, int count)
    {
        var index = (int)Math.Ceiling(percentile * count) - 1;
        return Math.Max(0, Math.Min(index, count - 1));
    }
}
