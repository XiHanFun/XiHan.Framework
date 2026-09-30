// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Tasks.ScheduledJobs.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Configuration;
using XiHan.Framework.Tasks.ScheduledJobs.Extensions.DependencyInjection;
using XiHan.Framework.Tasks.ScheduledJobs.Hosting;
using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs.Hosting;

/// <summary>
/// JobHistoryCleanupService 历史清理后台服务测试
/// </summary>
public class JobHistoryCleanupServiceTests : IDisposable
{
    private const int TimeoutMilliseconds = 30_000;

    private readonly List<ServiceProvider> _providers = [];

    private static readonly DateTimeOffset Now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 截止时间为当前时间减去保留天数，批量上限取自选项
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task RunOnceAsync_PassesCutoffAndBatchSizeToStore()
    {
        var store = new ScriptedBatchJobStore(3);
        var service = CreateService(store, new FakeTimeProvider(Now), options =>
        {
            options.HistoryRetentionDays = 7;
            options.HistoryCleanupBatchSize = 50;
        });

        var total = await service.RunOnceAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, total);
        var call = Assert.Single(store.Calls);
        Assert.Equal(Now.AddDays(-7), call.Cutoff);
        Assert.Equal(50, call.BatchSize);
    }

    /// <summary>
    /// 每批删满时继续，单轮最多执行配置的批数
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task RunOnceAsync_StopsAtMaxBatchesPerRun()
    {
        var store = new ScriptedBatchJobStore(Enumerable.Repeat(500, 20).ToArray());
        var service = CreateService(store, new FakeTimeProvider(Now), _ => { });

        var total = await service.RunOnceAsync(TestContext.Current.CancellationToken);

        Assert.Equal(10, store.Calls.Count);
        Assert.Equal(5000, total);
    }

    /// <summary>
    /// 某批删除数小于批量上限即停止本轮
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task RunOnceAsync_StopsWhenBatchIsNotFull()
    {
        var store = new ScriptedBatchJobStore(500, 500, 120, 500);
        var service = CreateService(store, new FakeTimeProvider(Now), _ => { });

        var total = await service.RunOnceAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, store.Calls.Count);
        Assert.Equal(1120, total);
    }

    /// <summary>
    /// 批次之间检查取消令牌，取消后不再发起下一批
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task RunOnceAsync_WhenCanceledBetweenBatches_StopsImmediately()
    {
        using var cts = new CancellationTokenSource();
        var store = new ScriptedBatchJobStore(500, 500, 500)
        {
            OnCall = count =>
            {
                if (count == 2)
                {
                    cts.Cancel();
                }
            }
        };
        var service = CreateService(store, new FakeTimeProvider(Now), _ => { });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.RunOnceAsync(cts.Token));

        Assert.Equal(2, store.Calls.Count);
    }

    /// <summary>
    /// 未覆写分批方法的第三方存储走默认回退：按保留天数一次清完并结束本轮
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task RunOnceAsync_WithLegacyStore_FallsBackToRetentionDaysCleanup()
    {
        var store = new LegacyJobStore();
        var service = CreateService(store, new FakeTimeProvider(DateTimeOffset.UtcNow), options => options.HistoryRetentionDays = 30);

        var total = await service.RunOnceAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, total);
        Assert.Equal([30], store.RetentionDaysCalls.ToArray());
    }

    /// <summary>
    /// 未启用时后台循环直接结束，从不调用存储
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task ExecuteAsync_WhenDisabled_NeverCallsStore()
    {
        var store = new ScriptedBatchJobStore(1);
        var timeProvider = new FakeTimeProvider(Now);
        using var service = CreateService(store, timeProvider, options => options.HistoryCleanupEnabled = false);

        await service.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => service.ExecuteTask is { IsCompleted: true });
        timeProvider.Advance(TimeSpan.FromDays(1));

        Assert.Empty(store.Calls);
        await service.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// 启用后按间隔触发一轮清理
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task ExecuteAsync_WhenEnabled_RunsOnInterval()
    {
        var store = new ScriptedBatchJobStore(1, 1, 1, 1, 1, 1, 1, 1, 1, 1);
        var timeProvider = new TimerSignalingTimeProvider(Now);
        using var service = CreateService(store, timeProvider, options =>
        {
            options.HistoryCleanupEnabled = true;
            options.HistoryCleanupIntervalMinutes = 15;
        });

        await service.StartAsync(TestContext.Current.CancellationToken);
        await timeProvider.TimerCreated.Task.WaitAsync(TestContext.Current.CancellationToken);

        timeProvider.Advance(TimeSpan.FromMinutes(14));
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Empty(store.Calls);

        timeProvider.Advance(TimeSpan.FromMinutes(1));
        await WaitUntilAsync(() => !store.Calls.IsEmpty);

        await service.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// 单轮抛出异常只记日志，后台循环继续运行
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task ExecuteAsync_WhenRoundThrows_KeepsRunning()
    {
        var store = new ScriptedBatchJobStore(1, 1, 1, 1, 1, 1, 1, 1, 1, 1) { ThrowOnFirstCall = true };
        var timeProvider = new FakeTimeProvider(Now);
        using var service = CreateService(store, timeProvider, options => options.HistoryCleanupEnabled = true);

        await service.StartAsync(TestContext.Current.CancellationToken);

        await WaitUntilAsync(() =>
        {
            timeProvider.Advance(TimeSpan.FromMinutes(60));
            return store.Calls.Count >= 2;
        });

        Assert.False(service.ExecuteTask!.IsCompleted);
        await service.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// 停止服务时后台循环立即结束
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task StopAsync_EndsBackgroundLoopPromptly()
    {
        var store = new ScriptedBatchJobStore(1);
        var timeProvider = new FakeTimeProvider(Now);
        using var service = CreateService(store, timeProvider, options => options.HistoryCleanupEnabled = true);

        await service.StartAsync(TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);

        Assert.True(service.ExecuteTask!.IsCompleted);
        Assert.Empty(store.Calls);
    }

    /// <summary>
    /// 清理选项默认关闭，默认间隔 60 分钟、每批 500 条、每轮最多 10 批
    /// </summary>
    [Fact]
    public void Options_Defaults()
    {
        var options = new XiHanJobOptions();

        Assert.False(options.HistoryCleanupEnabled);
        Assert.Equal(60, options.HistoryCleanupIntervalMinutes);
        Assert.Equal(500, options.HistoryCleanupBatchSize);
        Assert.Equal(10, options.HistoryCleanupMaxBatchesPerRun);
    }

    /// <summary>
    /// 非法的清理数值在读取选项时校验失败
    /// </summary>
    [Theory]
    [InlineData(0, 500, 10, 30)]
    [InlineData(71583, 500, 10, 30)]
    [InlineData(60, 0, 10, 30)]
    [InlineData(60, 500, 0, 30)]
    [InlineData(60, 500, 10, -1)]
    public void AddXiHanTasks_WithCleanupEnabledAndInvalidOptions_FailsValidation(int interval, int batchSize, int maxBatches, int retentionDays)
    {
        using var provider = BuildOptionsProvider(true, interval, batchSize, maxBatches, retentionDays);

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<XiHanJobOptions>>().Value);
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    /// <summary>
    /// 间隔取定时器允许的最大分钟数时通过校验
    /// </summary>
    [Fact]
    public void AddXiHanTasks_WithCleanupEnabledAndMaxInterval_PassesValidation()
    {
        using var provider = BuildOptionsProvider(true, 71582, 500, 10, 30);

        provider.GetRequiredService<IStartupValidator>().Validate();
        Assert.Equal(71582, provider.GetRequiredService<IOptions<XiHanJobOptions>>().Value.HistoryCleanupIntervalMinutes);
    }

    /// <summary>
    /// 未启用清理时不校验清理相关数值
    /// </summary>
    [Theory]
    [InlineData(0, 500, 10, 30)]
    [InlineData(71583, 500, 10, 30)]
    [InlineData(60, 0, 10, 30)]
    [InlineData(60, 500, 0, 30)]
    [InlineData(60, 500, 10, -1)]
    public void AddXiHanTasks_WithCleanupDisabledAndInvalidOptions_PassesValidation(int interval, int batchSize, int maxBatches, int retentionDays)
    {
        using var provider = BuildOptionsProvider(false, interval, batchSize, maxBatches, retentionDays);

        provider.GetRequiredService<IStartupValidator>().Validate();
        Assert.False(provider.GetRequiredService<IOptions<XiHanJobOptions>>().Value.HistoryCleanupEnabled);
    }

    /// <summary>
    /// 默认选项通过校验
    /// </summary>
    [Fact]
    public void AddXiHanTasks_WithDefaultOptions_PassesValidation()
    {
        var services = new ServiceCollection();
        services.AddXiHanTasks();
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IStartupValidator>().Validate();
        Assert.NotNull(provider.GetRequiredService<IOptions<XiHanJobOptions>>().Value);
    }

    /// <summary>
    /// 注册清理后台服务
    /// </summary>
    [Fact]
    public void AddXiHanTasks_RegistersCleanupHostedService()
    {
        var services = new ServiceCollection();
        services.AddXiHanTasks();
        services.AddXiHanTasks();

        Assert.Single(services, item => item.ServiceType == typeof(IHostedService)
            && item.ImplementationType == typeof(JobHistoryCleanupService));
    }

    /// <summary>
    /// 释放测试中创建的服务提供者
    /// </summary>
    public void Dispose()
    {
        foreach (var provider in _providers)
        {
            provider.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    private static ServiceProvider BuildOptionsProvider(bool enabled, int interval, int batchSize, int maxBatches, int retentionDays)
    {
        var services = new ServiceCollection();
        services.AddXiHanTasks(options =>
        {
            options.HistoryCleanupEnabled = enabled;
            options.HistoryCleanupIntervalMinutes = interval;
            options.HistoryCleanupBatchSize = batchSize;
            options.HistoryCleanupMaxBatchesPerRun = maxBatches;
            options.HistoryRetentionDays = retentionDays;
        });
        return services.BuildServiceProvider();
    }

    private JobHistoryCleanupService CreateService(IJobStore store, TimeProvider timeProvider, Action<XiHanJobOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddSingleton(store);
        var provider = services.BuildServiceProvider();
        _providers.Add(provider);
        var options = new XiHanJobOptions();
        configure(options);

        return new JobHistoryCleanupService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(options),
            NullLogger<JobHistoryCleanupService>.Instance,
            timeProvider);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        while (!condition())
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    private sealed class TimerSignalingTimeProvider(DateTimeOffset startDateTime) : FakeTimeProvider(startDateTime)
    {
        public TaskCompletionSource TimerCreated { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = base.CreateTimer(callback, state, dueTime, period);
            TimerCreated.TrySetResult();
            return timer;
        }
    }

    private sealed record CleanupCall(DateTimeOffset Cutoff, int BatchSize);

    private sealed class ScriptedBatchJobStore(params int[] results) : NoopJobStore, IJobStore
    {
        private int _callCount;

        public ConcurrentQueue<CleanupCall> Calls { get; } = new();

        public Action<int>? OnCall { get; init; }

        public bool ThrowOnFirstCall { get; init; }

        public Task<int> CleanupHistoryAsync(DateTimeOffset cutoff, int batchSize, CancellationToken cancellationToken = default)
        {
            Calls.Enqueue(new CleanupCall(cutoff, batchSize));
            var count = Interlocked.Increment(ref _callCount);
            OnCall?.Invoke(count);
            if (ThrowOnFirstCall && count == 1)
            {
                throw new InvalidOperationException("模拟清理失败");
            }

            return Task.FromResult(count <= results.Length ? results[count - 1] : 0);
        }
    }

    private sealed class LegacyJobStore : NoopJobStore
    {
        public ConcurrentQueue<int> RetentionDaysCalls { get; } = new();

        public override Task CleanupHistoryAsync(int retentionDays)
        {
            RetentionDaysCalls.Enqueue(retentionDays);
            return Task.CompletedTask;
        }
    }

    private abstract class NoopJobStore : IJobStore
    {
        public Task SaveJobInstanceAsync(JobInstance jobInstance)
        {
            return Task.CompletedTask;
        }

        public Task UpdateJobStatusAsync(string instanceId, JobStatus status)
        {
            return Task.CompletedTask;
        }

        public Task SaveJobHistoryAsync(JobHistory history)
        {
            return Task.CompletedTask;
        }

        public Task<JobInstance?> GetJobInstanceAsync(string instanceId)
        {
            return Task.FromResult<JobInstance?>(null);
        }

        public Task<IReadOnlyList<JobHistory>> GetJobHistoryAsync(string jobName, int pageIndex = 1, int pageSize = 20)
        {
            return Task.FromResult<IReadOnlyList<JobHistory>>([]);
        }

        public Task<IReadOnlyList<JobInstance>> GetRunningInstancesAsync(string jobName)
        {
            return Task.FromResult<IReadOnlyList<JobInstance>>([]);
        }

        public virtual Task CleanupHistoryAsync(int retentionDays)
        {
            return Task.CompletedTask;
        }
    }
}
