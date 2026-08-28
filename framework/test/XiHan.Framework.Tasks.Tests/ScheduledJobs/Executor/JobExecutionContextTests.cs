// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Tasks.ScheduledJobs.Executor;
using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs.Executor;

/// <summary>
/// JobExecutionContext 执行上下文测试
/// </summary>
/// <remarks>
/// 上下文是中间件与任务体之间唯一的数据通道，重点验证：参数永不为 null、追踪标识必定有值、
/// 租户与取消令牌原样透传、尝试次数从 1 起算且可被重试中间件改写。
/// </remarks>
public class JobExecutionContextTests
{
    /// <summary>
    /// 任务实例为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void Constructor_WhenJobInstanceIsNull_ThrowsArgumentNullException()
    {
        var serviceProvider = new ServiceCollection().BuildServiceProvider();

        Assert.Throws<ArgumentNullException>(() => new JobExecutionContext(null!, null, serviceProvider));
    }

    /// <summary>
    /// 服务提供者为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void Constructor_WhenServiceProviderIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new JobExecutionContext(CreateInstance(), null, null!));
    }

    /// <summary>
    /// 不传参数时给出空字典而不是 null，调用方可以直接索引
    /// </summary>
    [Fact]
    public void Constructor_WithoutParameters_ExposesEmptyDictionary()
    {
        var context = new JobExecutionContext(CreateInstance(), null, new ServiceCollection().BuildServiceProvider());

        Assert.NotNull(context.Parameters);
        Assert.Empty(context.Parameters);
    }

    /// <summary>
    /// 传入参数时原样持有同一个字典实例
    /// </summary>
    [Fact]
    public void Constructor_WithParameters_KeepsSameDictionaryInstance()
    {
        var parameters = new Dictionary<string, object?> { ["key"] = "value" };

        var context = new JobExecutionContext(CreateInstance(), parameters, new ServiceCollection().BuildServiceProvider());

        Assert.Same(parameters, context.Parameters);
        Assert.Equal("value", context.Parameters["key"]);
    }

    /// <summary>
    /// 任务实例带追踪标识时沿用该标识
    /// </summary>
    [Fact]
    public void Constructor_WhenInstanceHasTraceId_ReusesIt()
    {
        var instance = CreateInstance();
        instance.TraceId = "trace-from-instance";

        var context = new JobExecutionContext(instance, null, new ServiceCollection().BuildServiceProvider());

        Assert.Equal("trace-from-instance", context.TraceId);
    }

    /// <summary>
    /// 任务实例没有追踪标识时自动补一个，保证日志一定可关联
    /// </summary>
    [Fact]
    public void Constructor_WhenInstanceHasNoTraceId_GeneratesOne()
    {
        var instance = CreateInstance();
        instance.TraceId = null;

        var context = new JobExecutionContext(instance, null, new ServiceCollection().BuildServiceProvider());

        Assert.False(string.IsNullOrWhiteSpace(context.TraceId));
        Assert.Equal(32, context.TraceId.Length);
    }

    /// <summary>
    /// 租户标识来自任务实例
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData(88L)]
    public void Constructor_CopiesTenantIdFromInstance(long? tenantId)
    {
        var instance = CreateInstance();
        instance.TenantId = tenantId;

        var context = new JobExecutionContext(instance, null, new ServiceCollection().BuildServiceProvider());

        Assert.Equal(tenantId, context.TenantId);
    }

    /// <summary>
    /// 任务实例、服务提供者与取消令牌原样透传
    /// </summary>
    [Fact]
    public void Constructor_PassesThroughInstanceProviderAndToken()
    {
        var instance = CreateInstance();
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        using var cts = new CancellationTokenSource();

        var context = new JobExecutionContext(instance, null, serviceProvider, cts.Token);

        Assert.Same(instance, context.JobInstance);
        Assert.Same(serviceProvider, context.ServiceProvider);
        Assert.Equal(cts.Token, context.CancellationToken);
    }

    /// <summary>
    /// 不传取消令牌时使用 None
    /// </summary>
    [Fact]
    public void Constructor_WithoutCancellationToken_UsesNone()
    {
        var context = new JobExecutionContext(CreateInstance(), null, new ServiceCollection().BuildServiceProvider());

        Assert.Equal(CancellationToken.None, context.CancellationToken);
    }

    /// <summary>
    /// 开始时间取构造时刻，落在调用前后的区间内
    /// </summary>
    [Fact]
    public void Constructor_SetsStartedAtToConstructionMoment()
    {
        var before = DateTimeOffset.UtcNow;
        var context = new JobExecutionContext(CreateInstance(), null, new ServiceCollection().BuildServiceProvider());
        var after = DateTimeOffset.UtcNow;

        Assert.InRange(context.StartedAt, before, after);
    }

    /// <summary>
    /// 尝试次数从 1 起算，且允许重试中间件改写
    /// </summary>
    [Fact]
    public void AttemptCount_StartsAtOneAndIsWritable()
    {
        var context = new JobExecutionContext(CreateInstance(), null, new ServiceCollection().BuildServiceProvider());

        Assert.Equal(1, context.AttemptCount);

        context.AttemptCount = 4;
        Assert.Equal(4, context.AttemptCount);
    }

    /// <summary>
    /// 构造一个最小可用的任务实例
    /// </summary>
    private static JobInstance CreateInstance()
    {
        var jobInfo = new JobInfo
        {
            JobName = "context-job",
            JobType = typeof(JobExecutionContextTests),
            TriggerType = JobTriggerType.Manual
        };

        return new JobInstance
        {
            JobName = jobInfo.JobName,
            JobInfo = jobInfo,
            TriggerType = JobTriggerType.Manual
        };
    }
}
