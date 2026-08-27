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
/// LockMiddleware 分布式锁中间件测试
/// </summary>
/// <remarks>
/// 这是"任务不允许并发"的第二道闸（第一道在调度器侧）：抢不到锁必须直接失败并且绝不执行任务体，
/// 抢到锁则无论成败都要释放。全部用手写的假锁提供者构造，不连任何真实的 Redis。
/// </remarks>
public class LockMiddlewareTests
{
    /// <summary>
    /// 日志器为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void Constructor_WhenLoggerIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new LockMiddleware(null!));
    }

    /// <summary>
    /// 允许并发的任务不走加锁路径
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenConcurrencyAllowed_SkipsLocking()
    {
        var provider = new StubLockProvider(new StubLockToken("job:lock:lock-job"));
        var middleware = new LockMiddleware(NullLogger<LockMiddleware>.Instance, provider);
        var context = CreateContext(allowConcurrent: true);
        var invoked = false;

        var result = await middleware.InvokeAsync(context, _ =>
        {
            invoked = true;
            return Task.FromResult(JobResult.Success());
        });

        Assert.True(invoked);
        Assert.True(result.IsSuccess);
        Assert.Equal(0, provider.AcquireAttempts);
    }

    /// <summary>
    /// 没有配置锁提供者时退化为直接执行
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenLockProviderIsAbsent_ExecutesDirectly()
    {
        var middleware = new LockMiddleware(NullLogger<LockMiddleware>.Instance);
        var context = CreateContext(allowConcurrent: false);
        var invoked = false;

        var result = await middleware.InvokeAsync(context, _ =>
        {
            invoked = true;
            return Task.FromResult(JobResult.Success());
        });

        Assert.True(invoked);
        Assert.True(result.IsSuccess);
    }

    /// <summary>
    /// 抢不到锁时直接失败，任务体绝不执行
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenLockNotAcquired_FailsWithoutRunningWorker()
    {
        var provider = new StubLockProvider(null);
        var middleware = new LockMiddleware(NullLogger<LockMiddleware>.Instance, provider);
        var context = CreateContext(allowConcurrent: false);
        var invoked = false;

        var result = await middleware.InvokeAsync(context, _ =>
        {
            invoked = true;
            return Task.FromResult(JobResult.Success());
        });

        Assert.False(invoked);
        Assert.False(result.IsSuccess);
        Assert.Equal(JobStatus.Failed, result.Status);
        Assert.Contains("锁", result.ErrorMessage!, StringComparison.Ordinal);
        Assert.Equal(1, provider.AcquireAttempts);
    }

    /// <summary>
    /// 抢到锁后执行任务体，并在结束时释放锁
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenLockAcquired_RunsWorkerAndReleasesLock()
    {
        var token = new StubLockToken("job:lock:lock-job");
        var provider = new StubLockProvider(token);
        var middleware = new LockMiddleware(NullLogger<LockMiddleware>.Instance, provider);
        var context = CreateContext(allowConcurrent: false);
        var invoked = false;

        var result = await middleware.InvokeAsync(context, _ =>
        {
            invoked = true;
            Assert.False(token.IsReleased);
            return Task.FromResult(JobResult.Success());
        });

        Assert.True(invoked);
        Assert.True(result.IsSuccess);
        Assert.True(token.IsReleased);
        Assert.Equal(1, token.AsyncDisposeCount);
    }

    /// <summary>
    /// 锁键按"job:lock:任务名"约定拼装，过期时间为任务超时再加 5 秒缓冲
    /// </summary>
    [Fact]
    public async Task InvokeAsync_UsesConventionalLockKeyAndExpiry()
    {
        var provider = new StubLockProvider(new StubLockToken("job:lock:lock-job"));
        var middleware = new LockMiddleware(NullLogger<LockMiddleware>.Instance, provider);
        var context = CreateContext(allowConcurrent: false, timeoutMilliseconds: 10_000);

        await middleware.InvokeAsync(context, _ => Task.FromResult(JobResult.Success()));

        Assert.Equal("job:lock:lock-job", provider.LastResourceKey);
        Assert.Equal(TimeSpan.FromMilliseconds(15_000), provider.LastExpiry);
    }

    /// <summary>
    /// 上下文的取消令牌透传给锁提供者
    /// </summary>
    [Fact]
    public async Task InvokeAsync_PassesCancellationTokenToLockProvider()
    {
        using var cts = new CancellationTokenSource();
        var provider = new StubLockProvider(new StubLockToken("job:lock:lock-job"));
        var middleware = new LockMiddleware(NullLogger<LockMiddleware>.Instance, provider);
        var context = CreateContext(allowConcurrent: false, cancellationToken: cts.Token);

        await middleware.InvokeAsync(context, _ => Task.FromResult(JobResult.Success()));

        Assert.Equal(cts.Token, provider.LastCancellationToken);
    }

    /// <summary>
    /// 任务体抛异常时锁仍被释放，异常继续向上冒泡
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenWorkerThrows_StillReleasesLock()
    {
        var token = new StubLockToken("job:lock:lock-job");
        var provider = new StubLockProvider(token);
        var middleware = new LockMiddleware(NullLogger<LockMiddleware>.Instance, provider);
        var context = CreateContext(allowConcurrent: false);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => middleware.InvokeAsync(context, _ => throw new InvalidOperationException("任务体炸了")));

        Assert.True(token.IsReleased);
    }

    /// <summary>
    /// 释放锁失败不能污染任务执行结果
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenReleaseFails_KeepsWorkerResult()
    {
        var token = new StubLockToken("job:lock:lock-job") { ThrowOnDispose = true };
        var provider = new StubLockProvider(token);
        var middleware = new LockMiddleware(NullLogger<LockMiddleware>.Instance, provider);
        var context = CreateContext(allowConcurrent: false);
        var expected = JobResult.Success();

        var result = await middleware.InvokeAsync(context, _ => Task.FromResult(expected));

        Assert.Same(expected, result);
    }

    /// <summary>
    /// 构造一个执行上下文
    /// </summary>
    private static JobExecutionContext CreateContext(
        bool allowConcurrent,
        int timeoutMilliseconds = 300_000,
        CancellationToken cancellationToken = default)
    {
        var jobInfo = new JobInfo
        {
            JobName = "lock-job",
            JobType = typeof(LockMiddlewareTests),
            TriggerType = JobTriggerType.Manual,
            AllowConcurrent = allowConcurrent,
            TimeoutMilliseconds = timeoutMilliseconds
        };

        var instance = new JobInstance
        {
            JobName = jobInfo.JobName,
            JobInfo = jobInfo,
            TriggerType = JobTriggerType.Manual
        };

        return new JobExecutionContext(instance, null, new ServiceCollection().BuildServiceProvider(), cancellationToken);
    }

    /// <summary>
    /// 可预置返回结果的假锁提供者
    /// </summary>
    private sealed class StubLockProvider : IJobLockProvider
    {
        private readonly ILockToken? _token;

        /// <summary>
        /// 构造函数
        /// </summary>
        public StubLockProvider(ILockToken? token)
        {
            _token = token;
        }

        /// <summary>
        /// 尝试加锁的次数
        /// </summary>
        public int AcquireAttempts { get; private set; }

        /// <summary>
        /// 最近一次的资源键
        /// </summary>
        public string? LastResourceKey { get; private set; }

        /// <summary>
        /// 最近一次的过期时间
        /// </summary>
        public TimeSpan LastExpiry { get; private set; }

        /// <summary>
        /// 最近一次的取消令牌
        /// </summary>
        public CancellationToken LastCancellationToken { get; private set; }

        /// <summary>
        /// 尝试获取锁
        /// </summary>
        public Task<ILockToken?> TryAcquireLockAsync(string resourceKey, TimeSpan expiry, CancellationToken cancellationToken = default)
        {
            AcquireAttempts++;
            LastResourceKey = resourceKey;
            LastExpiry = expiry;
            LastCancellationToken = cancellationToken;
            return Task.FromResult(_token);
        }
    }

    /// <summary>
    /// 记录释放行为的假锁令牌
    /// </summary>
    private sealed class StubLockToken : ILockToken
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        public StubLockToken(string resourceKey)
        {
            ResourceKey = resourceKey;
        }

        /// <summary>
        /// 释放时是否抛异常
        /// </summary>
        public bool ThrowOnDispose { get; init; }

        /// <summary>
        /// 异步释放被调用的次数
        /// </summary>
        public int AsyncDisposeCount { get; private set; }

        /// <summary>
        /// 资源键
        /// </summary>
        public string ResourceKey { get; }

        /// <summary>
        /// 锁标识
        /// </summary>
        public string LockId { get; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// 是否已释放
        /// </summary>
        public bool IsReleased { get; private set; }

        /// <summary>
        /// 释放锁
        /// </summary>
        public Task ReleaseAsync()
        {
            IsReleased = true;
            return Task.CompletedTask;
        }

        /// <summary>
        /// 同步释放
        /// </summary>
        public void Dispose()
        {
            IsReleased = true;
        }

        /// <summary>
        /// 异步释放
        /// </summary>
        public ValueTask DisposeAsync()
        {
            AsyncDisposeCount++;
            IsReleased = true;

            if (ThrowOnDispose)
            {
                throw new InvalidOperationException("释放锁失败");
            }

            return ValueTask.CompletedTask;
        }
    }
}
