// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Tasks.BackgroundServices;
using XiHan.Framework.Tasks.Tests.BackgroundServices.Fakes;
using XiHan.Framework.Utils.Diagnostics.RetryPolicys;

namespace XiHan.Framework.Tasks.Tests.BackgroundServices;

/// <summary>
/// 后台服务基类优雅停机测试
/// </summary>
/// <remarks>
/// 停机这段的契约有两条，都只在"停机时真有在途任务"的前提下才看得出来，
/// 既有用例里的在途任务一收到取消就自己结束，正好绕开了这两条：
/// <list type="number">
/// <item>等在途任务收尾不受停止令牌约束，只受 <c>ShutdownTimeoutMilliseconds</c> 约束，
/// 且等待结束后 <c>ExecuteAsync</c> 要正常收场而不是以 Canceled 结束；</item>
/// <item>停止令牌要一路传到重试策略的退避等待里，卡在退避里的任务必须立刻被打断，
/// 而不是把停机拖到退避时长那么久。</item>
/// </list>
/// 用例用 <see cref="GatedBackgroundService"/> 制造"必然还没收尾"的在途任务，
/// 从而让这两条在时序上确定可测，而不是碰运气。
/// </remarks>
public class XiHanBackgroundServiceBaseShutdownTests
{
    /// <summary>
    /// 兜底超时
    /// </summary>
    private const int TimeoutMilliseconds = 60_000;

    /// <summary>
    /// 在途任务迟迟不收尾时，停机按配置的超时等满再强停，且主循环正常结束
    /// </summary>
    /// <remarks>
    /// 修复前 <c>WaitAsync</c> 收的是已经取消的停止令牌，只要在途集合非空就立刻返回已取消的任务，
    /// 于是一毫秒都不等、异常还穿出 finally 让 <c>ExecuteAsync</c> 以 Canceled 结束。
    /// </remarks>
    /// <returns>任务</returns>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task StopAsync_WhenInFlightTaskOutlivesShutdown_WaitsForConfiguredTimeout()
    {
        using var service = CreateGatedService(new XiHanBackgroundServiceOptions
        {
            MaxConcurrentTasks = 1,
            IdleDelayMilliseconds = 5,
            EnableRetry = false,
            ShutdownTimeoutMilliseconds = 500
        });

        service.EnqueueBatch(new SimpleBackgroundTaskItem("gated"));

        await service.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => service.StartedTaskIds.Count == 1, "任务应已进入处理并挂在闸门上");

        var startedAt = Environment.TickCount64;
        await service.StopAsync(TestContext.Current.CancellationToken);
        var elapsed = Environment.TickCount64 - startedAt;

        // 放掉那个永远等不到的在途任务，避免它挂到整轮测试结束
        service.ReleaseGate();

        Assert.True(elapsed >= 300, $"应按 ShutdownTimeoutMilliseconds 等待在途任务收尾，实际只等了 {elapsed} 毫秒");
        Assert.NotNull(service.ExecuteTask);
        Assert.True(service.ExecuteTask!.IsCompletedSuccessfully);
    }

    /// <summary>
    /// 在途任务在超时前收尾时，停机会等到它真正跑完（统计与在途计数都已归位）才返回
    /// </summary>
    /// <returns>任务</returns>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task StopAsync_WhenInFlightTaskFinishesBeforeTimeout_WaitsUntilItIsAccounted()
    {
        using var service = CreateGatedService(new XiHanBackgroundServiceOptions
        {
            MaxConcurrentTasks = 1,
            IdleDelayMilliseconds = 5,
            EnableRetry = false,
            ShutdownTimeoutMilliseconds = 10_000
        });

        service.EnqueueBatch(new SimpleBackgroundTaskItem("gated"));

        await service.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => service.StartedTaskIds.Count == 1, "任务应已进入处理并挂在闸门上");

        var stopTask = service.StopAsync(TestContext.Current.CancellationToken);

        // 主循环从"收到取消"到"进入等待收尾"只是几行同步代码，这里让出一小段时间让它走完，
        // 保证闸门是在等待收尾期间才打开的——否则测的就不是"停机会等在途任务"这件事了
        await Task.Delay(50, TestContext.Current.CancellationToken);
        service.ReleaseGate();
        await stopTask;

        Assert.Equal(1, service.GetStatistics().TotalTasksProcessed);
        Assert.Equal(0, service.GetServiceStatus().CurrentRunningTasks);
        Assert.NotNull(service.ExecuteTask);
        Assert.True(service.ExecuteTask!.IsCompletedSuccessfully);
    }

    /// <summary>
    /// 任务正卡在重试退避等待里时，停机立刻打断退避，不等满退避时长
    /// </summary>
    /// <remarks>
    /// 退避是 <c>await Task.Delay(delay, cancellationToken)</c>，修复前没把合成令牌传给重试策略，
    /// 拿到的是 <c>default</c>，退避一秒都不会少等。这里把退避设成 10 秒、停机超时设成 3 秒，
    /// 只要退避没被打断，停机就必然被拖到超时上限。
    /// </remarks>
    /// <returns>任务</returns>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task StopAsync_WhenRetryBackoffPending_CancelsBackoffInsteadOfWaitingItOut()
    {
        using var service = new RecordingBackgroundService(
            NullLogger<RecordingBackgroundService>.Instance,
            Microsoft.Extensions.Options.Options.Create(new XiHanBackgroundServiceOptions
            {
                MaxConcurrentTasks = 1,
                IdleDelayMilliseconds = 5,
                EnableRetry = false,
                ShutdownTimeoutMilliseconds = 3_000
            }),
            null,
            RetryPolicyFactory.WithFixedDelay(3, TimeSpan.FromSeconds(10)))
        {
            ProcessException = new InvalidOperationException("处理失败")
        };

        service.EnqueueBatch(new SimpleBackgroundTaskItem("backoff"));

        await service.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => service.ProcessedTaskIds.Count == 1, "首次尝试应已失败并进入退避等待");

        var startedAt = Environment.TickCount64;
        await service.StopAsync(TestContext.Current.CancellationToken);
        var elapsed = Environment.TickCount64 - startedAt;

        Assert.True(elapsed < 1_500, $"退避等待应随停止令牌立即中断，实际等了 {elapsed} 毫秒");

        // 退避被打断意味着第二次尝试根本没发生，任务以"被取消"收场而不是走失败回调
        Assert.Equal(1, service.ProcessedTaskIds.Count);
        Assert.Empty(service.Failures);
        Assert.Equal(1, service.GetStatistics().TotalTasksFailed);
        Assert.NotNull(service.ExecuteTask);
        Assert.True(service.ExecuteTask!.IsCompletedSuccessfully);
    }

    /// <summary>
    /// 轮询等待条件成立
    /// </summary>
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
    /// 创建任务体挂在闸门上的被测服务
    /// </summary>
    /// <param name="options">配置选项</param>
    /// <returns>被测服务</returns>
    private static GatedBackgroundService CreateGatedService(XiHanBackgroundServiceOptions options)
    {
        return new GatedBackgroundService(
            NullLogger<GatedBackgroundService>.Instance,
            Microsoft.Extensions.Options.Options.Create(options));
    }
}
