// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.ScheduledJobs.Models;
using XiHan.Framework.Tasks.ScheduledJobs.Scheduler;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs.Scheduler;

/// <summary>
/// IntervalScheduler 固定间隔触发时间换算测试
/// </summary>
/// <remarks>
/// 间隔调度只做"起点 + 间隔"的加法，关键契约是非正间隔必须被判定为不可调度（返回 null / 不触发），
/// 否则调度循环会退化成忙等。全部用例使用固定基准时间。
/// </remarks>
public class IntervalSchedulerTests
{
    /// <summary>
    /// 固定基准时间
    /// </summary>
    private static readonly DateTimeOffset BaseTime = new(2024, 6, 12, 8, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 正间隔时下次触发时间等于起点加间隔
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(60)]
    [InlineData(3600)]
    public void GetNextFireTime_WithPositiveInterval_AddsIntervalToBaseTime(int intervalSeconds)
    {
        var interval = TimeSpan.FromSeconds(intervalSeconds);

        var next = IntervalScheduler.GetNextFireTime(interval, BaseTime);

        Assert.NotNull(next);
        Assert.Equal(BaseTime.Add(interval), next!.Value);
    }

    /// <summary>
    /// 零间隔与负间隔都视为不可调度
    /// </summary>
    [Fact]
    public void GetNextFireTime_WithNonPositiveInterval_ReturnsNull()
    {
        Assert.Null(IntervalScheduler.GetNextFireTime(TimeSpan.Zero, BaseTime));
        Assert.Null(IntervalScheduler.GetNextFireTime(TimeSpan.FromSeconds(-1), BaseTime));
        Assert.Null(IntervalScheduler.GetNextFireTime(TimeSpan.FromDays(-1), BaseTime));
    }

    /// <summary>
    /// 不传起点时以当前时刻为基准，结果落在未来
    /// </summary>
    [Fact]
    public void GetNextFireTime_WithoutBaseTime_UsesCurrentMoment()
    {
        var before = DateTimeOffset.UtcNow;

        var next = IntervalScheduler.GetNextFireTime(TimeSpan.FromMinutes(10));

        Assert.NotNull(next);
        Assert.True(next!.Value >= before.AddMinutes(10));
        Assert.True(next.Value <= DateTimeOffset.UtcNow.AddMinutes(10));
    }

    /// <summary>
    /// 连续排期时间点等距递增
    /// </summary>
    [Fact]
    public void GetNextFireTime_ChainedFromPreviousResult_ProducesEvenlySpacedMoments()
    {
        var interval = TimeSpan.FromMinutes(15);

        var first = IntervalScheduler.GetNextFireTime(interval, BaseTime);
        var second = IntervalScheduler.GetNextFireTime(interval, first!.Value);

        Assert.Equal(BaseTime.AddMinutes(15), first.Value);
        Assert.Equal(BaseTime.AddMinutes(30), second!.Value);
    }

    /// <summary>
    /// 未配置间隔的任务永远不触发
    /// </summary>
    [Fact]
    public void ShouldFire_WhenIntervalMissing_ReturnsFalse()
    {
        var jobInfo = CreateIntervalJob(null);

        Assert.False(IntervalScheduler.ShouldFire(jobInfo, DateTimeOffset.UtcNow.AddDays(-1)));
    }

    /// <summary>
    /// 非正间隔的任务永远不触发
    /// </summary>
    [Fact]
    public void ShouldFire_WhenIntervalIsNonPositive_ReturnsFalse()
    {
        Assert.False(IntervalScheduler.ShouldFire(CreateIntervalJob(TimeSpan.Zero), DateTimeOffset.UtcNow.AddDays(-1)));
        Assert.False(IntervalScheduler.ShouldFire(CreateIntervalJob(TimeSpan.FromSeconds(-5)), DateTimeOffset.UtcNow.AddDays(-1)));
    }

    /// <summary>
    /// 距上次触发已超过一个间隔时应触发
    /// </summary>
    [Fact]
    public void ShouldFire_WhenIntervalElapsedSinceLastFire_ReturnsTrue()
    {
        var jobInfo = CreateIntervalJob(TimeSpan.FromMinutes(1));

        Assert.True(IntervalScheduler.ShouldFire(jobInfo, DateTimeOffset.UtcNow.AddHours(-1)));
    }

    /// <summary>
    /// 距上次触发还不到一个间隔时不触发
    /// </summary>
    [Fact]
    public void ShouldFire_WhenIntervalNotElapsedYet_ReturnsFalse()
    {
        var jobInfo = CreateIntervalJob(TimeSpan.FromHours(1));

        Assert.False(IntervalScheduler.ShouldFire(jobInfo, DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// 构造一个间隔触发类型的任务定义
    /// </summary>
    private static JobInfo CreateIntervalJob(TimeSpan? interval)
    {
        return new JobInfo
        {
            JobName = "interval-job",
            JobType = typeof(IntervalSchedulerTests),
            TriggerType = JobTriggerType.Interval,
            Interval = interval
        };
    }
}
