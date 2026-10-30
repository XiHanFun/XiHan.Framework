// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Observability.Host.Tests;

/// <summary>
/// Linux 读取路径。
/// </summary>
/// <remarks>
/// 根目录指向夹具，任何系统上都能跑：CPU 型号、核心、频率与缓存，CPU 时间，内存，运行时长，主板，显卡与 <c>pci.ids</c>，挂载表；读不到的项为空。
/// </remarks>
public sealed class LinuxHostPlatformTests : IDisposable
{
    /// <summary>
    /// 夹具根目录。
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xihan-host-" + Guid.NewGuid().ToString("N"));

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Processor_ReadsCpuinfoTopologyFrequencyAndCache()
    {
        Write("proc/cpuinfo", "processor\t: 0\nvendor_id\t: GenuineIntel\nmodel name\t: Intel(R) Xeon(R) Platinum 8370C CPU @ 2.80GHz\n\nprocessor\t: 1\nvendor_id\t: GenuineIntel\nmodel name\t: Intel(R) Xeon(R) Platinum 8370C CPU @ 2.80GHz\n");
        // 四个逻辑核心：两个物理核心各两个超线程
        foreach (var (cpu, core) in new[] { (0, 0), (1, 0), (2, 1), (3, 1) })
        {
            Write($"sys/devices/system/cpu/cpu{cpu}/topology/physical_package_id", "0\n");
            Write($"sys/devices/system/cpu/cpu{cpu}/topology/core_id", $"{core}\n");
        }

        Write("sys/devices/system/cpu/cpufreq/policy0/scaling_driver", "intel_pstate\n");
        Write("sys/devices/system/cpu/cpu0/cpufreq/cpuinfo_max_freq", "3500000\n");
        Write("sys/devices/system/cpu/cpu0/cache/index0/level", "1\n");
        Write("sys/devices/system/cpu/cpu0/cache/index0/type", "Data\n");
        Write("sys/devices/system/cpu/cpu0/cache/index0/size", "48K\n");
        Write("sys/devices/system/cpu/cpu0/cache/index1/level", "2\n");
        Write("sys/devices/system/cpu/cpu0/cache/index1/type", "Unified\n");
        Write("sys/devices/system/cpu/cpu0/cache/index1/size", "1280K\n");
        Write("sys/devices/system/cpu/cpu0/cache/index2/level", "3\n");
        Write("sys/devices/system/cpu/cpu0/cache/index2/type", "Unified\n");
        Write("sys/devices/system/cpu/cpu0/cache/index2/size", "48M\n");

        var processor = Platform().ReadProcessor();
        Assert.Equal("Intel(R) Xeon(R) Platinum 8370C CPU @ 2.80GHz", processor.Name);
        Assert.Equal("GenuineIntel", processor.Vendor);
        Assert.Equal(2, processor.PhysicalCores);
        // 没有 base_frequency 时取最大频率，kHz 换算 MHz
        Assert.Equal(3500d, processor.FrequencyMhz);
        Assert.Equal(48L * 1024 * 1024, processor.CacheBytes);
        Assert.Equal(Environment.ProcessorCount, processor.LogicalCores);
        Assert.Null(processor.UsagePercent);

        Write("sys/devices/system/cpu/cpu0/cpufreq/base_frequency", "2800000\n");
        Assert.Equal(2800d, Platform().ReadProcessor().FrequencyMhz);
    }

    [Fact]
    public void Processor_ArmCpuinfo_UsesHardware_AndMissingFilesAreNull()
    {
        Write("proc/cpuinfo", "processor\t: 0\nBogoMIPS\t: 108.00\nCPU implementer\t: 0x41\n\nHardware\t: BCM2835\n");

        var processor = Platform().ReadProcessor();
        Assert.Equal("BCM2835", processor.Name);
        Assert.Null(processor.Vendor);
        Assert.Null(processor.PhysicalCores);
        Assert.Null(processor.FrequencyMhz);
        Assert.Null(processor.CacheBytes);
    }

    [Fact]
    public void CpuTimes_ReadProcStat_IdleIncludesIowait()
    {
        Write("proc/stat", "cpu  100 20 30 400 50 6 7 8 9 10\ncpu0 50 10 15 200 25 3 3 4 4 5\nintr 1 2 3\n");

        var times = Platform().ReadCpuTimes();
        Assert.Equal(new CpuTimes(450, 621), times);

        Write("proc/stat", "intr 1 2 3\n");
        Assert.Null(Platform().ReadCpuTimes());
    }

    [Fact]
    public void Memory_ReadsMeminfo_AndComputesUsage()
    {
        Write("proc/meminfo", "MemTotal:       16000000 kB\nMemFree:         2000000 kB\nMemAvailable:    6000000 kB\nBuffers:          500000 kB\nCached:          3000000 kB\nSwapTotal:             0 kB\n");

        var memory = Platform().ReadMemory();
        Assert.Equal(16000000L * 1024, memory.TotalBytes);
        Assert.Equal(6000000L * 1024, memory.AvailableBytes);
        Assert.Equal(2000000L * 1024, memory.FreeBytes);
        Assert.Equal(3500000L * 1024, memory.CachedBytes);
        Assert.Equal(10000000L * 1024, memory.UsedBytes);
        Assert.Equal(62.5d, memory.UsagePercent);

        // 没有文件：全部为空，不以 0 顶替
        File.Delete(Path.Combine(_root, "proc/meminfo"));
        var missing = Platform().ReadMemory();
        Assert.Equal((null, null, null, null, null, null), (missing.TotalBytes, missing.AvailableBytes, missing.FreeBytes, missing.CachedBytes, missing.UsedBytes, missing.UsagePercent));
    }

    [Fact]
    public void Uptime_ReadsProcUptime()
    {
        Write("proc/uptime", "12345.67 98765.43\n");
        Assert.Equal(TimeSpan.FromSeconds(12345.67), Platform().ReadSystemUptime());

        File.Delete(Path.Combine(_root, "proc/uptime"));
        Assert.Null(Platform().ReadSystemUptime());
    }

    [Fact]
    public void Board_ReadsDmi_AndUnreadableSerialIsNull()
    {
        Write("sys/class/dmi/id/board_vendor", "ASUSTeK COMPUTER INC.\n");
        Write("sys/class/dmi/id/board_name", "PRIME B560M-A\n");
        Write("sys/class/dmi/id/board_version", "Rev 1.xx\n");
        Write("sys/class/dmi/id/sys_vendor", "System manufacturer\n");
        Write("sys/class/dmi/id/product_name", "  \n");
        Write("sys/class/dmi/id/bios_vendor", "American Megatrends Inc.\n");
        Write("sys/class/dmi/id/bios_version", "1601\n");
        Write("sys/class/dmi/id/bios_date", "08/13/2024\n");

        var board = Platform().ReadBoard();
        Assert.Equal(("ASUSTeK COMPUTER INC.", "PRIME B560M-A", "Rev 1.xx"), (board.Manufacturer, board.Product, board.Version));
        // board_serial 不存在（非 root 时读不到），空白的型号也当作读不到
        Assert.Null(board.SerialNumber);
        Assert.Equal("System manufacturer", board.SystemManufacturer);
        Assert.Null(board.SystemProduct);
        Assert.Equal(("American Megatrends Inc.", "1601", "08/13/2024"), (board.BiosVendor, board.BiosVersion, board.BiosDate));
    }

    [Fact]
    public void Gpus_ReadDrmCards_AndNamesFromPciIds()
    {
        Write("sys/class/drm/card0/device/uevent", "DRIVER=amdgpu\nPCI_CLASS=30000\nPCI_ID=1002:73BF\nPCI_SLOT_NAME=0000:0b:00.0\n");
        Write("sys/class/drm/card0/device/mem_info_vram_total", "17163091968\n");
        Write("sys/class/drm/card0/device/hwmon/hwmon3/temp1_input", "45000\n");
        Write("sys/class/drm/card1/device/vendor", "0x8086\n");
        Write("sys/class/drm/card1/device/device", "0x3E92\n");
        Write("sys/class/drm/card1/device/uevent", "DRIVER=i915\n");
        Write("sys/module/i915/version", "\n");
        // 连接器与渲染节点不是显卡
        Write("sys/class/drm/card0-DP-1/status", "connected\n");
        Write("sys/class/drm/renderD128/dev", "226:128\n");
        Write("usr/share/hwdata/pci.ids", "# comment\n1002  Advanced Micro Devices, Inc. [AMD/ATI]\n\t73bf  Navi 21 [Radeon RX 6800/6800 XT / 6900 XT]\n\t\t1002 0e3a  Radeon RX 6900 XT\n8086  Intel Corporation\n\t3e91  CoffeeLake-S GT2 [UHD Graphics 630]\n\t3e92  CoffeeLake-S GT2 [UHD Graphics 630]\nC 00  Unclassified device\n");

        var gpus = Platform().ReadGpus();
        Assert.Equal(2, gpus.Count);
        var amd = gpus[0];
        Assert.Equal(("Navi 21 [Radeon RX 6800/6800 XT / 6900 XT]", "Advanced Micro Devices, Inc. [AMD/ATI]"), (amd.Name, amd.Vendor));
        Assert.Equal(("1002", "73bf", "amdgpu"), (amd.VendorId, amd.DeviceId, amd.Driver));
        Assert.Equal(17163091968L, amd.MemoryBytes);
        Assert.Equal(45d, amd.TemperatureCelsius);
        Assert.Null(amd.DriverVersion);

        var intel = gpus[1];
        Assert.Equal(("CoffeeLake-S GT2 [UHD Graphics 630]", "Intel Corporation"), (intel.Name, intel.Vendor));
        Assert.Equal(("8086", "3e92", "i915"), (intel.VendorId, intel.DeviceId, intel.Driver));
        // 驱动版本文件为空、没有显存与温度：都为空
        Assert.Equal((null, null, null), (intel.DriverVersion, intel.MemoryBytes, intel.TemperatureCelsius));
    }

    [Fact]
    public void Gpus_WithoutPciIds_KeepIds_AndNamesAreNull()
    {
        Write("sys/class/drm/card0/device/uevent", "DRIVER=nvidia\nPCI_ID=10DE:2504\n");
        Write("sys/module/nvidia/version", "550.120\n");

        var gpu = Assert.Single(Platform().ReadGpus());
        Assert.Equal((null, null), (gpu.Name, gpu.Vendor));
        Assert.Equal(("10de", "2504", "nvidia", "550.120"), (gpu.VendorId, gpu.DeviceId, gpu.Driver, gpu.DriverVersion));
        Assert.Empty(new LinuxHostPlatform(Path.Combine(_root, "missing")).ReadGpus());
    }

    [Fact]
    public void Mounts_KeepFirstMountOfEachBlockDevice()
    {
        var mounts = LinuxHostPlatform.ParseMounts(
            "sysfs /sys sysfs rw,nosuid 0 0\n"
            + "/dev/sda2 / ext4 rw,relatime 0 0\n"
            + "tmpfs /run tmpfs rw 0 0\n"
            + "/dev/sda1 /boot/efi vfat rw 0 0\n"
            + "/dev/sda2 /var/lib/docker ext4 rw 0 0\n"
            + "/dev/loop3 /snap/core/1 squashfs ro 0 0\n"
            + "/dev/sdb1 /mnt/my\\040data xfs rw 0 0\n");

        Assert.Equal(7, mounts.Count);
        Assert.Equal(
            [new MountEntry("/dev/sda2", "/", "ext4"), new MountEntry("/dev/sda1", "/boot/efi", "vfat"), new MountEntry("/dev/sdb1", "/mnt/my data", "xfs")],
            LinuxHostPlatform.BlockDevices(mounts));
        Assert.Empty(LinuxHostPlatform.ParseMounts(null));
    }

    /// <summary>
    /// 指向夹具的读取路径。
    /// </summary>
    private LinuxHostPlatform Platform() => new(_root);

    /// <summary>
    /// 在夹具里写一个文件。
    /// </summary>
    private void Write(string relative, string content)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
