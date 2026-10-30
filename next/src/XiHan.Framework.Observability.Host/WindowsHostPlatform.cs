// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using XiHan.Framework.Observability.Abstractions;

namespace XiHan.Framework.Observability.Host;

/// <summary>
/// Windows 的读取路径。
/// </summary>
/// <remarks>
/// 读注册表与 kernel32 的 <c>GetSystemTimes</c>、<c>GetLogicalProcessorInformation</c>、<c>K32GetPerformanceInfo</c>，不用 WMI。
/// </remarks>
[SupportedOSPlatform("windows")]
internal sealed partial class WindowsHostPlatform : HostPlatform
{
    /// <summary>
    /// 显示适配器的设备类。
    /// </summary>
    private const string DisplayClass = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

    /// <summary>
    /// <c>RelationProcessorCore</c>。
    /// </summary>
    private const int RelationProcessorCore = 0;

    /// <summary>
    /// <c>RelationCache</c>。
    /// </summary>
    private const int RelationCache = 2;

    /// <summary>
    /// <c>CacheInstruction</c>。
    /// </summary>
    private const int CacheInstruction = 1;

    /// <inheritdoc />
    public override ProcessorInfo ReadProcessor()
    {
        using var key = Open(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
        var (cores, cache) = Topology();
        return base.ReadProcessor() with
        {
            Name = Text(key, "ProcessorNameString"),
            Vendor = Text(key, "VendorIdentifier"),
            PhysicalCores = cores,
            FrequencyMhz = Value(key, "~MHz") is int mhz && mhz > 0 ? mhz : null,
            CacheBytes = cache,
        };
    }

    /// <inheritdoc />
    /// <remarks>
    /// 内核时间含空闲时间，总量是内核加用户。
    /// </remarks>
    public override CpuTimes? ReadCpuTimes()
        => NativeMethods.GetSystemTimes(out var idle, out var kernel, out var user) ? new CpuTimes((ulong)idle, (ulong)kernel + (ulong)user) : null;

    /// <inheritdoc />
    /// <remarks>
    /// 没有空闲字节数（RFC-0023 的已知差异）。
    /// </remarks>
    public override MemoryInfo ReadMemory()
    {
        if (!NativeMethods.K32GetPerformanceInfo(out var information, (uint)Marshal.SizeOf<PerformanceInformation>()))
        {
            return new MemoryInfo();
        }

        var page = (long)information.PageSize;
        return new MemoryInfo
        {
            TotalBytes = (long)information.PhysicalTotal * page,
            AvailableBytes = (long)information.PhysicalAvailable * page,
            CachedBytes = (long)information.SystemCache * page,
        };
    }

    /// <inheritdoc />
    /// <remarks>
    /// 显示适配器类下 <c>MatchingDeviceId</c> 以 <c>pci\</c> 开头的子键。
    /// </remarks>
    public override IReadOnlyList<GpuInfo> ReadGpus()
    {
        using var display = Open(DisplayClass);
        if (display is null)
        {
            return [];
        }

        var gpus = new List<GpuInfo>();
        foreach (var name in SubKeyNames(display).Where(static n => n.Length == 4 && n.All(char.IsAsciiDigit)).Order(StringComparer.Ordinal))
        {
            using var adapter = OpenSubKey(display, name);
            if (Text(adapter, "MatchingDeviceId") is not { } matching || !matching.StartsWith(@"pci\", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            gpus.Add(new GpuInfo
            {
                Name = Text(adapter, "DriverDesc"),
                Vendor = Text(adapter, "ProviderName"),
                VendorId = PciPart(matching, "ven"),
                DeviceId = PciPart(matching, "dev"),
                DriverVersion = Text(adapter, "DriverVersion"),
                MemoryBytes = MemorySize(Value(adapter, "HardwareInformation.qwMemorySize")) ?? MemorySize(Value(adapter, "HardwareInformation.MemorySize")),
            });
        }

        return gpus;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <c>HKLM\HARDWARE\DESCRIPTION\System\BIOS</c>；没有序列号（RFC-0023 的已知差异）。
    /// </remarks>
    public override BoardInfo ReadBoard()
    {
        using var bios = Open(@"HARDWARE\DESCRIPTION\System\BIOS");
        return new BoardInfo
        {
            Manufacturer = Text(bios, "BaseBoardManufacturer"),
            Product = Text(bios, "BaseBoardProduct"),
            Version = Text(bios, "BaseBoardVersion"),
            SystemManufacturer = Text(bios, "SystemManufacturer"),
            SystemProduct = Text(bios, "SystemProductName"),
            BiosVendor = Text(bios, "BIOSVendor"),
            BiosVersion = Text(bios, "BIOSVersion"),
            BiosDate = Text(bios, "BIOSReleaseDate"),
        };
    }

    /// <summary>
    /// 物理核心数与最后一级缓存的字节数。
    /// </summary>
    /// <remarks>
    /// 解析 <c>SYSTEM_LOGICAL_PROCESSOR_INFORMATION</c> 数组：联合体在掩码与关系之后按 8 字节对齐。
    /// </remarks>
    private static (int? Cores, long? CacheBytes) Topology()
    {
        uint length = 0;
        _ = NativeMethods.GetLogicalProcessorInformation(0, ref length);
        if (length == 0)
        {
            return (null, null);
        }

        var buffer = Marshal.AllocHGlobal((int)length);
        try
        {
            if (!NativeMethods.GetLogicalProcessorInformation(buffer, ref length))
            {
                return (null, null);
            }

            var size = IntPtr.Size == 8 ? 32 : 24;
            var union = IntPtr.Size == 8 ? 16 : 8;
            var cores = 0;
            long? cache = null;
            var level = 0;
            for (var offset = 0; offset + size <= length; offset += size)
            {
                switch (Marshal.ReadInt32(buffer, offset + IntPtr.Size))
                {
                    case RelationProcessorCore:
                        cores++;
                        break;
                    case RelationCache:
                        var cacheLevel = Marshal.ReadByte(buffer, offset + union);
                        // CACHE_DESCRIPTOR：Level、Associativity、LineSize、Size、Type；跳过指令缓存
                        if (cacheLevel > level && Marshal.ReadInt32(buffer, offset + union + 8) != CacheInstruction)
                        {
                            level = cacheLevel;
                            cache = (uint)Marshal.ReadInt32(buffer, offset + union + 4);
                        }

                        break;
                }
            }

            return (cores > 0 ? cores : null, cache);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>
    /// 从 <c>pci\ven_10de&amp;dev_2504...</c> 取一段四位 ID。
    /// </summary>
    private static string? PciPart(string matching, string part)
        => PciPattern().Matches(matching).FirstOrDefault(m => string.Equals(m.Groups["part"].Value, part, StringComparison.OrdinalIgnoreCase)) is { } match ? match.Groups["id"].Value.ToLowerInvariant() : null;

    /// <summary>
    /// 显存：<c>REG_QWORD</c>、<c>REG_DWORD</c> 或小端的 <c>REG_BINARY</c>。
    /// </summary>
    private static long? MemorySize(object? value) => value switch
    {
        long bytes when bytes > 0 => bytes,
        int bytes when bytes != 0 => (uint)bytes,
        byte[] { Length: 8 } raw => BinaryPrimitives.ReadInt64LittleEndian(raw) is > 0 and var bytes ? bytes : null,
        byte[] { Length: 4 } raw => BinaryPrimitives.ReadUInt32LittleEndian(raw) is > 0 and var bytes ? bytes : null,
        _ => null,
    };

    /// <summary>
    /// 打开本机键，打不开为空。
    /// </summary>
    private static RegistryKey? Open(string path)
    {
        try
        {
            return Registry.LocalMachine.OpenSubKey(path);
        }
        catch (Exception exception) when (Readings.IsUnreadable(exception))
        {
            return null;
        }
    }

    /// <summary>
    /// 打开子键，打不开为空。
    /// </summary>
    private static RegistryKey? OpenSubKey(RegistryKey key, string name)
    {
        try
        {
            return key.OpenSubKey(name);
        }
        catch (Exception exception) when (Readings.IsUnreadable(exception))
        {
            return null;
        }
    }

    /// <summary>
    /// 子键名，读不到为空列表。
    /// </summary>
    private static string[] SubKeyNames(RegistryKey key)
    {
        try
        {
            return key.GetSubKeyNames();
        }
        catch (Exception exception) when (Readings.IsUnreadable(exception))
        {
            return [];
        }
    }

    /// <summary>
    /// 读一个值，读不到为空。
    /// </summary>
    private static object? Value(RegistryKey? key, string name)
    {
        try
        {
            return key?.GetValue(name);
        }
        catch (Exception exception) when (Readings.IsUnreadable(exception))
        {
            return null;
        }
    }

    /// <summary>
    /// 读一个字符串值，空白为空。
    /// </summary>
    private static string? Text(RegistryKey? key, string name) => Readings.Clean(Value(key, name) as string);

    /// <summary>
    /// <c>ven_xxxx</c> 与 <c>dev_xxxx</c>。
    /// </summary>
    [GeneratedRegex(@"(?<part>ven|dev)_(?<id>[0-9a-f]{4})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PciPattern();

    /// <summary>
    /// <c>PERFORMANCE_INFORMATION</c>。
    /// </summary>
    /// <remarks>
    /// 内存量以页为单位。
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    private struct PerformanceInformation
    {
        /// <summary>
        /// 结构大小。
        /// </summary>
        public uint Size;

        /// <summary>
        /// 已提交页数。
        /// </summary>
        public nuint CommitTotal;

        /// <summary>
        /// 提交上限。
        /// </summary>
        public nuint CommitLimit;

        /// <summary>
        /// 提交峰值。
        /// </summary>
        public nuint CommitPeak;

        /// <summary>
        /// 物理内存页数。
        /// </summary>
        public nuint PhysicalTotal;

        /// <summary>
        /// 可用物理内存页数。
        /// </summary>
        public nuint PhysicalAvailable;

        /// <summary>
        /// 系统缓存页数。
        /// </summary>
        public nuint SystemCache;

        /// <summary>
        /// 内核内存页数。
        /// </summary>
        public nuint KernelTotal;

        /// <summary>
        /// 分页池页数。
        /// </summary>
        public nuint KernelPaged;

        /// <summary>
        /// 非分页池页数。
        /// </summary>
        public nuint KernelNonpaged;

        /// <summary>
        /// 页大小。
        /// </summary>
        public nuint PageSize;

        /// <summary>
        /// 句柄数。
        /// </summary>
        public uint HandleCount;

        /// <summary>
        /// 进程数。
        /// </summary>
        public uint ProcessCount;

        /// <summary>
        /// 线程数。
        /// </summary>
        public uint ThreadCount;
    }

    /// <summary>
    /// kernel32 的函数。
    /// </summary>
    private static partial class NativeMethods
    {
        /// <summary>
        /// 系统的空闲、内核与用户时间。
        /// </summary>
        [LibraryImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GetSystemTimes(out long idleTime, out long kernelTime, out long userTime);

        /// <summary>
        /// 逻辑处理器的拓扑。
        /// </summary>
        [LibraryImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GetLogicalProcessorInformation(nint buffer, ref uint returnedLength);

        /// <summary>
        /// 系统的内存与对象计数。
        /// </summary>
        [LibraryImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool K32GetPerformanceInfo(out PerformanceInformation information, uint size);
    }
}
