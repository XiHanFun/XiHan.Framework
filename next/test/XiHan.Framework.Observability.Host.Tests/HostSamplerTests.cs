// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace XiHan.Framework.Observability.Host.Tests;

/// <summary>
/// 采样器。
/// </summary>
/// <remarks>
/// 两次采样之差：第一次之后为空，系统与进程使用率、网卡速率的换算，计数变小为空，百分比限制在 0–100，后台循环按间隔采样。
/// </remarks>
public sealed class HostSamplerTests
{
    /// <summary>
    /// 固定起点的可控时钟。
    /// </summary>
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero));

    /// <summary>
    /// 计数替身。
    /// </summary>
    private readonly FakeHostPlatform _platform = new();

    [Fact]
    public void FirstSample_LeavesEverythingEmpty()
    {
        var sampler = Create();
        _platform.Cpu = new CpuTimes(100, 1000);
        _platform.Network = [new NetworkCounters("eth0", 1000, 2000)];
        sampler.Sample();

        Assert.Null(sampler.CpuUsagePercent);
        Assert.Null(sampler.ProcessCpuUsagePercent);
        Assert.Equal((null, null), sampler.NetworkRate("eth0"));
    }

    [Fact]
    public void SecondSample_ComputesUsageAndRates()
    {
        var sampler = Create();
        _platform.Cpu = new CpuTimes(100, 1000);
        _platform.ProcessCpu = TimeSpan.FromSeconds(1);
        _platform.Network = [new NetworkCounters("eth0", 1000, 2000), new NetworkCounters("lo", null, 50)];
        sampler.Sample();

        // 两秒内：总增量 400、空闲增量 100 → 75%；进程用了 2 秒处理器时间，4 核两秒共 8 秒 → 25%
        _clock.Advance(TimeSpan.FromSeconds(2));
        _platform.Cpu = new CpuTimes(200, 1400);
        _platform.ProcessCpu = TimeSpan.FromSeconds(3);
        _platform.Network = [new NetworkCounters("eth0", 5000, 2600), new NetworkCounters("lo", null, 90), new NetworkCounters("wlan0", 10, 10)];
        sampler.Sample();

        Assert.Equal(75d, sampler.CpuUsagePercent);
        Assert.Equal(25d, sampler.ProcessCpuUsagePercent);
        Assert.Equal((2000d, 300d), sampler.NetworkRate("eth0"));
        // 读不到的计数为空；上一次没有的网卡还没有速率
        Assert.Equal((null, 20d), sampler.NetworkRate("lo"));
        Assert.Equal((null, null), sampler.NetworkRate("wlan0"));
    }

    [Fact]
    public void CountersGoingBackwards_OrNoCpuProgress_AreEmpty()
    {
        var sampler = Create();
        _platform.Cpu = new CpuTimes(100, 1000);
        _platform.Network = [new NetworkCounters("eth0", 1000, 2000)];
        sampler.Sample();

        // 计数回绕或网卡重建：该项为空，不报负数
        _clock.Advance(TimeSpan.FromSeconds(1));
        _platform.Cpu = new CpuTimes(100, 1000);
        _platform.Network = [new NetworkCounters("eth0", 10, 2100)];
        sampler.Sample();
        Assert.Null(sampler.CpuUsagePercent);
        Assert.Equal((null, 100d), sampler.NetworkRate("eth0"));

        _clock.Advance(TimeSpan.FromSeconds(1));
        _platform.Cpu = null;
        sampler.Sample();
        Assert.Null(sampler.CpuUsagePercent);
    }

    [Fact]
    public void Percentages_AreClampedToHundred()
    {
        var sampler = Create();
        _platform.ProcessCpu = TimeSpan.Zero;
        sampler.Sample();

        // 进程时间增量超过经过时间 × 核数（计时误差）：限制在 100
        _clock.Advance(TimeSpan.FromSeconds(1));
        _platform.ProcessCpu = TimeSpan.FromSeconds(5);
        sampler.Sample();
        Assert.Equal(100d, sampler.ProcessCpuUsagePercent);
    }

    [Fact]
    public async Task Loop_SamplesOnEveryInterval_UntilStopped()
    {
        await using var sampler = Create();
        _platform.Cpu = new CpuTimes(0, 0);
        await sampler.StartAsync(CancellationToken.None);
        Assert.Null(sampler.CpuUsagePercent);

        _platform.Cpu = new CpuTimes(50, 100);
        _clock.Advance(TimeSpan.FromSeconds(5));
        await WaitUntilAsync(() => sampler.CpuUsagePercent is not null);
        Assert.Equal(50d, sampler.CpuUsagePercent);

        // 停止后不再采样，最近的值保留
        await sampler.StopAsync(CancellationToken.None);
        _platform.Cpu = new CpuTimes(50, 200);
        _clock.Advance(TimeSpan.FromSeconds(10));
        await Task.Delay(TimeSpan.FromMilliseconds(100));
        Assert.Equal(50d, sampler.CpuUsagePercent);
    }

    /// <summary>
    /// 4 核、5 秒间隔的采样器。
    /// </summary>
    private HostSampler Create() => new(_platform, TimeSpan.FromSeconds(5), _clock, NullLogger.Instance, logicalCores: 4);

    /// <summary>
    /// 等条件成立。
    /// </summary>
    /// <remarks>
    /// 五秒内不成立即失败。
    /// </remarks>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), timeout.Token);
        }
    }
}
