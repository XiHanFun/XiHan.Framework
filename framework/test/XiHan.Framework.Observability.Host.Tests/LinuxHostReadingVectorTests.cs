// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using System.Text.Json;
using XiHan.Framework.Testing.Workspace;

namespace XiHan.Framework.Observability.Host.Tests;

/// <summary>
/// 回放 <c>host-usage.json</c> 的 <c>linux</c> 一节：Linux 上各项读数的取法
/// </summary>
/// <remarks>
/// 各项由 <c>/proc</c> 与 <c>/sys</c> 里的文本读出。规范 RFC-0023「平台读取路径」的「Linux 的取法」（ADR-0286）：<c>/proc/stat</c> 的空闲含 <c>iowait</c>、总计取前八列，<c>/proc/meminfo</c> 的数值乘以 1024、缓存是 <c>Buffers</c> 加 <c>Cached</c>，<c>/proc/uptime</c> 的第一列按十进制小数读，挂载表只取块设备、每个设备取第一行，处理器与显卡各由一组文件读出；读不到的项为空。每条用例把向量给的文本写进一个临时的根目录，交给 Linux 的读取路径去读，任何系统上都能跑。向量是规范的一部分，这里只回放，不另存一份
/// </remarks>
public sealed class LinuxHostReadingVectorTests
{
    /// <summary>
    /// 向量文件里 <c>linux</c> 一节的内容
    /// </summary>
    private static readonly LinuxVectors _vectors = SpecVectors.Read<HostUsageDocument>("observability", "host-usage.json").Linux;

    [Fact]
    public void ReadCpuTimes_GivesTheIdleAndTotalOfEveryCase()
    {
        Assert.NotEmpty(_vectors.CpuTimes);
        var failures = new List<string>();
        foreach (var vector in _vectors.CpuTimes)
        {
            var actual = Read([new("proc/stat", vector.Stat)], static platform => platform.ReadCpuTimes());
            var expected = vector.Times is { } times ? new CpuTimes(times.Idle, times.Total) : (CpuTimes?)null;
            if (actual != expected)
            {
                failures.Add($"{vector.Name}: read {Describe(actual)}, the vector has {Describe(expected)}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void ReadMemory_GivesTheBytesOfEveryCase()
    {
        Assert.NotEmpty(_vectors.Memory);
        var failures = new List<string>();
        foreach (var vector in _vectors.Memory)
        {
            var memory = Read([new("proc/meminfo", vector.Meminfo)], static platform => platform.ReadMemory());
            var actual = new MemoryReading(memory.TotalBytes, memory.AvailableBytes, memory.FreeBytes, memory.CachedBytes);
            if (actual != vector.Memory)
            {
                failures.Add($"{vector.Name}: read {actual}, the vector has {vector.Memory}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void ReadSystemUptime_GivesTheDurationOfEveryCase()
    {
        Assert.NotEmpty(_vectors.Uptime);
        var failures = new List<string>();
        foreach (var vector in _vectors.Uptime)
        {
            var actual = Read([new("proc/uptime", vector.Uptime)], static platform => platform.ReadSystemUptime());
            var expected = vector.Milliseconds is { } milliseconds ? TimeSpan.FromMilliseconds(milliseconds) : (TimeSpan?)null;
            if (actual != expected)
            {
                failures.Add($"{vector.Name}: read {(actual is null ? "nothing" : actual.Value.ToString("c"))}, the vector has {(expected is null ? "nothing" : expected.Value.ToString("c"))}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void BlockDevices_GivesTheMountsOfEveryCase()
    {
        Assert.NotEmpty(_vectors.Mounts);
        var failures = new List<string>();
        foreach (var vector in _vectors.Mounts)
        {
            var actual = LinuxHostPlatform.BlockDevices(LinuxHostPlatform.ParseMounts(vector.Mounts)).Select(static mount => new MountReading(mount.Device, mount.MountPoint, mount.FileSystem)).ToList();
            if (!actual.SequenceEqual(vector.Disks))
            {
                failures.Add($"{vector.Name}: read [{string.Join(", ", actual)}], the vector has [{string.Join(", ", vector.Disks.AsEnumerable())}]");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void ReadProcessor_GivesTheStaticInformationOfEveryCase()
    {
        Assert.NotEmpty(_vectors.Processors);
        var failures = new List<string>();
        foreach (var vector in _vectors.Processors)
        {
            var processor = Read(vector.Files, static platform => platform.ReadProcessor());
            var actual = new ProcessorReading(processor.Name, processor.Vendor, processor.PhysicalCores, processor.FrequencyMhz, processor.CacheBytes);
            if (actual != vector.Processor)
            {
                failures.Add($"{vector.Name}: read {actual}, the vector has {vector.Processor}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void ReadGpus_GivesTheAdaptersOfEveryCase()
    {
        Assert.NotEmpty(_vectors.Gpus);
        var failures = new List<string>();
        foreach (var vector in _vectors.Gpus)
        {
            var actual = Read(vector.Files, static platform => platform.ReadGpus())
                .Select(static gpu => new GpuReading(gpu.Name, gpu.Vendor, gpu.VendorId, gpu.DeviceId, gpu.Driver, gpu.DriverVersion, gpu.MemoryBytes, gpu.TemperatureCelsius))
                .ToList();
            if (!actual.SequenceEqual(vector.Gpus))
            {
                failures.Add($"{vector.Name}: read [{string.Join(", ", actual)}], the vector has [{string.Join(", ", vector.Gpus.AsEnumerable())}]");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void TheVectors_CoverTheColumnsTheFallbacksAndTheUnreadable()
    {
        // 防止向量换了写法后某一类一条都没核对还照样通过。CPU 时间：带 guest 两列的十列、只有四列的、iowait 不为零的（空闲比第四列大）、读不出的
        Assert.Contains(_vectors.CpuTimes, static vector => vector.Times is not null && Columns(vector.Stat) == 10);
        Assert.Contains(_vectors.CpuTimes, static vector => vector.Times is not null && Columns(vector.Stat) == 4);
        Assert.Contains(_vectors.CpuTimes, static vector => vector.Times is { } times && times.Idle > ulong.Parse(CpuLine(vector.Stat).Split(' ', StringSplitOptions.RemoveEmptyEntries)[4], System.Globalization.CultureInfo.InvariantCulture));
        Assert.Contains(_vectors.CpuTimes, static vector => vector.Times is null && Columns(vector.Stat) == 3);
        Assert.Contains(_vectors.CpuTimes, static vector => vector.Times is null && vector.Stat.Length == 0);

        // 内存：四项都有的、没有可用的、缓存只来自一项的、缓存为空的
        Assert.Contains(_vectors.Memory, static vector => vector.Memory is { TotalBytes: not null, AvailableBytes: not null, FreeBytes: not null, CachedBytes: not null });
        Assert.Contains(_vectors.Memory, static vector => vector.Memory is { TotalBytes: not null, AvailableBytes: null });
        Assert.Contains(_vectors.Memory, static vector => vector.Memory.CachedBytes is not null && !vector.Meminfo.Contains("Buffers", StringComparison.Ordinal));
        Assert.Contains(_vectors.Memory, static vector => vector.Memory is { TotalBytes: not null, CachedBytes: null });

        // 运行时长：两位小数的、带负号与指数而读不出的
        Assert.Contains(_vectors.Uptime, static vector => vector.Milliseconds is { } milliseconds && milliseconds % 1000 != 0);
        Assert.Contains(_vectors.Uptime, static vector => vector.Milliseconds is null && vector.Uptime.StartsWith('-'));
        Assert.Contains(_vectors.Uptime, static vector => vector.Milliseconds is null && vector.Uptime.Contains('e'));

        // 挂载表：同一个设备挂两处只取第一处、回环设备不取、转义还原出空格
        Assert.Contains(_vectors.Mounts, static vector => vector.Mounts.Contains("/dev/loop", StringComparison.Ordinal) && vector.Disks.All(static disk => !disk.Device.StartsWith("/dev/loop", StringComparison.Ordinal)));
        Assert.Contains(_vectors.Mounts, static vector => vector.Disks.Length > 0 && vector.Disks.Select(static disk => disk.Device).Distinct(StringComparer.Ordinal).Count() == vector.Disks.Length && vector.Mounts.Split('\n').Count(line => line.StartsWith(vector.Disks[0].Device + " ", StringComparison.Ordinal)) > 1);
        Assert.Contains(_vectors.Mounts, static vector => vector.Mounts.Contains("\\040", StringComparison.Ordinal) && vector.Disks.Any(static disk => disk.MountPoint.Contains(' ')));
        Assert.Contains(_vectors.Mounts, static vector => vector.Disks.Length == 0);

        // 处理器：型号取 Hardware 的、频率不是整数的、什么都读不到的
        Assert.Contains(_vectors.Processors, static vector => vector.Processor.Name is { } name && vector.Files.TryGetValue("proc/cpuinfo", out var cpuinfo) && cpuinfo.Contains("Hardware", StringComparison.Ordinal) && cpuinfo.Contains(name, StringComparison.Ordinal) && !cpuinfo.Contains("model name\t: " + name, StringComparison.Ordinal));
        Assert.Contains(_vectors.Processors, static vector => vector.Processor.FrequencyMhz is { } megahertz && megahertz != Math.Round(megahertz));
        Assert.Contains(_vectors.Processors, static vector => vector.Processor is { Name: null, Vendor: null, PhysicalCores: null, FrequencyMhz: null, CacheBytes: null });

        // 显卡：一块以上的、名称查不到的、温度不是整数的、一块都没有的
        Assert.Contains(_vectors.Gpus, static vector => vector.Gpus.Length > 1);
        Assert.Contains(_vectors.Gpus, static vector => vector.Gpus.Any(static gpu => gpu is { Name: null, VendorId: not null }));
        Assert.Contains(_vectors.Gpus, static vector => vector.Gpus.Any(static gpu => gpu.TemperatureCelsius is { } celsius && celsius != Math.Round(celsius)));
        Assert.Contains(_vectors.Gpus, static vector => vector.Gpus.Length == 0);
    }

    /// <summary>
    /// 把一组文件写进临时的根目录，交给 Linux 的读取路径去读
    /// </summary>
    /// <remarks>
    /// 读完连目录一起删掉；文件按 UTF-8、不带 BOM 写，换行照向量给的原样
    /// </remarks>
    /// <param name="files">路径（相对文件系统的根）到文件内容</param>
    /// <param name="read">在这个根上的一次读取</param>
    private static T Read<T>(IEnumerable<KeyValuePair<string, string>> files, Func<LinuxHostPlatform, T> read)
    {
        var root = Path.Combine(Path.GetTempPath(), "xihan-host-vectors-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            foreach (var (relative, content) in files)
            {
                var path = Path.Combine(root, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }

            return read(new LinuxHostPlatform(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// <c>/proc/stat</c> 里总计的那一行
    /// </summary>
    /// <remarks>
    /// 没有时为空串
    /// </remarks>
    private static string CpuLine(string stat)
        => stat.Split('\n').FirstOrDefault(static line => line.StartsWith("cpu ", StringComparison.Ordinal)) ?? string.Empty;

    /// <summary>
    /// 总计的那一行在名字之后有几列
    /// </summary>
    private static int Columns(string stat)
        => Math.Max(CpuLine(stat).Split(' ', StringSplitOptions.RemoveEmptyEntries).Length - 1, 0);

    /// <summary>
    /// 失败信息里的 CPU 累计时间
    /// </summary>
    private static string Describe(CpuTimes? times)
        => times is { } value ? $"idle {value.Idle} of {value.Total}" : "nothing";

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <remarks>
    /// 这个类只回放 <c>linux</c> 一节，其余各节由 <see cref="HostUsageVectorTests"/> 按各自的模型回放，这里原样留着
    /// </remarks>
    /// <param name="SampleInterval">采样间隔的缺省值与取值范围</param>
    /// <param name="Cpu">系统 CPU 使用率的用例</param>
    /// <param name="Process">进程 CPU 使用率的用例</param>
    /// <param name="Network">网卡速率的用例</param>
    /// <param name="Memory">内存的已用与使用率的用例</param>
    /// <param name="Disks">磁盘的已用与使用率的用例</param>
    /// <param name="Linux">Linux 上各项的取法</param>
    private sealed record HostUsageDocument(JsonElement SampleInterval, JsonElement Cpu, JsonElement Process, JsonElement Network, JsonElement Memory, JsonElement Disks, LinuxVectors Linux);

    /// <summary>
    /// <c>linux</c> 一节的对象模型
    /// </summary>
    /// <param name="CpuTimes">由 <c>/proc/stat</c> 读 CPU 累计时间的用例</param>
    /// <param name="Memory">由 <c>/proc/meminfo</c> 读内存的用例</param>
    /// <param name="Uptime">由 <c>/proc/uptime</c> 读运行时长的用例</param>
    /// <param name="Mounts">由 <c>/proc/self/mounts</c> 取块设备挂载点的用例</param>
    /// <param name="Processors">读处理器静态信息的用例</param>
    /// <param name="Gpus">读显卡的用例</param>
    private sealed record LinuxVectors(CpuTimesCase[] CpuTimes, MemoryCase[] Memory, UptimeCase[] Uptime, MountsCase[] Mounts, ProcessorCase[] Processors, GpuCase[] Gpus);

    /// <summary>
    /// 一个 CPU 累计时间的用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Stat"><c>/proc/stat</c> 的内容</param>
    /// <param name="Times">读出的累计时间；<see langword="null"/> 表示读不出</param>
    private sealed record CpuTimesCase(string Name, string Stat, CpuReading? Times);

    /// <summary>
    /// 读出的 CPU 累计时间
    /// </summary>
    /// <param name="Idle">空闲</param>
    /// <param name="Total">总计</param>
    private sealed record CpuReading(ulong Idle, ulong Total);

    /// <summary>
    /// 一个内存的用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Meminfo"><c>/proc/meminfo</c> 的内容</param>
    /// <param name="Memory">读出的内存</param>
    private sealed record MemoryCase(string Name, string Meminfo, MemoryReading Memory);

    /// <summary>
    /// 读出的内存（字节）
    /// </summary>
    /// <param name="TotalBytes">总量</param>
    /// <param name="AvailableBytes">可用</param>
    /// <param name="FreeBytes">空闲</param>
    /// <param name="CachedBytes">缓存</param>
    private sealed record MemoryReading(long? TotalBytes, long? AvailableBytes, long? FreeBytes, long? CachedBytes);

    /// <summary>
    /// 一个运行时长的用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Uptime"><c>/proc/uptime</c> 的内容</param>
    /// <param name="Milliseconds">读出的时长（毫秒）；<see langword="null"/> 表示读不出</param>
    private sealed record UptimeCase(string Name, string Uptime, long? Milliseconds);

    /// <summary>
    /// 一个挂载表的用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Mounts"><c>/proc/self/mounts</c> 的内容</param>
    /// <param name="Disks">要读容量的挂载点，按文件里的次序</param>
    private sealed record MountsCase(string Name, string Mounts, MountReading[] Disks);

    /// <summary>
    /// 一个要读容量的挂载点
    /// </summary>
    /// <param name="Device">设备</param>
    /// <param name="MountPoint">挂载点</param>
    /// <param name="FileSystem">文件系统</param>
    private sealed record MountReading(string Device, string MountPoint, string FileSystem);

    /// <summary>
    /// 一个处理器的用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Files">路径（相对文件系统的根）到文件内容</param>
    /// <param name="Processor">读出的静态信息</param>
    private sealed record ProcessorCase(string Name, Dictionary<string, string> Files, ProcessorReading Processor);

    /// <summary>
    /// 读出的处理器静态信息
    /// </summary>
    /// <param name="Name">型号</param>
    /// <param name="Vendor">厂商</param>
    /// <param name="PhysicalCores">物理核心数</param>
    /// <param name="FrequencyMhz">标称频率（MHz）</param>
    /// <param name="CacheBytes">最后一级缓存的字节数</param>
    private sealed record ProcessorReading(string? Name, string? Vendor, int? PhysicalCores, double? FrequencyMhz, long? CacheBytes);

    /// <summary>
    /// 一个显卡的用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Files">路径（相对文件系统的根）到文件内容</param>
    /// <param name="Gpus">读出的各块显卡，按 <c>card&lt;N&gt;</c> 目录名的字节序</param>
    private sealed record GpuCase(string Name, Dictionary<string, string> Files, GpuReading[] Gpus);

    /// <summary>
    /// 读出的一块显卡
    /// </summary>
    /// <param name="Name">名称</param>
    /// <param name="Vendor">厂商名</param>
    /// <param name="VendorId">PCI 厂商 ID</param>
    /// <param name="DeviceId">PCI 设备 ID</param>
    /// <param name="Driver">驱动</param>
    /// <param name="DriverVersion">驱动版本</param>
    /// <param name="MemoryBytes">显存字节数</param>
    /// <param name="TemperatureCelsius">温度（摄氏度）</param>
    private sealed record GpuReading(string? Name, string? Vendor, string? VendorId, string? DeviceId, string? Driver, string? DriverVersion, long? MemoryBytes, double? TemperatureCelsius);
}
