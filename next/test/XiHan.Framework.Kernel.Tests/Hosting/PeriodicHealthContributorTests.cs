// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests.Hosting;

/// <summary>
/// <see cref="PeriodicHealthContributor"/>。
/// </summary>
/// <remarks>
/// 首探同步完成、失败结果 Degraded 并带原错误、再探成功回 Ready、异常与超时记为 <see cref="KernelErrors.HealthProbeFailed"/> 且不传播、按时钟重探、停止后不再探。
/// </remarks>
public sealed class PeriodicHealthContributorTests
{
    /// <summary>
    /// 条目名。
    /// </summary>
    private const string Name = "test.probe";

    /// <summary>
    /// 探针返回的失败结果。
    /// </summary>
    private static readonly Error _failure = Error.Unavailable("TEST_UNAVAILABLE_001", "test.store.unavailable");

    /// <summary>
    /// 以假时钟与空日志创建贡献者。
    /// </summary>
    /// <remarks>
    /// 间隔缺省 15 秒。
    /// </remarks>
    private static PeriodicHealthContributor Create(Func<CancellationToken, ValueTask<Result>> probe, FakeTimeProvider clock, TimeSpan? interval = null)
        => new(Name, probe, interval ?? TimeSpan.FromSeconds(15), clock, NullLoggerFactory.Instance);

    /// <summary>
    /// 等条件成立，最多等 5 秒真实时间。
    /// </summary>
    /// <remarks>
    /// 后台循环在线程池上跑，假时钟推进后要给它调度的机会。
    /// </remarks>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The condition did not become true in time.");
            await Task.Delay(10);
        }
    }

    [Fact]
    public void Constructor_RejectsNonPositiveInterval_AndBlankName()
    {
        var clock = new FakeTimeProvider();
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(static _ => ValueTask.FromResult(Result.Success()), clock, TimeSpan.Zero));
        Assert.Throws<ArgumentException>(() => new PeriodicHealthContributor(" ", static _ => ValueTask.FromResult(Result.Success()), TimeSpan.FromSeconds(1), clock, NullLoggerFactory.Instance));
    }

    [Fact]
    public async Task StartAsync_ProbesOnceBeforeReturning_AndReportsReady()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 27, 8, 0, 0, TimeSpan.Zero));
        var probes = 0;
        await using var contributor = Create(_ => { probes++; return ValueTask.FromResult(Result.Success()); }, clock);

        // 还没探过：Started，不影响就绪
        var initial = Assert.Single(contributor.Check());
        Assert.Equal((Name, LifecycleState.Started), (initial.Name, initial.State));
        Assert.Null(contributor.LastProbedAt);

        await contributor.StartAsync(CancellationToken.None);

        Assert.Equal(1, probes);
        var status = Assert.Single(contributor.Check());
        Assert.Equal(LifecycleState.Ready, status.State);
        Assert.Null(status.Error);
        Assert.Equal(clock.GetUtcNow(), contributor.LastProbedAt);
        // 重复启动不再探，也不起第二个循环
        await contributor.StartAsync(CancellationToken.None);
        Assert.Equal(1, probes);
    }

    [Fact]
    public async Task Probe_FailureResult_IsDegradedWithThatError_AndSuccessRecovers()
    {
        var clock = new FakeTimeProvider();
        var outcome = Result.Failure(_failure);
        await using var contributor = Create(_ => ValueTask.FromResult(outcome), clock);

        await contributor.StartAsync(CancellationToken.None);
        var degraded = Assert.Single(contributor.Check());
        Assert.Equal(LifecycleState.Degraded, degraded.State);
        Assert.Same(_failure, degraded.Error);

        outcome = Result.Success();
        await contributor.ProbeAsync(CancellationToken.None);
        var ready = Assert.Single(contributor.Check());
        Assert.Equal(LifecycleState.Ready, ready.State);
        Assert.Null(ready.Error);
    }

    [Fact]
    public async Task Probe_Exception_IsDegradedWithHealthProbeFailed_WithoutPropagating()
    {
        var clock = new FakeTimeProvider();
        await using var contributor = Create(static _ => throw new InvalidOperationException("boom"), clock);

        await contributor.StartAsync(CancellationToken.None);

        var status = Assert.Single(contributor.Check());
        Assert.Equal(LifecycleState.Degraded, status.State);
        Assert.Equal(KernelErrors.HealthProbeFailed, status.Error?.Code);
        Assert.Equal(KernelMessages.HealthProbeFailed, status.Error?.MessageKey);
        Assert.Equal(Name, status.Error?.Details?["name"]);
        Assert.Equal("boom", status.Error?.Details?["reason"]);
    }

    [Fact]
    public async Task Probe_Timeout_IsDegraded_WhenTheProbeOutlivesTheInterval()
    {
        var clock = new FakeTimeProvider();
        var interval = TimeSpan.FromSeconds(3);
        await using var contributor = Create(static async ct =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return Result.Success();
        }, clock, interval);

        // 首探卡住：推进假时钟越过间隔，超时取消探针，启动随即完成且条目 Degraded
        var starting = contributor.StartAsync(CancellationToken.None);
        clock.Advance(interval);
        await starting.WaitAsync(TimeSpan.FromSeconds(5));

        var status = Assert.Single(contributor.Check());
        Assert.Equal(LifecycleState.Degraded, status.State);
        Assert.Equal(KernelErrors.HealthProbeFailed, status.Error?.Code);
    }

    [Fact]
    public async Task Loop_ReprobesWhenTheClockAdvances_AndStopsAfterStopAsync()
    {
        var clock = new FakeTimeProvider();
        var interval = TimeSpan.FromSeconds(10);
        var probes = 0;
        await using var contributor = Create(_ => { Interlocked.Increment(ref probes); return ValueTask.FromResult(Result.Success()); }, clock, interval);

        await contributor.StartAsync(CancellationToken.None);
        Assert.Equal(1, Volatile.Read(ref probes));

        clock.Advance(interval);
        await WaitUntilAsync(() => Volatile.Read(ref probes) == 2);

        await contributor.StopAsync(CancellationToken.None);
        clock.Advance(interval);
        await Task.Delay(100);
        Assert.Equal(2, Volatile.Read(ref probes));
        // 停止后缓存的最近结果保留
        Assert.Equal(LifecycleState.Ready, Assert.Single(contributor.Check()).State);
    }

    [Fact]
    public async Task ProbeAsync_PropagatesCallerCancellation_WithoutChangingTheStatus()
    {
        var clock = new FakeTimeProvider();
        await using var contributor = Create(static async ct =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return Result.Success();
        }, clock);
        using var cancellation = new CancellationTokenSource();

        var probing = contributor.ProbeAsync(cancellation.Token).AsTask();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => probing);
        Assert.Equal(LifecycleState.Started, Assert.Single(contributor.Check()).State);
    }
}
