// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using XiHan.Framework.Observability.Abstractions;

namespace XiHan.Framework.Observability.Host;

/// <summary>
/// 一个平台的读取路径。
/// </summary>
/// <remarks>
/// 本类只用 BCL：处理器只有架构与逻辑核心，没有 CPU 时间、内存、GPU 与主板；磁盘按 <see cref="DriveInfo"/>，网卡按 <see cref="NetworkInterface"/>。Linux 与 Windows 的子类覆盖各自读得到的部分（RFC-0023 的平台读取路径）。
/// </remarks>
internal class HostPlatform
{
    /// <summary>
    /// 按当前操作系统选择读取路径。
    /// </summary>
    public static HostPlatform Create()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsHostPlatform();
        }

        return OperatingSystem.IsLinux() ? new LinuxHostPlatform("/") : new HostPlatform();
    }

    /// <summary>
    /// 平台名。
    /// </summary>
    public static string PlatformName()
    {
        if (OperatingSystem.IsWindows())
        {
            return "windows";
        }

        if (OperatingSystem.IsLinux())
        {
            return "linux";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "macos";
        }

        return OperatingSystem.IsFreeBSD() ? "freebsd" : "other";
    }

    /// <summary>
    /// 处理器的静态信息。
    /// </summary>
    public virtual ProcessorInfo ReadProcessor() => new()
    {
        Architecture = RuntimeInformation.OSArchitecture,
        LogicalCores = Environment.ProcessorCount,
    };

    /// <summary>
    /// 系统 CPU 的累计时间。
    /// </summary>
    public virtual CpuTimes? ReadCpuTimes() => null;

    /// <summary>
    /// 本进程的累计处理器时间。
    /// </summary>
    public virtual TimeSpan ReadProcessCpuTime() => Environment.CpuUsage.TotalTime;

    /// <summary>
    /// 物理内存。
    /// </summary>
    public virtual MemoryInfo ReadMemory() => new();

    /// <summary>
    /// 系统运行时长。
    /// </summary>
    public virtual TimeSpan? ReadSystemUptime() => TimeSpan.FromMilliseconds(Environment.TickCount64);

    /// <summary>
    /// 磁盘。
    /// </summary>
    /// <remarks>
    /// 就绪的固定、可移动与网络盘。
    /// </remarks>
    public virtual IReadOnlyList<DiskInfo> ReadDisks()
    {
        DriveInfo[] drives;
        try
        {
            drives = DriveInfo.GetDrives();
        }
        catch (Exception exception) when (Readings.IsUnreadable(exception))
        {
            return [];
        }

        var disks = new List<DiskInfo>();
        foreach (var drive in drives)
        {
            if (drive.DriveType is DriveType.Fixed or DriveType.Removable or DriveType.Network && Describe(drive, device: null, format: null) is { } disk)
            {
                disks.Add(disk);
            }
        }

        return disks;
    }

    /// <summary>
    /// 显卡。
    /// </summary>
    public virtual IReadOnlyList<GpuInfo> ReadGpus() => [];

    /// <summary>
    /// 主板、整机与 BIOS。
    /// </summary>
    public virtual BoardInfo ReadBoard() => new();

    /// <summary>
    /// 网卡、地址与累计流量。
    /// </summary>
    /// <remarks>
    /// 不含速率，速率由采样器补上。
    /// </remarks>
    public virtual IReadOnlyList<NetworkInterfaceInfo> ReadNetworkInterfaces()
        => [.. Interfaces().Select(static nic => Describe(nic)).OfType<NetworkInterfaceInfo>()];

    /// <summary>
    /// 每块网卡的累计收发字节数。
    /// </summary>
    public virtual IReadOnlyList<NetworkCounters> ReadNetworkCounters()
        => [.. Interfaces().Select(static nic => Statistics(nic) is { } statistics
            ? new NetworkCounters(nic.Id, Readings.Optional(() => statistics.BytesReceived), Readings.Optional(() => statistics.BytesSent))
            : new NetworkCounters(nic.Id, null, null))];

    /// <summary>
    /// 一个挂载点的容量。
    /// </summary>
    /// <remarks>
    /// 未就绪或读不到容量为 <see langword="null"/>。
    /// </remarks>
    protected static DiskInfo? Describe(DriveInfo drive, string? device, string? format)
    {
        ArgumentNullException.ThrowIfNull(drive);
        try
        {
            if (!drive.IsReady || drive.TotalSize <= 0)
            {
                return null;
            }

            return new DiskInfo
            {
                Name = drive.Name,
                Device = device,
                // Unix 上卷标就是挂载点本身，不是真正的卷标
                Label = OperatingSystem.IsWindows() ? Readings.Text(() => drive.VolumeLabel) : null,
                Format = format ?? Readings.Text(() => drive.DriveFormat),
                Type = drive.DriveType,
                TotalBytes = drive.TotalSize,
                FreeBytes = drive.TotalFreeSpace,
                AvailableBytes = drive.AvailableFreeSpace,
            };
        }
        catch (Exception exception) when (Readings.IsUnreadable(exception))
        {
            return null;
        }
    }

    /// <summary>
    /// 全部网卡。
    /// </summary>
    private static NetworkInterface[] Interfaces()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces();
        }
        catch (Exception exception) when (Readings.IsUnreadable(exception))
        {
            return [];
        }
    }

    /// <summary>
    /// 网卡的收发统计。
    /// </summary>
    private static IPInterfaceStatistics? Statistics(NetworkInterface nic)
    {
        try
        {
            return nic.GetIPStatistics();
        }
        catch (Exception exception) when (Readings.IsUnreadable(exception))
        {
            return null;
        }
    }

    /// <summary>
    /// 网卡的 IP 配置。
    /// </summary>
    private static IPInterfaceProperties? Properties(NetworkInterface nic)
    {
        try
        {
            return nic.GetIPProperties();
        }
        catch (Exception exception) when (Readings.IsUnreadable(exception))
        {
            return null;
        }
    }

    /// <summary>
    /// 描述一块网卡。
    /// </summary>
    /// <remarks>
    /// 连 ID 与名称都读不到时为 <see langword="null"/>。
    /// </remarks>
    private static NetworkInterfaceInfo? Describe(NetworkInterface nic)
    {
        try
        {
            var properties = Properties(nic);
            var statistics = Statistics(nic);
            IReadOnlyList<UnicastIPAddressInformation> unicast = properties is null ? [] : Addresses(() => properties.UnicastAddresses, static a => a);
            return new NetworkInterfaceInfo
            {
                Id = nic.Id,
                Name = nic.Name,
                Description = Readings.Text(() => nic.Description),
                Type = Readings.Optional(() => nic.NetworkInterfaceType) ?? NetworkInterfaceType.Unknown,
                Status = Readings.Optional(() => nic.OperationalStatus) ?? OperationalStatus.Unknown,
                SpeedBitsPerSecond = Readings.Optional(() => nic.Speed) is >= 0 and var speed ? speed : null,
                PhysicalAddress = PhysicalAddressOf(nic),
                IPv4Addresses = [.. unicast.Where(static a => a.Address.AddressFamily == AddressFamily.InterNetwork).Select(WithPrefix)],
                IPv6Addresses = [.. unicast.Where(static a => a.Address.AddressFamily == AddressFamily.InterNetworkV6).Select(WithPrefix)],
                Gateways = properties is null ? [] : Addresses(() => properties.GatewayAddresses, static g => g.Address.ToString()),
                DnsServers = properties is null ? [] : Addresses(() => properties.DnsAddresses, static d => d.ToString()),
                BytesReceived = statistics is null ? null : Readings.Optional(() => statistics.BytesReceived),
                BytesSent = statistics is null ? null : Readings.Optional(() => statistics.BytesSent),
                PacketsReceived = statistics is null ? null : Readings.Optional(() => statistics.UnicastPacketsReceived + statistics.NonUnicastPacketsReceived),
                // Linux 的发送统计不分单播与非单播，单播一项即总包数
                PacketsSent = statistics is null ? null : Readings.Optional(() => OperatingSystem.IsLinux() ? statistics.UnicastPacketsSent : statistics.UnicastPacketsSent + statistics.NonUnicastPacketsSent),
            };
        }
        catch (Exception exception) when (Readings.IsUnreadable(exception))
        {
            return null;
        }
    }

    /// <summary>
    /// 读一组地址，平台不支持时为空列表。
    /// </summary>
    private static IReadOnlyList<TResult> Addresses<TSource, TResult>(Func<IEnumerable<TSource>> read, Func<TSource, TResult> select)
    {
        try
        {
            return [.. read().Select(select)];
        }
        catch (Exception exception) when (Readings.IsUnreadable(exception))
        {
            return [];
        }
    }

    /// <summary>
    /// 冒号分隔的物理地址。
    /// </summary>
    private static string? PhysicalAddressOf(NetworkInterface nic)
    {
        byte[] bytes;
        try
        {
            bytes = nic.GetPhysicalAddress().GetAddressBytes();
        }
        catch (Exception exception) when (Readings.IsUnreadable(exception))
        {
            return null;
        }

        return bytes.Length > 0 ? string.Join(':', bytes.Select(static b => b.ToString("X2", CultureInfo.InvariantCulture))) : null;
    }

    /// <summary>
    /// 地址加上前缀长度。
    /// </summary>
    /// <remarks>
    /// 平台读不到前缀长度时只给地址。
    /// </remarks>
    private static string WithPrefix(UnicastIPAddressInformation address)
        => Readings.Optional(() => address.PrefixLength) is { } prefix ? $"{address.Address}/{prefix.ToString(CultureInfo.InvariantCulture)}" : address.Address.ToString();
}
