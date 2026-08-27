// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.ScheduledJobs.Models;
using XiHan.Framework.Tasks.ScheduledJobs.Monitoring;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs.Monitoring;

/// <summary>
/// JobMetricsProvider 最小耗时统计测试
/// </summary>
/// <remarks>
/// 最小耗时原来用 MinDurationMs == 0 当"尚未设置"的哨兵值，于是一次真实的 0ms 执行会被误判成
/// "还没设置过"，之后任何非零耗时都能把它顶掉——最快的那次反而记不下来。
/// 这里专门覆盖 0ms 参与统计的各种位置（首次、中途、清理后重来），以及"首次非零"这个反例。
/// </remarks>
public class JobMetricsProviderMinDurationTests
{
    /// <summary>
    /// 首次执行耗时为 0ms 时也要记进最小耗时，后续非零耗时不得把它顶掉
    /// </summary>
    [Fact]
    public void RecordExecution_WhenFirstExecutionTakesZeroMilliseconds_KeepsZeroAsMinimum()
    {
        var provider = new JobMetricsProvider();

        provider.RecordExecution("job-a", JobStatus.Succeeded, 0);
        provider.RecordExecution("job-a", JobStatus.Succeeded, 100);
        provider.RecordExecution("job-a", JobStatus.Succeeded, 50);

        var metrics = provider.GetMetrics("job-a");
        Assert.NotNull(metrics);
        Assert.Equal(0L, metrics!.MinDurationMs);
        Assert.Equal(100L, metrics.MaxDurationMs);
        Assert.Equal(50L, metrics.LastDurationMs);
        Assert.Equal(3L, metrics.TotalExecutions);
    }

    /// <summary>
    /// 只记录一次 0ms 时最小与最大耗时都是 0
    /// </summary>
    [Fact]
    public void RecordExecution_WithSingleZeroDuration_ReportsZeroForBothBounds()
    {
        var provider = new JobMetricsProvider();

        provider.RecordExecution("job-a", JobStatus.Succeeded, 0);

        var metrics = provider.GetMetrics("job-a");
        Assert.NotNull(metrics);
        Assert.Equal(0L, metrics!.MinDurationMs);
        Assert.Equal(0L, metrics.MaxDurationMs);
        Assert.Equal(1L, metrics.TotalExecutions);
    }

    /// <summary>
    /// 0ms 出现在中途时同样能把最小耗时压到 0
    /// </summary>
    [Fact]
    public void RecordExecution_WhenLaterExecutionTakesZeroMilliseconds_LowersMinimumToZero()
    {
        var provider = new JobMetricsProvider();

        provider.RecordExecution("job-a", JobStatus.Succeeded, 100);
        provider.RecordExecution("job-a", JobStatus.Succeeded, 0);
        provider.RecordExecution("job-a", JobStatus.Succeeded, 80);

        Assert.Equal(0L, provider.GetMetrics("job-a")!.MinDurationMs);
    }

    /// <summary>
    /// 首次执行耗时非零时最小耗时就是该值，不会被字段的 0 初值影响（反例）
    /// </summary>
    [Fact]
    public void RecordExecution_WhenFirstExecutionIsNonZero_UsesItAsMinimum()
    {
        var provider = new JobMetricsProvider();

        provider.RecordExecution("job-a", JobStatus.Succeeded, 900);

        Assert.Equal(900L, provider.GetMetrics("job-a")!.MinDurationMs);
    }

    /// <summary>
    /// 首次之后只出现更大的耗时时，最小值保持首次那一条
    /// </summary>
    [Fact]
    public void RecordExecution_WhenSubsequentDurationsAreLarger_KeepsFirstAsMinimum()
    {
        var provider = new JobMetricsProvider();

        provider.RecordExecution("job-a", JobStatus.Succeeded, 30);
        provider.RecordExecution("job-a", JobStatus.Succeeded, 90);
        provider.RecordExecution("job-a", JobStatus.Failed, 120);

        Assert.Equal(30L, provider.GetMetrics("job-a")!.MinDurationMs);
    }

    /// <summary>
    /// 清空后重新记录的 0ms 是新的起点，不会沿用上一轮的最小值
    /// </summary>
    [Fact]
    public void RecordExecution_AfterClear_TreatsZeroAsFreshMinimum()
    {
        var provider = new JobMetricsProvider();
        provider.RecordExecution("job-a", JobStatus.Succeeded, 10);

        provider.Clear("job-a");
        provider.RecordExecution("job-a", JobStatus.Succeeded, 0);
        provider.RecordExecution("job-a", JobStatus.Succeeded, 5);

        var metrics = provider.GetMetrics("job-a");
        Assert.NotNull(metrics);
        Assert.Equal(0L, metrics!.MinDurationMs);
        Assert.Equal(2L, metrics.TotalExecutions);
    }

    /// <summary>
    /// 不同任务的 0ms 记录互不影响
    /// </summary>
    [Fact]
    public void RecordExecution_WithZeroDuration_KeepsJobsIsolated()
    {
        var provider = new JobMetricsProvider();

        provider.RecordExecution("job-a", JobStatus.Succeeded, 0);
        provider.RecordExecution("job-b", JobStatus.Succeeded, 300);
        provider.RecordExecution("job-b", JobStatus.Succeeded, 200);

        Assert.Equal(0L, provider.GetMetrics("job-a")!.MinDurationMs);
        Assert.Equal(200L, provider.GetMetrics("job-b")!.MinDurationMs);
    }

    /// <summary>
    /// 中间态状态的记录也参与耗时统计，0ms 同样算数
    /// </summary>
    [Fact]
    public void RecordExecution_WithNonTerminalStatusAndZeroDuration_StillParticipatesInMinimum()
    {
        var provider = new JobMetricsProvider();

        provider.RecordExecution("job-a", JobStatus.Running, 0);
        provider.RecordExecution("job-a", JobStatus.Succeeded, 70);

        Assert.Equal(0L, provider.GetMetrics("job-a")!.MinDurationMs);
    }
}
