// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Executor;
using XiHan.Framework.Tasks.ScheduledJobs.Models;
using XiHan.Framework.Tasks.ScheduledJobs.Pipeline;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs.Pipeline;

/// <summary>
/// TimeoutMiddleware 超时中间件测试
/// </summary>
/// <remarks>
/// 真正会触发超时的用例把超时阈值压到几十毫秒，并让下游等待在取消令牌上——超时由令牌驱动唤醒，
/// 不是靠固定 Sleep 熬时间。其余用例全部走同步路径。
/// </remarks>
public class TimeoutMiddlewareTests
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
        Assert.Throws<ArgumentNullException>(() => new TimeoutMiddleware(null!));
    }

    /// <summary>
    /// 超时阈值非正时不做包装，下游拿到的就是原始上下文
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task InvokeAsync_WhenTimeoutIsNonPositive_PassesOriginalContextThrough(int timeoutMilliseconds)
    {
        var middleware = new TimeoutMiddleware(NullLogger<TimeoutMiddleware>.Instance);
        var context = CreateContext(timeoutMilliseconds);
        IJobContext? observed = null;
        var expected = JobResult.Success();

        var result = await middleware.InvokeAsync(context, ctx =>
        {
            observed = ctx;
            return Task.FromResult(expected);
        });

        Assert.Same(context, observed);
        Assert.Same(expected, result);
    }

    /// <summary>
    /// 超时阈值为正时用包装上下文替换取消令牌，其余数据原样透传
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task InvokeAsync_WhenTimeoutConfigured_WrapsContextButKeepsPayload()
    {
        var middleware = new TimeoutMiddleware(NullLogger<TimeoutMiddleware>.Instance);
        var context = CreateContext(60_000);
        IJobContext? observed = null;

        await middleware.InvokeAsync(context, ctx =>
        {
            observed = ctx;
            return Task.FromResult(JobResult.Success());
        });

        Assert.NotNull(observed);
        Assert.NotSame(context, observed);
        Assert.Same(context.JobInstance, observed!.JobInstance);
        Assert.Same(context.Parameters, observed.Parameters);
        Assert.Same(context.ServiceProvider, observed.ServiceProvider);
        Assert.Equal(context.TraceId, observed.TraceId);
        Assert.Equal(context.StartedAt, observed.StartedAt);
        Assert.Equal(context.TenantId, observed.TenantId);
        Assert.NotEqual(context.CancellationToken, observed.CancellationToken);
    }

    /// <summary>
    /// 包装上下文对尝试次数的写入会落到内层上下文，重试中间件的计数不会丢
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task InvokeAsync_WhenWrappedContextWritesAttemptCount_ReflectsOnInnerContext()
    {
        var middleware = new TimeoutMiddleware(NullLogger<TimeoutMiddleware>.Instance);
        var context = CreateContext(60_000);

        await middleware.InvokeAsync(context, ctx =>
        {
            Assert.Equal(1, ctx.AttemptCount);
            ctx.AttemptCount = 5;
            return Task.FromResult(JobResult.Success());
        });

        Assert.Equal(5, context.AttemptCount);
    }

    /// <summary>
    /// 未超时的正常执行结果原样返回
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task InvokeAsync_WhenCompletedBeforeTimeout_ReturnsDownstreamResult()
    {
        var middleware = new TimeoutMiddleware(NullLogger<TimeoutMiddleware>.Instance);
        var context = CreateContext(60_000);
        var expected = JobResult.Failure("业务失败但没超时");

        var result = await middleware.InvokeAsync(context, _ => Task.FromResult(expected));

        Assert.Same(expected, result);
    }

    /// <summary>
    /// 下游等待超过阈值时被取消，并转换为带超时说明的失败结果
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task InvokeAsync_WhenDownstreamExceedsTimeout_ReturnsTimeoutFailure()
    {
        var middleware = new TimeoutMiddleware(NullLogger<TimeoutMiddleware>.Instance);
        var context = CreateContext(50);

        var result = await middleware.InvokeAsync(context, async ctx =>
        {
            await Task.Delay(Timeout.Infinite, ctx.CancellationToken);
            return JobResult.Success();
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(JobStatus.Failed, result.Status);
        Assert.Contains("超时", result.ErrorMessage!, StringComparison.Ordinal);
        Assert.Contains("50", result.ErrorMessage!, StringComparison.Ordinal);
    }

    /// <summary>
    /// 超时令牌与外层令牌是联动的：外层取消也会唤醒下游
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task InvokeAsync_WhenOuterTokenCanceled_WrappedTokenIsCanceledToo()
    {
        using var cts = new CancellationTokenSource();
        var middleware = new TimeoutMiddleware(NullLogger<TimeoutMiddleware>.Instance);
        var context = CreateContext(60_000, cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => middleware.InvokeAsync(context, async ctx =>
        {
            await cts.CancelAsync();
            ctx.CancellationToken.ThrowIfCancellationRequested();
            return JobResult.Success();
        }));
    }

    /// <summary>
    /// 非取消类异常不被超时中间件吞掉，交由上层重试或兜底处理
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task InvokeAsync_WhenDownstreamThrowsOtherException_PropagatesIt()
    {
        var middleware = new TimeoutMiddleware(NullLogger<TimeoutMiddleware>.Instance);
        var context = CreateContext(60_000);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => middleware.InvokeAsync(context, _ => throw new InvalidOperationException("下游炸了")));
    }

    /// <summary>
    /// 构造一个带指定超时阈值的执行上下文
    /// </summary>
    private static JobExecutionContext CreateContext(int timeoutMilliseconds, CancellationToken cancellationToken = default)
    {
        var jobInfo = new JobInfo
        {
            JobName = "timeout-job",
            JobType = typeof(TimeoutMiddlewareTests),
            TriggerType = JobTriggerType.Manual,
            TimeoutMilliseconds = timeoutMilliseconds
        };

        var instance = new JobInstance
        {
            JobName = jobInfo.JobName,
            JobInfo = jobInfo,
            TriggerType = JobTriggerType.Manual,
            TenantId = 5L
        };

        return new JobExecutionContext(
            instance,
            new Dictionary<string, object?> { ["k"] = "v" },
            new ServiceCollection().BuildServiceProvider(),
            cancellationToken);
    }
}
