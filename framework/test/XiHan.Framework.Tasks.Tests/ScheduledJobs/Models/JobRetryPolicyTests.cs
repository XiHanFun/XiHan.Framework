// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs.Models;

/// <summary>
/// JobRetryPolicy 重试策略测试
/// </summary>
/// <remarks>
/// 退避算法是纯函数，直接表驱动验证。默认值同时被 JobInfo 与特性映射复用，改动会波及全链路，
/// 所以在这里钉死。
/// </remarks>
public class JobRetryPolicyTests
{
    /// <summary>
    /// 新建策略采用"3 次重试 + 1 秒起步 + 2 倍指数退避 + 60 秒封顶"的默认口径
    /// </summary>
    [Fact]
    public void Constructor_Default_UsesDocumentedDefaults()
    {
        var policy = new JobRetryPolicy();

        Assert.Equal(3, policy.MaxRetryCount);
        Assert.Equal(1000, policy.RetryIntervalMilliseconds);
        Assert.True(policy.UseExponentialBackoff);
        Assert.Equal(2.0, policy.BackoffMultiplier);
        Assert.Equal(60000, policy.MaxRetryIntervalMilliseconds);
    }

    /// <summary>
    /// Default 与新建实例等价
    /// </summary>
    [Fact]
    public void Default_MatchesFreshInstance()
    {
        var policy = JobRetryPolicy.Default;

        Assert.Equal(3, policy.MaxRetryCount);
        Assert.Equal(1000, policy.RetryIntervalMilliseconds);
        Assert.True(policy.UseExponentialBackoff);
    }

    /// <summary>
    /// None 表示不重试，其余参数保持默认
    /// </summary>
    [Fact]
    public void None_DisablesRetryOnly()
    {
        var policy = JobRetryPolicy.None;

        Assert.Equal(0, policy.MaxRetryCount);
        Assert.Equal(1000, policy.RetryIntervalMilliseconds);
        Assert.Equal(60000, policy.MaxRetryIntervalMilliseconds);
    }

    /// <summary>
    /// 静态属性每次返回全新实例，调用方修改不会污染其他任务
    /// </summary>
    [Fact]
    public void StaticPolicies_ReturnIndependentInstances()
    {
        var first = JobRetryPolicy.Default;
        first.MaxRetryCount = 99;

        Assert.Equal(3, JobRetryPolicy.Default.MaxRetryCount);
        Assert.NotSame(JobRetryPolicy.Default, JobRetryPolicy.Default);
        Assert.NotSame(JobRetryPolicy.None, JobRetryPolicy.None);
    }

    /// <summary>
    /// 关闭指数退避时，任何一次重试的间隔都等于固定间隔
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(10)]
    public void CalculateDelay_WithoutExponentialBackoff_IsConstant(int attemptNumber)
    {
        var policy = new JobRetryPolicy
        {
            RetryIntervalMilliseconds = 250,
            UseExponentialBackoff = false
        };

        Assert.Equal(TimeSpan.FromMilliseconds(250), policy.CalculateDelay(attemptNumber));
    }

    /// <summary>
    /// 开启指数退避时按倍数逐次放大
    /// </summary>
    [Theory]
    [InlineData(1, 1000)]
    [InlineData(2, 2000)]
    [InlineData(3, 4000)]
    [InlineData(4, 8000)]
    public void CalculateDelay_WithExponentialBackoff_GrowsByMultiplier(int attemptNumber, int expectedMilliseconds)
    {
        var policy = new JobRetryPolicy();

        Assert.Equal(TimeSpan.FromMilliseconds(expectedMilliseconds), policy.CalculateDelay(attemptNumber));
    }

    /// <summary>
    /// 退避间隔被最大间隔封顶，不会无限放大
    /// </summary>
    [Theory]
    [InlineData(7)]
    [InlineData(20)]
    [InlineData(100)]
    public void CalculateDelay_WhenBackoffExceedsCap_IsClampedToMaxInterval(int attemptNumber)
    {
        var policy = new JobRetryPolicy();

        Assert.Equal(TimeSpan.FromMilliseconds(60000), policy.CalculateDelay(attemptNumber));
    }

    /// <summary>
    /// 自定义倍数与起步间隔同样生效
    /// </summary>
    [Fact]
    public void CalculateDelay_WithCustomMultiplier_UsesConfiguredValues()
    {
        var policy = new JobRetryPolicy
        {
            RetryIntervalMilliseconds = 100,
            BackoffMultiplier = 3.0,
            MaxRetryIntervalMilliseconds = 100000
        };

        Assert.Equal(TimeSpan.FromMilliseconds(100), policy.CalculateDelay(1));
        Assert.Equal(TimeSpan.FromMilliseconds(300), policy.CalculateDelay(2));
        Assert.Equal(TimeSpan.FromMilliseconds(900), policy.CalculateDelay(3));
    }

    /// <summary>
    /// 倍数为 1 时退化为固定间隔
    /// </summary>
    [Fact]
    public void CalculateDelay_WithMultiplierOne_DegradesToConstantInterval()
    {
        var policy = new JobRetryPolicy
        {
            RetryIntervalMilliseconds = 500,
            BackoffMultiplier = 1.0
        };

        Assert.Equal(TimeSpan.FromMilliseconds(500), policy.CalculateDelay(1));
        Assert.Equal(TimeSpan.FromMilliseconds(500), policy.CalculateDelay(5));
    }
}
