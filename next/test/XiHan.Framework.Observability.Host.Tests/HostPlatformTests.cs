// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net.NetworkInformation;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;

namespace XiHan.Framework.Observability.Host.Tests;

/// <summary>
/// 在真实机器上读。
/// </summary>
/// <remarks>
/// Windows 与 Linux 各一组用例，只在对应系统上运行（Linux 由 CI 运行）；断言只依赖任何机器都满足的性质，不依赖具体硬件。
/// </remarks>
public sealed partial class HostPlatformTests
{
    [Fact]
    [SupportedOSPlatform("windows")]
    public void Windows_ReadsRegistryAndKernel32()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");
        var platform = HostPlatform.Create();
        Assert.IsType<WindowsHostPlatform>(platform);

        var processor = platform.ReadProcessor();
        Assert.False(string.IsNullOrWhiteSpace(processor.Name));
        Assert.InRange(processor.PhysicalCores!.Value, 1, processor.LogicalCores);
        Assert.True(processor.FrequencyMhz > 0);
        Assert.True(processor.CacheBytes > 0);
        AssertCpuTimesAdvance(platform);
        AssertMemory(platform);

        var board = platform.ReadBoard();
        Assert.Null(board.SerialNumber);
        Assert.All(platform.ReadGpus(), AssertGpu);
        AssertCommon(platform);
    }

    [Fact]
    public void Linux_ReadsProcAndSys()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        var platform = HostPlatform.Create();
        Assert.IsType<LinuxHostPlatform>(platform);

        var processor = platform.ReadProcessor();
        Assert.False(string.IsNullOrWhiteSpace(processor.Name));
        Assert.True(processor.PhysicalCores is null or >= 1);
        AssertCpuTimesAdvance(platform);
        AssertMemory(platform);
        Assert.True(platform.ReadSystemUptime() > TimeSpan.Zero);
        Assert.All(platform.ReadGpus(), AssertGpu);
        Assert.All(platform.ReadDisks(), static disk => Assert.StartsWith("/dev/", disk.Device, StringComparison.Ordinal));
        AssertCommon(platform);
    }

    /// <summary>
    /// 两个平台都满足的性质：磁盘、网卡与运行时长。
    /// </summary>
    private static void AssertCommon(HostPlatform platform)
    {
        var disks = platform.ReadDisks();
        Assert.NotEmpty(disks);
        Assert.All(disks, static disk =>
        {
            Assert.True(disk.TotalBytes > 0);
            Assert.InRange(disk.FreeBytes, 0, disk.TotalBytes);
            Assert.InRange(disk.UsagePercent!.Value, 0d, 100d);
        });

        var nics = platform.ReadNetworkInterfaces();
        Assert.NotEmpty(nics);
        Assert.All(nics, static nic =>
        {
            Assert.False(string.IsNullOrEmpty(nic.Id));
            Assert.True(nic.SpeedBitsPerSecond is null or >= 0);
            Assert.True(nic.PhysicalAddress is null || MacPattern().IsMatch(nic.PhysicalAddress), nic.PhysicalAddress);
            Assert.DoesNotContain(string.Empty, nic.IPv4Addresses);
        });
        Assert.Contains(nics, static nic => nic.Type == NetworkInterfaceType.Loopback || nic.IPv4Addresses.Count > 0);
        Assert.Equal(nics.Select(static n => n.Id).Order(StringComparer.Ordinal), platform.ReadNetworkCounters().Select(static c => c.Id).Order(StringComparer.Ordinal));
        Assert.True(platform.ReadSystemUptime() > TimeSpan.Zero);
    }

    /// <summary>
    /// CPU 时间读得到，且总量不倒退。
    /// </summary>
    private static void AssertCpuTimesAdvance(HostPlatform platform)
    {
        var first = platform.ReadCpuTimes()!.Value;
        Assert.InRange(first.Idle, 0UL, first.Total);
        var second = platform.ReadCpuTimes()!.Value;
        Assert.True(second.Total >= first.Total);
        Assert.True(platform.ReadProcessCpuTime() > TimeSpan.Zero);
    }

    /// <summary>
    /// 物理内存读得到且自洽。
    /// </summary>
    private static void AssertMemory(HostPlatform platform)
    {
        var memory = platform.ReadMemory();
        Assert.True(memory.TotalBytes > 0);
        Assert.InRange(memory.AvailableBytes!.Value, 1, memory.TotalBytes!.Value);
        Assert.InRange(memory.UsagePercent!.Value, 0d, 100d);
    }

    /// <summary>
    /// 显卡的 ID 格式与非负显存。
    /// </summary>
    private static void AssertGpu(Abstractions.GpuInfo gpu)
    {
        Assert.True(gpu.VendorId is null || PciIdPattern().IsMatch(gpu.VendorId), gpu.VendorId);
        Assert.True(gpu.DeviceId is null || PciIdPattern().IsMatch(gpu.DeviceId), gpu.DeviceId);
        Assert.True(gpu.MemoryBytes is null or > 0);
        Assert.True(gpu.Name is null || gpu.Name.Length > 0);
    }

    /// <summary>
    /// 冒号分隔的大写十六进制物理地址。
    /// </summary>
    [GeneratedRegex("^[0-9A-F]{2}(:[0-9A-F]{2})+$")]
    private static partial Regex MacPattern();

    /// <summary>
    /// 四位小写十六进制。
    /// </summary>
    [GeneratedRegex("^[0-9a-f]{4}$")]
    private static partial Regex PciIdPattern();
}
