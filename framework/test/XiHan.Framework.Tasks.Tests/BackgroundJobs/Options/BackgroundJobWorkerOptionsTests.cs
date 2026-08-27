// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.BackgroundJobs.Options;

namespace XiHan.Framework.Tasks.Tests.BackgroundJobs.Options;

/// <summary>
/// 后台作业 Worker 调优选项测试
/// </summary>
/// <remarks>
/// 该选项没有 Validate 方法，默认值本身就是对外契约：
/// 配置节名决定 appsettings 里写在哪、退避三兄弟（首等待 / 倍率 / 放弃阈值）决定失败作业的重试形状。
/// 这里把默认值逐条锁住，任何调整都要显式改测试并评估线上作业的重试节奏变化。
/// </remarks>
public class BackgroundJobWorkerOptionsTests
{
    /// <summary>
    /// 配置节名称锁死，改动会让既有 appsettings 静默失效
    /// </summary>
    [Fact]
    public void SectionName_IsStable()
    {
        Assert.Equal("XiHan:BackgroundJobs", BackgroundJobWorkerOptions.SectionName);
    }

    /// <summary>
    /// 默认开启执行，且不区分应用名
    /// </summary>
    [Fact]
    public void Defaults_EnableExecutionWithoutApplicationScope()
    {
        var options = new BackgroundJobWorkerOptions();

        Assert.True(options.IsJobExecutionEnabled);
        Assert.Null(options.ApplicationName);
    }

    /// <summary>
    /// 轮询相关默认值
    /// </summary>
    [Fact]
    public void Defaults_PollingValues()
    {
        var options = new BackgroundJobWorkerOptions();

        Assert.Equal(5000, options.FirstWaitDurationMilliseconds);
        Assert.Equal(5000, options.JobPollPeriodMilliseconds);
        Assert.Equal(1000, options.MaxJobFetchCount);
    }

    /// <summary>
    /// 退避与放弃相关默认值：首等待 60 秒、倍率 2、放弃阈值 2 天
    /// </summary>
    [Fact]
    public void Defaults_BackoffValues()
    {
        var options = new BackgroundJobWorkerOptions();

        Assert.Equal(60, options.DefaultFirstWaitDurationSeconds);
        Assert.Equal(2.0, options.DefaultWaitFactor);
        Assert.Equal(172800, options.DefaultTimeoutSeconds);
        Assert.Equal(TimeSpan.FromDays(2), TimeSpan.FromSeconds(options.DefaultTimeoutSeconds));
    }

    /// <summary>
    /// 分布式锁默认值：锁名与 TTL 决定多实例单活行为
    /// </summary>
    [Fact]
    public void Defaults_DistributedLockValues()
    {
        var options = new BackgroundJobWorkerOptions();

        Assert.Equal("XiHanBackgroundJobWorker", options.DistributedLockName);
        Assert.Equal(300, options.DistributedLockExpirySeconds);
    }

    /// <summary>
    /// 锁 TTL 必须显著大于轮询周期，否则单轮还没跑完锁就过期，多实例会重复执行
    /// </summary>
    [Fact]
    public void Defaults_LockExpiryIsLongerThanPollPeriod()
    {
        var options = new BackgroundJobWorkerOptions();

        Assert.True(options.DistributedLockExpirySeconds * 1000 > options.JobPollPeriodMilliseconds);
    }

    /// <summary>
    /// 所有调优项均可写，允许配置绑定覆盖
    /// </summary>
    [Fact]
    public void Properties_AreWritable()
    {
        var options = new BackgroundJobWorkerOptions
        {
            IsJobExecutionEnabled = false,
            ApplicationName = "order-service",
            FirstWaitDurationMilliseconds = 1,
            JobPollPeriodMilliseconds = 2,
            MaxJobFetchCount = 3,
            DefaultFirstWaitDurationSeconds = 4,
            DefaultWaitFactor = 1.5,
            DefaultTimeoutSeconds = 5,
            DistributedLockName = "custom-lock",
            DistributedLockExpirySeconds = 6
        };

        Assert.False(options.IsJobExecutionEnabled);
        Assert.Equal("order-service", options.ApplicationName);
        Assert.Equal(1, options.FirstWaitDurationMilliseconds);
        Assert.Equal(2, options.JobPollPeriodMilliseconds);
        Assert.Equal(3, options.MaxJobFetchCount);
        Assert.Equal(4, options.DefaultFirstWaitDurationSeconds);
        Assert.Equal(1.5, options.DefaultWaitFactor);
        Assert.Equal(5, options.DefaultTimeoutSeconds);
        Assert.Equal("custom-lock", options.DistributedLockName);
        Assert.Equal(6, options.DistributedLockExpirySeconds);
    }
}
