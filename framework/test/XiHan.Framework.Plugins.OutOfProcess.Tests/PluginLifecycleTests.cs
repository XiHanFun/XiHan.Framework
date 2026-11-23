// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Plugins.OutOfProcess.Tests.Fixtures;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Protocol.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests;

/// <summary>
/// 两个运行时共用的生命周期组件：状态与退避重启（<c>lifecycle.md</c> §6，ADR-0260）
/// </summary>
/// <remarks>
/// 进程外与 Wasm 宿主的端到端用例各自覆盖真实载体上的崩溃重启；这里用假载体与假时钟核对节奏本身
/// </remarks>
public sealed class PluginLifecycleTests
{
    /// <summary>
    /// 重启计数的时间窗
    /// </summary>
    private static readonly TimeSpan _window = TimeSpan.FromMinutes(10);

    [Fact]
    public async Task Restart_BacksOffFromOneSecondDoubling_AndStopsAtTheLimit()
    {
        var clock = new TimerSignalingClock();
        var carrier = new RecordingCarrier();
        using var lifetime = new CancellationTokenSource();
        var lifecycle = Create(carrier, clock, 3, lifetime.Token);

        lifecycle.MarkRestartPending();
        var restarting = lifecycle.RestartAsync();

        foreach (var (delay, attempt) in new[] { (1, 1), (2, 2), (4, 3) })
        {
            var scheduled = await carrier.NextScheduledAsync();
            Assert.Equal((TimeSpan.FromSeconds(delay), attempt), scheduled);
            // 退避期间的调用被挡住
            Assert.True(lifecycle.IsRestartPending);
            await clock.TimerCreatedAsync();
            clock.Advance(scheduled.Delay);
        }

        await restarting.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None);
        Assert.Equal(3, carrier.Recoveries);
        Assert.Equal(3, lifecycle.Restarts);
        Assert.Equal([(3, _window)], carrier.Exhausted);
        Assert.Empty(carrier.Restarted);
        // 次数用完后放开标志，后台不再重试
        Assert.False(lifecycle.IsRestartPending);
        Assert.False(carrier.HasScheduled);
    }

    [Fact]
    public async Task Restart_CountsOnlyTheRestartsInsideTheWindow()
    {
        var clock = new TimerSignalingClock();
        var carrier = new RecordingCarrier { Recover = static () => Result.Success() };
        using var lifetime = new CancellationTokenSource();
        var lifecycle = Create(carrier, clock, 5, lifetime.Token);

        Assert.Equal(TimeSpan.FromSeconds(1), await RestartOnceAsync(lifecycle, carrier, clock));
        // 紧接着再崩溃：窗内已有一次，退避翻倍
        Assert.Equal(TimeSpan.FromSeconds(2), await RestartOnceAsync(lifecycle, carrier, clock));

        // 稳定运行超过一个时间窗，退避归零
        clock.Advance(_window + TimeSpan.FromSeconds(1));
        Assert.Equal(TimeSpan.FromSeconds(1), await RestartOnceAsync(lifecycle, carrier, clock));

        Assert.Equal([1, 2, 3], carrier.Restarted);
        Assert.Equal(3, lifecycle.Restarts);
        Assert.False(lifecycle.IsRestartPending);
    }

    [Fact]
    public async Task Restart_CapsTheBackoffAtSixtySeconds()
    {
        var clock = new TimerSignalingClock();
        var carrier = new RecordingCarrier();
        using var lifetime = new CancellationTokenSource();
        var lifecycle = Create(carrier, clock, 8, lifetime.Token);

        var restarting = lifecycle.RestartAsync();
        var delays = new List<double>();
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var scheduled = await carrier.NextScheduledAsync();
            delays.Add(scheduled.Delay.TotalSeconds);
            await clock.TimerCreatedAsync();
            clock.Advance(scheduled.Delay);
        }

        await restarting.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None);
        Assert.Equal([1, 2, 4, 8, 16, 32, 60, 60], delays);
    }

    [Fact]
    public async Task Restart_WhenTheCarrierStopsDuringARetry_DoesNotRetryAgain()
    {
        var clock = new TimerSignalingClock();
        var carrier = new RecordingCarrier();
        using var lifetime = new CancellationTokenSource();
        var lifecycle = Create(carrier, clock, 5, lifetime.Token);
        // 声明登记不进去而按发现时被拒处理：重试失败，载体同时不再接受重启
        carrier.Recover = () =>
        {
            carrier.IsStopped = true;
            return Result.Failure(Error.Literal(PluginErrors.InvalidSource, "The declarations were refused."));
        };

        var restarting = lifecycle.RestartAsync();
        var scheduled = await carrier.NextScheduledAsync();
        await clock.TimerCreatedAsync();
        clock.Advance(scheduled.Delay);

        await restarting.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None);
        Assert.Equal(1, carrier.Recoveries);
        Assert.False(carrier.HasScheduled);
        Assert.Empty(carrier.Exhausted);
    }

    [Fact]
    public async Task Restart_CancelledDuringTheBackoff_EndsWithoutRetrying()
    {
        var clock = new TimerSignalingClock();
        var carrier = new RecordingCarrier();
        using var lifetime = new CancellationTokenSource();
        var lifecycle = Create(carrier, clock, 5, lifetime.Token);

        var restarting = lifecycle.RestartAsync();
        await carrier.NextScheduledAsync();
        await lifetime.CancelAsync();

        await restarting.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None);
        Assert.Equal(0, carrier.Recoveries);
        Assert.Equal(0, lifecycle.Restarts);
    }

    [Fact]
    public async Task ScheduleRestart_DoesNothing_WhenStoppedOrAlreadyPending()
    {
        var clock = new TimerSignalingClock();
        var carrier = new RecordingCarrier { IsStopped = true };
        using var lifetime = new CancellationTokenSource();
        var lifecycle = Create(carrier, clock, 5, lifetime.Token);

        lifecycle.ScheduleRestart();
        Assert.False(lifecycle.IsRestartPending);
        Assert.False(carrier.HasScheduled);

        carrier.IsStopped = false;
        lifecycle.ScheduleRestart();
        lifecycle.ScheduleRestart();
        Assert.True(lifecycle.IsRestartPending);
        await carrier.NextScheduledAsync();
        // 第二次调用没有再排一轮
        Assert.False(carrier.HasScheduled);
        await lifetime.CancelAsync();
    }

    [Fact]
    public void Enter_RecordsTheStateWithItsError_AndUnavailableCarriesTheCurrentState()
    {
        var carrier = new RecordingCarrier();
        var lifecycle = Create(carrier, new TimerSignalingClock(), 5, CancellationToken.None);
        var degraded = Error.Literal(PluginErrors.Unexpected, "The dependency is slow.");

        Assert.Equal(LifecycleState.Declared, lifecycle.State);
        Assert.Null(lifecycle.Error);

        lifecycle.Enter(LifecycleState.Degraded, degraded);
        Assert.Equal(LifecycleState.Degraded, lifecycle.Status.State);
        Assert.Same(degraded, lifecycle.Status.Error);
        Assert.Equal("xihan.test.lifecycle", lifecycle.Status.Manifest.Id);

        lifecycle.Enter(LifecycleState.Ready, null);
        Assert.Null(lifecycle.Error);

        var crashed = Error.Literal(PluginErrors.Crashed, "The process exited.");
        lifecycle.Fail(crashed);
        Assert.Equal(LifecycleState.Failed, lifecycle.State);
        Assert.Same(crashed, lifecycle.Error);
        Assert.Equal([LifecycleState.Degraded, LifecycleState.Ready, LifecycleState.Failed], carrier.Entered);

        var unavailable = lifecycle.Unavailable();
        Assert.Equal(PluginErrors.Unavailable, unavailable.Code);
        Assert.Equal(PluginMessages.ProcessUnavailable, unavailable.MessageKey);
        Assert.Equal("xihan.test.lifecycle", unavailable.Details?["id"]);
        Assert.Equal("Failed", unavailable.Details?["state"]);
    }

    /// <summary>
    /// 一个假载体上的生命周期
    /// </summary>
    private static PluginLifecycle Create(RecordingCarrier carrier, TimerSignalingClock clock, int limit, CancellationToken lifetime)
        => new(TestManifests.Create("xihan.test.lifecycle", kind: ManifestKind.Plugin), carrier, limit, _window, () => clock, lifetime);

    /// <summary>
    /// 走完一次崩溃后的重启，返回这次排的退避
    /// </summary>
    private static async Task<TimeSpan> RestartOnceAsync(PluginLifecycle lifecycle, RecordingCarrier carrier, TimerSignalingClock clock)
    {
        lifecycle.MarkRestartPending();
        var restarting = lifecycle.RestartAsync();
        var scheduled = await carrier.NextScheduledAsync();
        await clock.TimerCreatedAsync();
        clock.Advance(scheduled.Delay);
        await restarting.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None);
        return scheduled.Delay;
    }
}
