// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Executor;
using XiHan.Framework.Tasks.ScheduledJobs.Models;
using XiHan.Framework.Tasks.ScheduledJobs.Pipeline;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs.Pipeline;

/// <summary>
/// RetryMiddleware 重试中间件测试
/// </summary>
/// <remarks>
/// 全部用例都把重试间隔压到 1ms 且关闭指数退避，用"下游被调用了几次"来断言重试语义，
/// 不靠观察真实耗时，也不会拖慢测试。退避算法本身在 JobRetryPolicyTests 里单独验证。
/// </remarks>
public class RetryMiddlewareTests
{
    /// <summary>
    /// 兜底超时
    /// </summary>
    private const int TimeoutMilliseconds = 60_000;

    /// <summary>
    /// 日志器为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void Constructor_WhenLoggerIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new RetryMiddleware(null!));
    }

    /// <summary>
    /// 首次即成功时只调用一次下游，尝试次数为 1
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task InvokeAsync_WhenFirstAttemptSucceeds_DoesNotRetry()
    {
        var middleware = new RetryMiddleware(NullLogger<RetryMiddleware>.Instance);
        var context = CreateContext(CreateFastPolicy(2));
        var invocations = 0;
        var expected = JobResult.Success();

        var result = await middleware.InvokeAsync(context, _ =>
        {
            invocations++;
            return Task.FromResult(expected);
        });

        Assert.Same(expected, result);
        Assert.Equal(1, invocations);
        Assert.Equal(1, context.AttemptCount);
    }

    /// <summary>
    /// 前几次失败后成功时返回成功结果，调用次数等于实际尝试次数
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task InvokeAsync_WhenLaterAttemptSucceeds_ReturnsSuccessAfterRetries()
    {
        var middleware = new RetryMiddleware(NullLogger<RetryMiddleware>.Instance);
        var context = CreateContext(CreateFastPolicy(3));
        var invocations = 0;

        var result = await middleware.InvokeAsync(context, _ =>
        {
            invocations++;
            return Task.FromResult(invocations < 3 ? JobResult.Failure("暂时失败") : JobResult.Success());
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(3, invocations);
        Assert.Equal(3, context.AttemptCount);
    }

    /// <summary>
    /// 下游一直返回失败结果时，总调用次数为最大重试次数加一，并返回最后一次的失败结果
    /// </summary>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(3, 4)]
    public async Task InvokeAsync_WhenAlwaysFailing_InvokesMaxRetryCountPlusOnce(int maxRetryCount, int expectedInvocations)
    {
        var middleware = new RetryMiddleware(NullLogger<RetryMiddleware>.Instance);
        var context = CreateContext(CreateFastPolicy(maxRetryCount));
        var invocations = 0;
        JobResult? lastReturned = null;

        var result = await middleware.InvokeAsync(context, _ =>
        {
            invocations++;
            lastReturned = JobResult.Failure($"第 {invocations} 次失败");
            return Task.FromResult(lastReturned);
        });

        Assert.Equal(expectedInvocations, invocations);
        Assert.Same(lastReturned, result);
        Assert.False(result.IsSuccess);
    }

    /// <summary>
    /// 每次尝试都会把当前尝试序号写进上下文，供执行器统计重试次数
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task InvokeAsync_StampsAttemptNumberOnContextEveryRound()
    {
        var middleware = new RetryMiddleware(NullLogger<RetryMiddleware>.Instance);
        var context = CreateContext(CreateFastPolicy(2));
        var observed = new List<int>();

        await middleware.InvokeAsync(context, ctx =>
        {
            observed.Add(ctx.AttemptCount);
            return Task.FromResult(JobResult.Failure("失败"));
        });

        Assert.Equal(new[] { 1, 2, 3 }, observed);
    }

    /// <summary>
    /// 下游一直抛异常时最终返回失败结果，带出重试次数与最后一次异常
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task InvokeAsync_WhenAlwaysThrowing_ReturnsFailureCarryingLastException()
    {
        var middleware = new RetryMiddleware(NullLogger<RetryMiddleware>.Instance);
        var context = CreateContext(CreateFastPolicy(2));
        var invocations = 0;
        Exception? lastThrown = null;

        var result = await middleware.InvokeAsync(context, _ =>
        {
            invocations++;
            lastThrown = new InvalidOperationException($"第 {invocations} 次异常");
            throw lastThrown;
        });

        Assert.Equal(3, invocations);
        Assert.False(result.IsSuccess);
        Assert.Equal(JobStatus.Failed, result.Status);
        Assert.Same(lastThrown, result.Exception);
        Assert.Contains("已重试 2 次", result.ErrorMessage!, StringComparison.Ordinal);
        Assert.Contains("第 3 次异常", result.ErrorMessage!, StringComparison.Ordinal);
    }

    /// <summary>
    /// 抛异常后又成功时返回成功结果，异常被吞在重试内部
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task InvokeAsync_WhenThrowThenSucceed_ReturnsSuccess()
    {
        var middleware = new RetryMiddleware(NullLogger<RetryMiddleware>.Instance);
        var context = CreateContext(CreateFastPolicy(2));
        var invocations = 0;

        var result = await middleware.InvokeAsync(context, _ =>
        {
            invocations++;
            return invocations == 1
                ? throw new InvalidOperationException("首次异常")
                : Task.FromResult(JobResult.Success());
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(2, invocations);
    }

    /// <summary>
    /// 重试策略为空时退化为不重试，只调用一次下游
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task InvokeAsync_WhenRetryPolicyIsNull_DoesNotRetry()
    {
        var middleware = new RetryMiddleware(NullLogger<RetryMiddleware>.Instance);
        var context = CreateContext(null);
        var invocations = 0;

        var result = await middleware.InvokeAsync(context, _ =>
        {
            invocations++;
            return Task.FromResult(JobResult.Failure("失败"));
        });

        Assert.Equal(1, invocations);
        Assert.False(result.IsSuccess);
    }

    /// <summary>
    /// 任务已被取消时立即返回取消结果，不再消耗重试次数
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task InvokeAsync_WhenCanceled_ReturnsCanceledWithoutRetrying()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var middleware = new RetryMiddleware(NullLogger<RetryMiddleware>.Instance);
        var context = CreateContext(CreateFastPolicy(3), cts.Token);
        var invocations = 0;

        var result = await middleware.InvokeAsync(context, _ =>
        {
            invocations++;
            throw new OperationCanceledException(cts.Token);
        });

        Assert.Equal(1, invocations);
        Assert.False(result.IsSuccess);
        Assert.Equal(JobStatus.Canceled, result.Status);
    }

    /// <summary>
    /// 关闭指数退避后每次重试的间隔恒定，不会随次数放大
    /// </summary>
    [Fact]
    public void RetryPolicy_WithoutExponentialBackoff_KeepsDelayConstant()
    {
        var policy = CreateFastPolicy(3);

        Assert.Equal(policy.CalculateDelay(1), policy.CalculateDelay(3));
    }

    /// <summary>
    /// 构造一个 1ms 间隔、无退避的重试策略，保证用例不产生可感知的等待
    /// </summary>
    private static JobRetryPolicy CreateFastPolicy(int maxRetryCount)
    {
        return new JobRetryPolicy
        {
            MaxRetryCount = maxRetryCount,
            RetryIntervalMilliseconds = 1,
            UseExponentialBackoff = false
        };
    }

    /// <summary>
    /// 构造一个挂着指定重试策略的执行上下文
    /// </summary>
    private static JobExecutionContext CreateContext(JobRetryPolicy? retryPolicy, CancellationToken cancellationToken = default)
    {
        var jobInfo = new JobInfo
        {
            JobName = "retry-job",
            JobType = typeof(RetryMiddlewareTests),
            TriggerType = JobTriggerType.Manual,
            RetryPolicy = retryPolicy!
        };

        var instance = new JobInstance
        {
            JobName = jobInfo.JobName,
            JobInfo = jobInfo,
            TriggerType = JobTriggerType.Manual
        };

        return new JobExecutionContext(instance, null, new ServiceCollection().BuildServiceProvider(), cancellationToken);
    }
}
