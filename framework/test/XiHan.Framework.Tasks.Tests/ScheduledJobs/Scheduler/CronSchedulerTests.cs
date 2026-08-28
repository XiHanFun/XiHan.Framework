// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.ScheduledJobs.Models;
using XiHan.Framework.Tasks.ScheduledJobs.Scheduler;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs.Scheduler;

/// <summary>
/// CronScheduler Cron 触发时间换算测试
/// </summary>
/// <remarks>
/// CronScheduler 的职责是把 CronHelper 的本地时间结果包装成带偏移的 DateTimeOffset，并把解析
/// 失败降级为 null（而不是抛给调度循环）。基准时间统一取 2024-06-12，避开各时区的夏令时切换日。
/// </remarks>
public class CronSchedulerTests
{
    /// <summary>
    /// 按本地时区求值：返回的时刻在本地时间上等于表达式指定的钟点
    /// </summary>
    [Fact]
    public void GetNextFireTime_WithLocalBaseTime_ReturnsLocalWallClockMoment()
    {
        var from = new DateTimeOffset(new DateTime(2024, 6, 12, 1, 30, 0, DateTimeKind.Local));

        var next = CronScheduler.GetNextFireTime("0 2 * * *", from);

        Assert.NotNull(next);
        Assert.Equal(new DateTime(2024, 6, 12, 2, 0, 0), next!.Value.DateTime);
    }

    /// <summary>
    /// 返回值携带本地时区偏移，便于与 UtcNow 做绝对时刻比较
    /// </summary>
    [Fact]
    public void GetNextFireTime_ReturnsMomentCarryingLocalOffset()
    {
        var from = new DateTimeOffset(new DateTime(2024, 6, 12, 1, 30, 0, DateTimeKind.Local));

        var next = CronScheduler.GetNextFireTime("0 2 * * *", from);

        Assert.NotNull(next);
        Assert.Equal(TimeZoneInfo.Local.GetUtcOffset(new DateTime(2024, 6, 12, 2, 0, 0)), next!.Value.Offset);
    }

    /// <summary>
    /// 起始时刻本身命中时也要向前推进一个周期，不能原地返回
    /// </summary>
    [Fact]
    public void GetNextFireTime_WhenBaseTimeAlreadyMatches_AdvancesOnePeriod()
    {
        var from = new DateTimeOffset(new DateTime(2024, 6, 12, 2, 0, 0, DateTimeKind.Local));

        var next = CronScheduler.GetNextFireTime("0 2 * * *", from);

        Assert.NotNull(next);
        Assert.Equal(new DateTime(2024, 6, 13, 2, 0, 0), next!.Value.DateTime);
    }

    /// <summary>
    /// 秒级表达式同样按本地钟点求值
    /// </summary>
    [Fact]
    public void GetNextFireTime_WithSixPartExpression_ResolvesSecondPrecision()
    {
        var from = new DateTimeOffset(new DateTime(2024, 6, 12, 1, 0, 0, DateTimeKind.Local));

        var next = CronScheduler.GetNextFireTime("30 * * * * *", from);

        Assert.NotNull(next);
        Assert.Equal(new DateTime(2024, 6, 12, 1, 0, 30), next!.Value.DateTime);
    }

    /// <summary>
    /// 表达式非法时降级为 null，不把解析异常抛进调度循环
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a cron")]
    [InlineData("99 * * * *")]
    [InlineData("* * * *")]
    public void GetNextFireTime_WhenExpressionIsInvalid_ReturnsNull(string expression)
    {
        var from = new DateTimeOffset(new DateTime(2024, 6, 12, 1, 0, 0, DateTimeKind.Local));

        Assert.Null(CronScheduler.GetNextFireTime(expression, from));
    }

    /// <summary>
    /// 不传起始时间时以当前时刻为基准，结果必定落在未来
    /// </summary>
    [Fact]
    public void GetNextFireTime_WithoutBaseTime_ReturnsFutureMoment()
    {
        var before = DateTimeOffset.Now;

        var next = CronScheduler.GetNextFireTime("* * * * *");

        Assert.NotNull(next);
        Assert.True(next!.Value > before);
    }

    /// <summary>
    /// 表达式校验直接委托给 CronHelper，口径保持一致
    /// </summary>
    [Theory]
    [InlineData("0 2 * * *", true)]
    [InlineData("*/15 * * * * *", true)]
    [InlineData("@daily", true)]
    [InlineData("* * * *", false)]
    [InlineData("abc", false)]
    public void IsValidExpression_MatchesCronHelperVerdict(string expression, bool expected)
    {
        Assert.Equal(expected, CronScheduler.IsValidExpression(expression));
    }

    /// <summary>
    /// 未配置 Cron 表达式的任务永远不触发
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ShouldFire_WhenCronExpressionMissing_ReturnsFalse(string? cronExpression)
    {
        var jobInfo = CreateCronJob(cronExpression);

        Assert.False(CronScheduler.ShouldFire(jobInfo, DateTimeOffset.UtcNow.AddDays(-1)));
    }

    /// <summary>
    /// 上次触发时间已过去很久时，本周期应触发
    /// </summary>
    [Fact]
    public void ShouldFire_WhenNextMomentAlreadyElapsed_ReturnsTrue()
    {
        var jobInfo = CreateCronJob("* * * * *");

        Assert.True(CronScheduler.ShouldFire(jobInfo, DateTimeOffset.UtcNow.AddHours(-1)));
    }

    /// <summary>
    /// 刚刚触发过时下一周期尚未到达，不应重复触发
    /// </summary>
    [Fact]
    public void ShouldFire_WhenNextMomentIsStillAhead_ReturnsFalse()
    {
        var jobInfo = CreateCronJob("* * * * *");

        Assert.False(CronScheduler.ShouldFire(jobInfo, DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// 表达式非法时不触发，避免把坏配置变成疯狂重试
    /// </summary>
    [Fact]
    public void ShouldFire_WhenCronExpressionIsInvalid_ReturnsFalse()
    {
        var jobInfo = CreateCronJob("this is not cron");

        Assert.False(CronScheduler.ShouldFire(jobInfo, DateTimeOffset.UtcNow.AddDays(-1)));
    }

    /// <summary>
    /// 构造一个 Cron 触发类型的任务定义
    /// </summary>
    private static JobInfo CreateCronJob(string? cronExpression)
    {
        return new JobInfo
        {
            JobName = "cron-job",
            JobType = typeof(CronSchedulerTests),
            TriggerType = JobTriggerType.Cron,
            CronExpression = cronExpression
        };
    }
}
