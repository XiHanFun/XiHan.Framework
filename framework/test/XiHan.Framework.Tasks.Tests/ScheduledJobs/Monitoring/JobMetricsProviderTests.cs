// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.ScheduledJobs.Models;
using XiHan.Framework.Tasks.ScheduledJobs.Monitoring;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs.Monitoring;

/// <summary>
/// JobMetricsProvider 任务度量测试
/// </summary>
/// <remarks>
/// 度量是纯累加的整数运算，用刻意挑选的耗时序列（100/200/300/400）让均值每一步都能整除，
/// 断言可以写成精确值而不是近似区间。成功率/失败率同理取能整除的样本量。
/// </remarks>
public class JobMetricsProviderTests
{
    /// <summary>
    /// 并发用例的兜底超时
    /// </summary>
    private const int TimeoutMilliseconds = 60_000;

    /// <summary>
    /// 没有记录过的任务查不到度量
    /// </summary>
    [Fact]
    public void GetMetrics_WhenJobUnknown_ReturnsNull()
    {
        var provider = new JobMetricsProvider();

        Assert.Null(provider.GetMetrics("unknown"));
        Assert.Empty(provider.GetAllMetrics());
    }

    /// <summary>
    /// 首次记录时建立度量条目并填入耗时三件套
    /// </summary>
    [Fact]
    public void RecordExecution_FirstTime_InitializesMetrics()
    {
        var provider = new JobMetricsProvider();

        provider.RecordExecution("job-a", JobStatus.Succeeded, 120);

        var metrics = provider.GetMetrics("job-a");
        Assert.NotNull(metrics);
        Assert.Equal("job-a", metrics!.JobName);
        Assert.Equal(1L, metrics.TotalExecutions);
        Assert.Equal(1L, metrics.SuccessCount);
        Assert.Equal(0L, metrics.FailureCount);
        Assert.Equal(0L, metrics.CancelledCount);
        Assert.Equal(120L, metrics.LastDurationMs);
        Assert.Equal(120L, metrics.MinDurationMs);
        Assert.Equal(120L, metrics.MaxDurationMs);
        Assert.Equal(120L, metrics.AverageDurationMs);
        Assert.NotNull(metrics.LastExecutionTime);
    }

    /// <summary>
    /// 连续记录时最小/最大/平均/最后耗时逐步收敛到正确值
    /// </summary>
    [Fact]
    public void RecordExecution_Repeatedly_AggregatesDurationStatistics()
    {
        var provider = new JobMetricsProvider();

        provider.RecordExecution("job-a", JobStatus.Succeeded, 100);
        provider.RecordExecution("job-a", JobStatus.Succeeded, 200);
        provider.RecordExecution("job-a", JobStatus.Failed, 300);
        provider.RecordExecution("job-a", JobStatus.Canceled, 400);

        var metrics = provider.GetMetrics("job-a");
        Assert.NotNull(metrics);
        Assert.Equal(4L, metrics!.TotalExecutions);
        Assert.Equal(100L, metrics.MinDurationMs);
        Assert.Equal(400L, metrics.MaxDurationMs);
        Assert.Equal(400L, metrics.LastDurationMs);
        Assert.Equal(250L, metrics.AverageDurationMs);
    }

    /// <summary>
    /// 不同状态分别累加到对应计数器
    /// </summary>
    [Fact]
    public void RecordExecution_CountsEachStatusSeparately()
    {
        var provider = new JobMetricsProvider();

        provider.RecordExecution("job-a", JobStatus.Succeeded, 100);
        provider.RecordExecution("job-a", JobStatus.Succeeded, 200);
        provider.RecordExecution("job-a", JobStatus.Failed, 300);
        provider.RecordExecution("job-a", JobStatus.Canceled, 400);

        var metrics = provider.GetMetrics("job-a");
        Assert.NotNull(metrics);
        Assert.Equal(2L, metrics!.SuccessCount);
        Assert.Equal(1L, metrics.FailureCount);
        Assert.Equal(1L, metrics.CancelledCount);
    }

    /// <summary>
    /// 中间态状态只计总次数，不计入成功/失败/取消
    /// </summary>
    [Theory]
    [InlineData(JobStatus.Pending)]
    [InlineData(JobStatus.Running)]
    [InlineData(JobStatus.Paused)]
    public void RecordExecution_WithNonTerminalStatus_OnlyCountsTotal(JobStatus status)
    {
        var provider = new JobMetricsProvider();

        provider.RecordExecution("job-a", status, 100);

        var metrics = provider.GetMetrics("job-a");
        Assert.NotNull(metrics);
        Assert.Equal(1L, metrics!.TotalExecutions);
        Assert.Equal(0L, metrics.SuccessCount);
        Assert.Equal(0L, metrics.FailureCount);
        Assert.Equal(0L, metrics.CancelledCount);
    }

    /// <summary>
    /// 成功率与失败率按总次数折算成百分比
    /// </summary>
    [Fact]
    public void SuccessRateAndFailureRate_ArePercentagesOfTotalExecutions()
    {
        var provider = new JobMetricsProvider();

        provider.RecordExecution("job-a", JobStatus.Succeeded, 100);
        provider.RecordExecution("job-a", JobStatus.Succeeded, 200);
        provider.RecordExecution("job-a", JobStatus.Failed, 300);
        provider.RecordExecution("job-a", JobStatus.Canceled, 400);

        var metrics = provider.GetMetrics("job-a");
        Assert.NotNull(metrics);
        Assert.Equal(50d, metrics!.SuccessRate);
        Assert.Equal(25d, metrics.FailureRate);
    }

    /// <summary>
    /// 没有执行记录时成功率与失败率为 0，不做除零
    /// </summary>
    [Fact]
    public void SuccessRateAndFailureRate_WithoutExecutions_AreZero()
    {
        var metrics = new JobMetrics();

        Assert.Equal(0d, metrics.SuccessRate);
        Assert.Equal(0d, metrics.FailureRate);
    }

    /// <summary>
    /// 不同任务的度量互相隔离
    /// </summary>
    [Fact]
    public void RecordExecution_ForDifferentJobs_KeepsMetricsIsolated()
    {
        var provider = new JobMetricsProvider();

        provider.RecordExecution("job-a", JobStatus.Succeeded, 100);
        provider.RecordExecution("job-b", JobStatus.Failed, 900);

        Assert.Equal(1L, provider.GetMetrics("job-a")!.SuccessCount);
        Assert.Equal(0L, provider.GetMetrics("job-a")!.FailureCount);
        Assert.Equal(1L, provider.GetMetrics("job-b")!.FailureCount);
        Assert.Equal(900L, provider.GetMetrics("job-b")!.MaxDurationMs);
        Assert.Equal(2, provider.GetAllMetrics().Count);
    }

    /// <summary>
    /// 指定任务名清理时只影响该任务
    /// </summary>
    [Fact]
    public void Clear_WithJobName_RemovesOnlyThatJob()
    {
        var provider = new JobMetricsProvider();
        provider.RecordExecution("job-a", JobStatus.Succeeded, 100);
        provider.RecordExecution("job-b", JobStatus.Succeeded, 100);

        provider.Clear("job-a");

        Assert.Null(provider.GetMetrics("job-a"));
        Assert.NotNull(provider.GetMetrics("job-b"));
    }

    /// <summary>
    /// 不指定任务名时清空全部度量
    /// </summary>
    [Fact]
    public void Clear_WithoutJobName_RemovesEverything()
    {
        var provider = new JobMetricsProvider();
        provider.RecordExecution("job-a", JobStatus.Succeeded, 100);
        provider.RecordExecution("job-b", JobStatus.Succeeded, 100);

        provider.Clear();

        Assert.Empty(provider.GetAllMetrics());
    }

    /// <summary>
    /// 清理不存在的任务是空操作
    /// </summary>
    [Fact]
    public void Clear_WithUnknownJobName_DoesNothing()
    {
        var provider = new JobMetricsProvider();
        provider.RecordExecution("job-a", JobStatus.Succeeded, 100);

        provider.Clear("job-x");

        Assert.Single(provider.GetAllMetrics());
    }

    /// <summary>
    /// 清空后重新记录会重建条目并从头累计
    /// </summary>
    [Fact]
    public void RecordExecution_AfterClear_StartsFromScratch()
    {
        var provider = new JobMetricsProvider();
        provider.RecordExecution("job-a", JobStatus.Succeeded, 100);
        provider.RecordExecution("job-a", JobStatus.Succeeded, 100);

        provider.Clear("job-a");
        provider.RecordExecution("job-a", JobStatus.Failed, 500);

        var metrics = provider.GetMetrics("job-a");
        Assert.NotNull(metrics);
        Assert.Equal(1L, metrics!.TotalExecutions);
        Assert.Equal(0L, metrics.SuccessCount);
        Assert.Equal(1L, metrics.FailureCount);
        Assert.Equal(500L, metrics.MaxDurationMs);
    }

    /// <summary>
    /// 多线程并发记录同一任务时计数不丢失
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task RecordExecution_UnderConcurrentWriters_DoesNotLoseCounts()
    {
        var provider = new JobMetricsProvider();
        const int WriterCount = 200;

        var tasks = Enumerable.Range(0, WriterCount)
            .Select(_ => Task.Run(() => provider.RecordExecution("job-hot", JobStatus.Succeeded, 10)))
            .ToArray();

        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        var metrics = provider.GetMetrics("job-hot");
        Assert.NotNull(metrics);
        Assert.Equal(WriterCount, metrics!.TotalExecutions);
        Assert.Equal(WriterCount, metrics.SuccessCount);
    }

    /// <summary>
    /// 新建的度量对象采用零值起点
    /// </summary>
    [Fact]
    public void JobMetrics_Default_UsesZeroedCounters()
    {
        var metrics = new JobMetrics();

        Assert.Equal(string.Empty, metrics.JobName);
        Assert.Equal(0L, metrics.TotalExecutions);
        Assert.Equal(0L, metrics.SuccessCount);
        Assert.Equal(0L, metrics.FailureCount);
        Assert.Equal(0L, metrics.CancelledCount);
        Assert.Equal(0L, metrics.LastDurationMs);
        Assert.Equal(0L, metrics.AverageDurationMs);
        Assert.Equal(0L, metrics.MinDurationMs);
        Assert.Equal(0L, metrics.MaxDurationMs);
        Assert.Null(metrics.LastExecutionTime);
    }
}
