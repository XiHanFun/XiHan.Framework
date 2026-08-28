// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.BackgroundServices;

namespace XiHan.Framework.Tasks.Tests.BackgroundServices;

/// <summary>
/// 后台服务状态信息测试
/// </summary>
/// <remarks>
/// 这是对外暴露的只读快照，常被运维接口直接读取。
/// 最重要的一条是统计子对象永不为 null——否则每个调用方都要写判空，很容易漏。
/// </remarks>
public class XiHanBackgroundServiceStatusInfoTests
{
    /// <summary>
    /// 默认值：服务名为空串、统计子对象已初始化
    /// </summary>
    [Fact]
    public void Defaults_HaveEmptyNameAndNonNullStatistics()
    {
        var status = new XiHanBackgroundServiceStatusInfo();

        Assert.Equal(string.Empty, status.ServiceName);
        Assert.NotNull(status.Statistics);
        Assert.False(status.IsTaskProcessingEnabled);
        Assert.Equal(0, status.MaxConcurrentTasks);
        Assert.Equal(0, status.CurrentRunningTasks);
        Assert.Equal(0, status.IdleDelayMilliseconds);
        Assert.False(status.RetryEnabled);
    }

    /// <summary>
    /// 每次新建都拿到各自独立的统计子对象，快照之间不会互相串改
    /// </summary>
    [Fact]
    public void Defaults_StatisticsInstancesAreNotShared()
    {
        var first = new XiHanBackgroundServiceStatusInfo();
        var second = new XiHanBackgroundServiceStatusInfo();

        Assert.NotSame(first.Statistics, second.Statistics);
    }

    /// <summary>
    /// 全部字段可写，供服务侧组装快照
    /// </summary>
    [Fact]
    public void Properties_AreWritable()
    {
        var summary = new StatisticsSummary
        {
            TotalTasksProcessed = 11,
            TotalTasksFailed = 2,
            TotalTasksRetried = 5,
            CurrentRunningTasks = 3,
            AverageProcessingTimeMs = 12.5,
            SuccessRate = 84.5
        };

        var status = new XiHanBackgroundServiceStatusInfo
        {
            ServiceName = "EmailSendingService",
            IsTaskProcessingEnabled = true,
            MaxConcurrentTasks = 8,
            CurrentRunningTasks = 3,
            IdleDelayMilliseconds = 250,
            RetryEnabled = true,
            Statistics = summary
        };

        Assert.Equal("EmailSendingService", status.ServiceName);
        Assert.True(status.IsTaskProcessingEnabled);
        Assert.Equal(8, status.MaxConcurrentTasks);
        Assert.Equal(3, status.CurrentRunningTasks);
        Assert.Equal(250, status.IdleDelayMilliseconds);
        Assert.True(status.RetryEnabled);
        Assert.Same(summary, status.Statistics);
    }

    /// <summary>
    /// 统计摘要默认值全为零，未运行过的服务不会给出误导性的成功率
    /// </summary>
    [Fact]
    public void StatisticsSummary_DefaultsAreZero()
    {
        var summary = new StatisticsSummary();

        Assert.Equal(0, summary.TotalTasksProcessed);
        Assert.Equal(0, summary.TotalTasksFailed);
        Assert.Equal(0, summary.TotalTasksRetried);
        Assert.Equal(0, summary.CurrentRunningTasks);
        Assert.Equal(0, summary.AverageProcessingTimeMs);
        Assert.Equal(0, summary.SuccessRate);
        Assert.Equal(TimeSpan.Zero, summary.Uptime);
    }
}
