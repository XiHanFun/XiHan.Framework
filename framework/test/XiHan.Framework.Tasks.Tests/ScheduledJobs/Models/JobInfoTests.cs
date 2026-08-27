// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs.Models;

/// <summary>
/// JobInfo 任务定义默认值测试
/// </summary>
/// <remarks>
/// 这些默认值直接决定"没配就是什么行为"：不限重复次数、允许并发、5 分钟超时、默认重试策略、
/// 默认启用。调度器与中间件全都读它们，属于对外承诺的一部分。
/// </remarks>
public class JobInfoTests
{
    /// <summary>
    /// 新建任务定义采用文档约定的默认值
    /// </summary>
    [Fact]
    public void Constructor_Default_UsesDocumentedDefaults()
    {
        var jobInfo = new JobInfo();

        Assert.Equal(string.Empty, jobInfo.JobName);
        Assert.Null(jobInfo.Description);
        Assert.Equal(JobTriggerType.Cron, jobInfo.TriggerType);
        Assert.Null(jobInfo.CronExpression);
        Assert.Null(jobInfo.Interval);
        Assert.Null(jobInfo.Delay);
        Assert.Null(jobInfo.EndTime);
        Assert.Equal(-1, jobInfo.RepeatCount);
        Assert.Equal(JobPriority.Normal, jobInfo.Priority);
        Assert.True(jobInfo.AllowConcurrent);
        Assert.Equal(300000, jobInfo.TimeoutMilliseconds);
        Assert.True(jobInfo.IsEnabled);
        Assert.Null(jobInfo.TenantId);
        Assert.Null(jobInfo.DefaultParameters);
        Assert.Null(jobInfo.ModifiedAt);
    }

    /// <summary>
    /// 默认重试策略等价于全局默认策略，而不是空引用
    /// </summary>
    [Fact]
    public void Constructor_Default_AttachesDefaultRetryPolicy()
    {
        var jobInfo = new JobInfo();

        Assert.NotNull(jobInfo.RetryPolicy);
        Assert.Equal(JobRetryPolicy.Default.MaxRetryCount, jobInfo.RetryPolicy.MaxRetryCount);
        Assert.Equal(JobRetryPolicy.Default.RetryIntervalMilliseconds, jobInfo.RetryPolicy.RetryIntervalMilliseconds);
    }

    /// <summary>
    /// 两个任务定义各自持有独立的重试策略，改一个不影响另一个
    /// </summary>
    [Fact]
    public void RetryPolicy_OnDifferentInstances_IsNotShared()
    {
        var first = new JobInfo();
        var second = new JobInfo();

        first.RetryPolicy.MaxRetryCount = 9;

        Assert.Equal(3, second.RetryPolicy.MaxRetryCount);
    }

    /// <summary>
    /// 创建时间取构造时刻的 UTC 值
    /// </summary>
    [Fact]
    public void Constructor_StampsCreatedAtWithUtcNow()
    {
        var before = DateTimeOffset.UtcNow;
        var jobInfo = new JobInfo();
        var after = DateTimeOffset.UtcNow;

        Assert.InRange(jobInfo.CreatedAt, before, after);
        Assert.Equal(TimeSpan.Zero, jobInfo.CreatedAt.Offset);
    }

    /// <summary>
    /// 重复次数 -1 表示不限，0 表示一次都不触发
    /// </summary>
    [Fact]
    public void RepeatCount_SupportsUnlimitedSentinelAndZero()
    {
        var jobInfo = new JobInfo();
        Assert.Equal(-1, jobInfo.RepeatCount);

        jobInfo.RepeatCount = 0;
        Assert.Equal(0, jobInfo.RepeatCount);
    }

    /// <summary>
    /// 各触发类型对应的配置字段可以独立设置，互不牵连
    /// </summary>
    [Fact]
    public void TriggerConfiguration_FieldsAreIndependent()
    {
        var jobInfo = new JobInfo
        {
            TriggerType = JobTriggerType.Interval,
            CronExpression = "0 2 * * *",
            Interval = TimeSpan.FromMinutes(5),
            Delay = TimeSpan.FromSeconds(30)
        };

        Assert.Equal(JobTriggerType.Interval, jobInfo.TriggerType);
        Assert.Equal("0 2 * * *", jobInfo.CronExpression);
        Assert.Equal(TimeSpan.FromMinutes(5), jobInfo.Interval);
        Assert.Equal(TimeSpan.FromSeconds(30), jobInfo.Delay);
    }
}
