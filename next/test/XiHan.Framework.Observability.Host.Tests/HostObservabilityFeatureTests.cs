// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Observability.Abstractions;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Observability.Host.Tests;

/// <summary>
/// 能力包装配与选项。
/// </summary>
/// <remarks>
/// 宿主里取到 <see cref="IHostMonitor"/>，运行信息自洽，采样一个间隔后有 CPU 使用率；选项解析与校验。
/// </remarks>
public sealed class HostObservabilityFeatureTests
{
    [Fact]
    public async Task Feature_ProvidesMonitor_AndSamplesInTheBackground()
    {
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<HostObservabilityFeature>(),
            [new("XiHan:Observability:Host:SampleIntervalSeconds", "0.1")]);

        Assert.Equal(["observability.host"], host.App.Graph["xihan.observability.host"].SelectedCapabilities);
        var monitor = host.Services.GetRequiredService<IHostMonitor>();

        var runtime = monitor.ReadRuntime();
        Assert.Equal(Environment.ProcessId, runtime.ProcessId);
        Assert.Equal(HostPlatform.PlatformName(), runtime.OsPlatform);
        Assert.False(string.IsNullOrWhiteSpace(runtime.Framework));
        Assert.True(runtime.WorkingSetBytes > 0);
        Assert.True(runtime.ProcessUptime >= TimeSpan.Zero);
        Assert.True(runtime.SystemUptime > runtime.ProcessUptime - TimeSpan.FromSeconds(1));
        Assert.Equal(Environment.ProcessorCount, monitor.ReadProcessor().LogicalCores);

        // 只有 Linux 与 Windows 读得到系统 CPU 时间
        if (OperatingSystem.IsLinux() || OperatingSystem.IsWindows())
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (monitor.ReadProcessor().UsagePercent is null)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(20), timeout.Token);
            }

            Assert.InRange(monitor.ReadProcessor().UsagePercent!.Value, 0d, 100d);
            Assert.InRange(monitor.ReadRuntime().ProcessCpuUsagePercent!.Value, 0d, 100d);
        }

        // 静态部分缓存：两次读到同一份
        Assert.Same(monitor.ReadGpus(), monitor.ReadGpus());
        Assert.Same(monitor.ReadBoard(), monitor.ReadBoard());
    }

    [Fact]
    public void Options_FromConfiguration_ParsesAndRejectsInvalid()
    {
        static IConfiguration Build(params (string Key, string Value)[] values)
            => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

        Assert.Equal(TimeSpan.FromSeconds(5), HostObservabilityOptions.FromConfiguration(Build()).SampleInterval);
        Assert.Equal(TimeSpan.FromSeconds(2.5), HostObservabilityOptions.FromConfiguration(Build(("XiHan:Observability:Host:SampleIntervalSeconds", "2.5"))).SampleInterval);
        Assert.Throws<ArgumentException>(static () => HostObservabilityOptions.FromConfiguration(Build(("XiHan:Observability:Host:SampleIntervalSeconds", "0"))));
        Assert.Throws<ArgumentException>(static () => HostObservabilityOptions.FromConfiguration(Build(("XiHan:Observability:Host:SampleIntervalSeconds", "3601"))));
        Assert.Throws<ArgumentException>(static () => HostObservabilityOptions.FromConfiguration(Build(("XiHan:Observability:Host:SampleIntervalSeconds", "soon"))));
        Assert.Throws<ArgumentOutOfRangeException>(static () => new HostObservabilityOptions { SampleInterval = TimeSpan.Zero });
    }
}
