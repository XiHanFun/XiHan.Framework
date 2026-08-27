// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.BackgroundServices;

namespace XiHan.Framework.Tasks.Tests.BackgroundServices;

/// <summary>
/// 后台服务统计信息测试
/// </summary>
/// <remarks>
/// 统计口径本身就是契约：成功率分母只算"已完成"（成功 + 失败），不含重试次数；
/// 平均处理时间按任务标识去重记录，同一标识重复上报不会二次计入。
/// 摘要必须是值快照，取走之后再有任务进来不能倒灌改写调用方手里的那份数据。
/// </remarks>
public class BackgroundServiceStatisticsTests
{
    /// <summary>
    /// 初始状态全为零，成功率与平均耗时不给出误导值
    /// </summary>
    [Fact]
    public void Initial_AllCountersAreZero()
    {
        var statistics = new BackgroundServiceStatistics();

        Assert.Equal(0, statistics.TotalTasksProcessed);
        Assert.Equal(0, statistics.TotalTasksFailed);
        Assert.Equal(0, statistics.TotalTasksRetried);
        Assert.Equal(0, statistics.CurrentRunningTasks);
        Assert.Equal(0, statistics.AverageProcessingTimeMs);
        Assert.Equal(0, statistics.SuccessRate);
        Assert.True(statistics.Uptime >= TimeSpan.Zero);
        Assert.True(statistics.LastActivityTime >= statistics.StartTime);
    }

    /// <summary>
    /// 开始与完成成对出现时，运行中任务数回到零
    /// </summary>
    [Fact]
    public void RecordTaskStartedThenCompleted_RestoresRunningCount()
    {
        var statistics = new BackgroundServiceStatistics();

        statistics.RecordTaskStarted();
        Assert.Equal(1, statistics.CurrentRunningTasks);

        statistics.RecordTaskCompleted("t1", 10, true);

        Assert.Equal(0, statistics.CurrentRunningTasks);
        Assert.Equal(1, statistics.TotalTasksProcessed);
        Assert.Equal(0, statistics.TotalTasksFailed);
    }

    /// <summary>
    /// 失败完成只计入失败数
    /// </summary>
    [Fact]
    public void RecordTaskCompleted_WhenFailed_CountsAsFailure()
    {
        var statistics = new BackgroundServiceStatistics();

        statistics.RecordTaskStarted();
        statistics.RecordTaskCompleted("t1", 10, false);

        Assert.Equal(0, statistics.TotalTasksProcessed);
        Assert.Equal(1, statistics.TotalTasksFailed);
    }

    /// <summary>
    /// 成功率 = 成功数 /（成功数 + 失败数）× 100
    /// </summary>
    [Fact]
    public void SuccessRate_IsPercentageOfCompletedTasks()
    {
        var statistics = new BackgroundServiceStatistics();

        statistics.RecordTaskCompleted("t1", 10, true);
        statistics.RecordTaskCompleted("t2", 10, true);
        statistics.RecordTaskCompleted("t3", 10, true);
        statistics.RecordTaskCompleted("t4", 10, false);

        Assert.Equal(75d, statistics.SuccessRate);
    }

    /// <summary>
    /// 重试次数独立计数，不影响成功率分母
    /// </summary>
    [Fact]
    public void RecordTaskRetried_DoesNotAffectSuccessRate()
    {
        var statistics = new BackgroundServiceStatistics();

        statistics.RecordTaskRetried();
        statistics.RecordTaskRetried();
        statistics.RecordTaskCompleted("t1", 10, true);

        Assert.Equal(2, statistics.TotalTasksRetried);
        Assert.Equal(100d, statistics.SuccessRate);
    }

    /// <summary>
    /// 平均处理时间按已记录的任务取算术平均
    /// </summary>
    [Fact]
    public void AverageProcessingTimeMs_IsArithmeticMean()
    {
        var statistics = new BackgroundServiceStatistics();

        statistics.RecordTaskCompleted("t1", 100, true);
        statistics.RecordTaskCompleted("t2", 200, true);
        statistics.RecordTaskCompleted("t3", 300, true);

        Assert.Equal(200d, statistics.AverageProcessingTimeMs);
    }

    /// <summary>
    /// 同一任务标识重复上报时，耗时样本按首次记录为准（避免重试把平均值算重）
    /// </summary>
    [Fact]
    public void AverageProcessingTimeMs_WhenSameTaskIdReported_KeepsFirstSample()
    {
        var statistics = new BackgroundServiceStatistics();

        statistics.RecordTaskCompleted("t1", 100, true);
        statistics.RecordTaskCompleted("t1", 900, true);

        Assert.Equal(100d, statistics.AverageProcessingTimeMs);
        Assert.Equal(2, statistics.TotalTasksProcessed);
    }

    /// <summary>
    /// 记录活动会推进最后活动时间
    /// </summary>
    [Fact]
    public void RecordActivity_AdvancesLastActivityTime()
    {
        var statistics = new BackgroundServiceStatistics();
        var before = statistics.LastActivityTime;

        statistics.RecordTaskStarted();

        Assert.True(statistics.LastActivityTime >= before);
    }

    /// <summary>
    /// 重置清零全部计数与耗时样本
    /// </summary>
    [Fact]
    public void Reset_ClearsAllCounters()
    {
        var statistics = new BackgroundServiceStatistics();
        statistics.RecordTaskStarted();
        statistics.RecordTaskCompleted("t1", 100, true);
        statistics.RecordTaskCompleted("t2", 100, false);
        statistics.RecordTaskRetried();

        statistics.Reset();

        Assert.Equal(0, statistics.TotalTasksProcessed);
        Assert.Equal(0, statistics.TotalTasksFailed);
        Assert.Equal(0, statistics.TotalTasksRetried);
        Assert.Equal(0, statistics.CurrentRunningTasks);
        Assert.Equal(0, statistics.AverageProcessingTimeMs);
        Assert.Equal(0, statistics.SuccessRate);
    }

    /// <summary>
    /// 重置不会改变服务启动时间（启动时间描述的是进程生命周期，不是统计窗口）
    /// </summary>
    [Fact]
    public void Reset_KeepsStartTime()
    {
        var statistics = new BackgroundServiceStatistics();
        var startTime = statistics.StartTime;

        statistics.Reset();

        Assert.Equal(startTime, statistics.StartTime);
    }

    /// <summary>
    /// 摘要反映当前计数
    /// </summary>
    [Fact]
    public void GetSummary_ReflectsCurrentCounters()
    {
        var statistics = new BackgroundServiceStatistics();
        statistics.RecordTaskStarted();
        statistics.RecordTaskCompleted("t1", 40, true);
        statistics.RecordTaskCompleted("t2", 60, false);
        statistics.RecordTaskRetried();

        var summary = statistics.GetSummary();

        Assert.Equal(1, summary.TotalTasksProcessed);
        Assert.Equal(1, summary.TotalTasksFailed);
        Assert.Equal(1, summary.TotalTasksRetried);
        Assert.Equal(50d, summary.AverageProcessingTimeMs);
        Assert.Equal(50d, summary.SuccessRate);
        Assert.Equal(statistics.StartTime, summary.StartTime);
    }

    /// <summary>
    /// 摘要是值快照，取走后再有新任务不会倒灌改写
    /// </summary>
    [Fact]
    public void GetSummary_IsSnapshot()
    {
        var statistics = new BackgroundServiceStatistics();
        statistics.RecordTaskCompleted("t1", 10, true);

        var summary = statistics.GetSummary();
        statistics.RecordTaskCompleted("t2", 10, true);

        Assert.Equal(1, summary.TotalTasksProcessed);
        Assert.Equal(2, statistics.TotalTasksProcessed);
    }
}
