// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Tasks.BackgroundServices;
using XiHan.Framework.Tasks.Tests.BackgroundServices.Fakes;
using XiHan.Framework.Utils.Diagnostics.RetryPolicys;

namespace XiHan.Framework.Tasks.Tests.BackgroundServices;

/// <summary>
/// 后台服务基类模板方法测试
/// </summary>
/// <remarks>
/// 基类把"抽取 → 并发处理 → 重试 → 失败回调 → 统计"固化成模板，子类只填两个抽象方法。
/// 这里用一个最小子类验证模板本身的行为：
/// 构造期的重试策略取舍、动态配置的接管与退订、主循环的抽取额度与暂停分支、
/// 重试次数与失败回调的配合，以及停止时取消令牌的向下传播。
/// <para>
/// 所有涉及运行的用例都把空闲延迟压到 5 毫秒，并用条件轮询同步，不做固定时长等待。
/// </para>
/// </remarks>
public class XiHanBackgroundServiceBaseTests
{
    /// <summary>
    /// 兜底超时
    /// </summary>
    private const int TimeoutMilliseconds = 60_000;

    /// <summary>
    /// 日志为 null 时抛出空引用参数异常
    /// </summary>
    [Fact]
    public void Constructor_WhenLoggerNull_ThrowsArgumentNullException()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new XiHanBackgroundServiceOptions());

        Assert.Throws<ArgumentNullException>(() => new RecordingBackgroundService(null!, options));
    }

    /// <summary>
    /// 选项为 null 时抛出空引用参数异常
    /// </summary>
    [Fact]
    public void Constructor_WhenOptionsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RecordingBackgroundService(NullLogger<RecordingBackgroundService>.Instance, null!));
    }

    /// <summary>
    /// 开启重试且未传策略时，基类自建默认重试策略
    /// </summary>
    [Fact]
    public void Constructor_WhenRetryEnabled_CreatesDefaultRetryPolicy()
    {
        using var service = CreateService(new XiHanBackgroundServiceOptions { EnableRetry = true });

        Assert.True(service.GetServiceStatus().RetryEnabled);
    }

    /// <summary>
    /// 关闭重试且未传策略时不启用重试
    /// </summary>
    [Fact]
    public void Constructor_WhenRetryDisabled_HasNoRetryPolicy()
    {
        using var service = CreateService(new XiHanBackgroundServiceOptions { EnableRetry = false });

        Assert.False(service.GetServiceStatus().RetryEnabled);
    }

    /// <summary>
    /// 显式传入的重试策略优先于开关：即使关闭重试也照用
    /// </summary>
    [Fact]
    public void Constructor_WhenRetryPolicyProvided_TakesPrecedenceOverSwitch()
    {
        using var service = CreateService(
            new XiHanBackgroundServiceOptions { EnableRetry = false },
            retryPolicy: RetryPolicyFactory.WithImmediateRetry(1));

        Assert.True(service.GetServiceStatus().RetryEnabled);
    }

    /// <summary>
    /// 注入的动态配置被原样接管
    /// </summary>
    [Fact]
    public void GetDynamicConfig_WhenInjected_ReturnsSameInstance()
    {
        var options = new XiHanBackgroundServiceOptions();
        var dynamicConfig = new RecordingDynamicServiceConfig(Microsoft.Extensions.Options.Options.Create(options));
        using var service = CreateService(options, dynamicConfig);

        Assert.Same(dynamicConfig, service.GetDynamicConfig());
    }

    /// <summary>
    /// 未注入动态配置时按静态选项自建一份
    /// </summary>
    [Fact]
    public void GetDynamicConfig_WhenNotInjected_CreatesDefaultFromOptions()
    {
        using var service = CreateService(new XiHanBackgroundServiceOptions
        {
            MaxConcurrentTasks = 11,
            IdleDelayMilliseconds = 22
        });

        var config = service.GetDynamicConfig();

        Assert.IsType<DynamicServiceConfig>(config);
        Assert.Equal(11, config.MaxConcurrentTasks);
        Assert.Equal(22, config.IdleDelayMilliseconds);
    }

    /// <summary>
    /// 服务状态快照反映服务名与动态配置当前值
    /// </summary>
    [Fact]
    public void GetServiceStatus_ReflectsServiceNameAndDynamicConfig()
    {
        using var service = CreateService(new XiHanBackgroundServiceOptions
        {
            MaxConcurrentTasks = 6,
            IdleDelayMilliseconds = 321,
            EnableRetry = true
        });

        var status = service.GetServiceStatus();

        Assert.Equal(nameof(RecordingBackgroundService), status.ServiceName);
        Assert.True(status.IsTaskProcessingEnabled);
        Assert.Equal(6, status.MaxConcurrentTasks);
        Assert.Equal(321, status.IdleDelayMilliseconds);
        Assert.Equal(0, status.CurrentRunningTasks);
        Assert.True(status.RetryEnabled);
        Assert.NotNull(status.Statistics);
    }

    /// <summary>
    /// 尚未处理任何任务时统计全为零
    /// </summary>
    [Fact]
    public void GetStatistics_BeforeAnyWork_IsEmpty()
    {
        using var service = CreateService(new XiHanBackgroundServiceOptions());

        var statistics = service.GetStatistics();

        Assert.Equal(0, statistics.TotalTasksProcessed);
        Assert.Equal(0, statistics.TotalTasksFailed);
        Assert.Equal(0, statistics.TotalTasksRetried);
    }

    /// <summary>
    /// 动态配置变更会回调到基类的变更钩子
    /// </summary>
    [Fact]
    public void OnConfigChanged_WhenDynamicConfigUpdated_IsInvoked()
    {
        var options = new XiHanBackgroundServiceOptions();
        var dynamicConfig = new RecordingDynamicServiceConfig(Microsoft.Extensions.Options.Options.Create(options));
        using var service = CreateService(options, dynamicConfig);

        dynamicConfig.UpdateIdleDelay(123);

        var change = Assert.Single(service.ConfigChanges);
        Assert.Equal("IdleDelayMilliseconds", change.PropertyName);
        Assert.Equal(123, change.NewValue);
    }

    /// <summary>
    /// 释放后退订配置变更，避免服务实例被事件源长期持有
    /// </summary>
    [Fact]
    public void Dispose_UnsubscribesFromConfigChanged()
    {
        var options = new XiHanBackgroundServiceOptions();
        var dynamicConfig = new RecordingDynamicServiceConfig(Microsoft.Extensions.Options.Options.Create(options));
        var service = CreateService(options, dynamicConfig);

        dynamicConfig.UpdateMaxConcurrentTasks(7);
        Assert.Single(service.ConfigChanges);

        service.Dispose();
        dynamicConfig.UpdateMaxConcurrentTasks(9);

        Assert.Single(service.ConfigChanges);
    }

    /// <summary>
    /// 主循环把抽取到的任务逐个交给处理方法，并计入成功统计
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task ExecuteAsync_ProcessesFetchedItems()
    {
        using var service = CreateService(new XiHanBackgroundServiceOptions
        {
            MaxConcurrentTasks = 3,
            IdleDelayMilliseconds = 5,
            EnableRetry = false,
            ShutdownTimeoutMilliseconds = 500
        });

        service.EnqueueBatch(new SimpleBackgroundTaskItem("a"), new SimpleBackgroundTaskItem("b"));

        await service.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => service.GetStatistics().TotalTasksProcessed == 2, "两个任务都应处理成功");
        await service.StopAsync(TestContext.Current.CancellationToken);

        var statistics = service.GetStatistics();
        Assert.Equal(2, statistics.TotalTasksProcessed);
        Assert.Equal(0, statistics.TotalTasksFailed);
        Assert.Contains("a", service.ProcessedTaskIds);
        Assert.Contains("b", service.ProcessedTaskIds);
    }

    /// <summary>
    /// 首轮抽取的额度等于最大并发数（此时没有在途任务）
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task ExecuteAsync_FirstFetchAsksForFullConcurrency()
    {
        using var service = CreateService(new XiHanBackgroundServiceOptions
        {
            MaxConcurrentTasks = 4,
            IdleDelayMilliseconds = 5,
            EnableRetry = false
        });

        await service.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => service.FetchCallCount >= 1, "应至少抽取一次任务");
        await service.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(4, service.RequestedMaxCounts[0]);
    }

    /// <summary>
    /// 暂停任务处理时完全不抽取任务，恢复后立即继续
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task ExecuteAsync_WhenTaskProcessingDisabled_DoesNotFetchUntilResumed()
    {
        var options = new XiHanBackgroundServiceOptions
        {
            MaxConcurrentTasks = 2,
            IdleDelayMilliseconds = 5,
            EnableRetry = false,
            ShutdownTimeoutMilliseconds = 500
        };
        var dynamicConfig = new RecordingDynamicServiceConfig(Microsoft.Extensions.Options.Options.Create(options));
        dynamicConfig.SetTaskProcessingEnabled(false);

        using var service = CreateService(options, dynamicConfig);
        service.EnqueueBatch(new SimpleBackgroundTaskItem("paused"));

        await service.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => dynamicConfig.IdleDelayReadCount >= 3, "暂停期间主循环应持续空转");

        Assert.Equal(0, service.FetchCallCount);

        dynamicConfig.SetTaskProcessingEnabled(true);
        await WaitUntilAsync(() => service.ProcessedTaskIds.Count == 1, "恢复后应抽取并处理任务");
        await service.StopAsync(TestContext.Current.CancellationToken);

        Assert.Contains("paused", service.ProcessedTaskIds);
    }

    /// <summary>
    /// 处理失败时按重试策略重试，重试次数计入统计，最终失败走失败回调
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task ExecuteAsync_WhenProcessingFails_RetriesThenReportsFailure()
    {
        using var service = CreateService(
            new XiHanBackgroundServiceOptions
            {
                MaxConcurrentTasks = 1,
                IdleDelayMilliseconds = 5,
                EnableRetry = false,
                ShutdownTimeoutMilliseconds = 500
            },
            retryPolicy: RetryPolicyFactory.WithImmediateRetry(2));

        service.ProcessException = new InvalidOperationException("处理失败");
        service.EnqueueBatch(new SimpleBackgroundTaskItem("fail"));

        await service.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => service.GetStatistics().TotalTasksFailed == 1, "任务应在重试耗尽后被判失败");
        await service.StopAsync(TestContext.Current.CancellationToken);

        var statistics = service.GetStatistics();
        Assert.Equal(0, statistics.TotalTasksProcessed);
        Assert.Equal(1, statistics.TotalTasksFailed);
        Assert.Equal(2, statistics.TotalTasksRetried);
        Assert.Equal(3, service.ProcessedTaskIds.Count);

        var failure = Assert.Single(service.Failures);
        Assert.IsType<InvalidOperationException>(failure);
    }

    /// <summary>
    /// 未配置重试策略时失败不重试，只调用一次处理方法
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task ExecuteAsync_WhenRetryDisabled_DoesNotRetryFailedItem()
    {
        using var service = CreateService(new XiHanBackgroundServiceOptions
        {
            MaxConcurrentTasks = 1,
            IdleDelayMilliseconds = 5,
            EnableRetry = false,
            ShutdownTimeoutMilliseconds = 500
        });

        service.ProcessException = new InvalidOperationException("处理失败");
        service.EnqueueBatch(new SimpleBackgroundTaskItem("fail-once"));

        await service.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => service.GetStatistics().TotalTasksFailed == 1, "任务应被判失败");
        await service.StopAsync(TestContext.Current.CancellationToken);

        Assert.Single(service.ProcessedTaskIds);
        Assert.Equal(0, service.GetStatistics().TotalTasksRetried);
    }

    /// <summary>
    /// 停止服务时取消令牌传播到正在处理的任务
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task StopAsync_PropagatesCancellationIntoProcessItem()
    {
        using var service = CreateService(new XiHanBackgroundServiceOptions
        {
            MaxConcurrentTasks = 1,
            IdleDelayMilliseconds = 5,
            EnableRetry = false,
            ShutdownTimeoutMilliseconds = 200
        });

        service.BlockUntilCancelled = true;
        service.EnqueueBatch(new SimpleBackgroundTaskItem("blocking"));

        await service.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => service.ProcessedTaskIds.Count == 1, "任务应已进入处理");

        var token = service.LastProcessToken;
        Assert.True(token.CanBeCanceled);
        Assert.False(token.IsCancellationRequested);

        await service.StopAsync(TestContext.Current.CancellationToken);

        Assert.True(service.LastProcessToken.IsCancellationRequested);
    }

    /// <summary>
    /// 停止时没有在途任务，主循环正常结束
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task StopAsync_WhenNoInFlightTasks_CompletesLoopSuccessfully()
    {
        using var service = CreateService(new XiHanBackgroundServiceOptions
        {
            MaxConcurrentTasks = 2,
            IdleDelayMilliseconds = 5,
            EnableRetry = false,
            ShutdownTimeoutMilliseconds = 500
        });

        await service.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => service.FetchCallCount >= 2, "主循环应已空转若干轮");
        await service.StopAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(service.ExecuteTask);
        Assert.True(service.ExecuteTask!.IsCompletedSuccessfully);
    }

    /// <summary>
    /// 运行期调大并发数后，下一轮抽取额度随之变大
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task ExecuteAsync_WhenConcurrencyRaisedAtRuntime_FetchesMoreItems()
    {
        var options = new XiHanBackgroundServiceOptions
        {
            MaxConcurrentTasks = 1,
            IdleDelayMilliseconds = 5,
            EnableRetry = false,
            ShutdownTimeoutMilliseconds = 500
        };
        var dynamicConfig = new RecordingDynamicServiceConfig(Microsoft.Extensions.Options.Options.Create(options));

        using var service = CreateService(options, dynamicConfig);

        await service.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => service.FetchCallCount >= 1, "应至少抽取一次任务");

        dynamicConfig.UpdateMaxConcurrentTasks(8);
        await WaitUntilAsync(() => service.RequestedMaxCounts.Contains(8), "调大并发数后抽取额度应随之变大");

        await service.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, service.RequestedMaxCounts[0]);
    }

    /// <summary>
    /// 轮询等待条件成立
    /// </summary>
    /// <remarks>
    /// 后台服务的观测点散落在另一个线程的 finally 里，没有可精确同步的单点信号，
    /// 因此用"短周期轮询 + 硬上限"代替固定睡眠：正常几毫秒就返回，只有真出问题才会走满上限并判失败。
    /// </remarks>
    /// <param name="condition">条件</param>
    /// <param name="description">条件描述</param>
    /// <returns>任务</returns>
    private static async Task WaitUntilAsync(Func<bool> condition, string description)
    {
        var deadline = Environment.TickCount64 + 10_000;
        while (Environment.TickCount64 <= deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        Assert.Fail($"等待条件超时：{description}");
    }

    /// <summary>
    /// 创建被测服务
    /// </summary>
    /// <param name="options">配置选项</param>
    /// <param name="dynamicConfig">动态配置</param>
    /// <param name="retryPolicy">重试策略</param>
    /// <returns>被测服务</returns>
    private static RecordingBackgroundService CreateService(
        XiHanBackgroundServiceOptions options,
        IDynamicServiceConfig? dynamicConfig = null,
        RetryPolicy? retryPolicy = null)
    {
        return new RecordingBackgroundService(
            NullLogger<RecordingBackgroundService>.Instance,
            Microsoft.Extensions.Options.Options.Create(options),
            dynamicConfig,
            retryPolicy);
    }
}
