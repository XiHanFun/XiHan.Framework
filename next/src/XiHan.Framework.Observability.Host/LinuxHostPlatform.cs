// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using XiHan.Framework.Observability.Abstractions;

namespace XiHan.Framework.Observability.Host;

/// <summary>
/// Linux 的读取路径。
/// </summary>
/// <remarks>
/// 读 <c>/proc</c>、<c>/sys</c> 与 <c>pci.ids</c>，路径都相对 <paramref name="root"/>（正式运行是 <c>/</c>，测试指向夹具目录）。磁盘容量仍按 <see cref="DriveInfo"/> 读真实的挂载点。
/// </remarks>
/// <param name="root">文件系统根。</param>
internal sealed class LinuxHostPlatform(string root) : HostPlatform
{
    /// <summary>
    /// <c>pci.ids</c> 的常见位置。
    /// </summary>
    private static readonly string[] _pciIdsPaths = ["usr/share/hwdata/pci.ids", "usr/share/misc/pci.ids", "usr/share/pci.ids"];

    /// <inheritdoc />
    public override ProcessorInfo ReadProcessor()
    {
        var cpuinfo = ReadText("proc/cpuinfo");
        return base.ReadProcessor() with
        {
            Name = Field(cpuinfo, "model name") ?? Field(cpuinfo, "Hardware"),
            Vendor = Field(cpuinfo, "vendor_id"),
            PhysicalCores = PhysicalCores(),
            FrequencyMhz = (ReadNumber("sys/devices/system/cpu/cpu0/cpufreq/base_frequency") ?? ReadNumber("sys/devices/system/cpu/cpu0/cpufreq/cpuinfo_max_freq")) / 1000d,
            CacheBytes = LastLevelCache(),
        };
    }

    /// <inheritdoc />
    /// <remarks>
    /// <c>/proc/stat</c> 首行：空闲含 iowait，总量是前八列之和（guest 已计入 user）。
    /// </remarks>
    public override CpuTimes? ReadCpuTimes()
    {
        var line = ReadText("proc/stat")?.Split('\n').FirstOrDefault(static l => l.StartsWith("cpu ", StringComparison.Ordinal));
        if (line is null)
        {
            return null;
        }

        var values = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).Take(8).Select(static v => ulong.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : (ulong?)null).ToList();
        if (values.Count < 4 || values.Any(static v => v is null))
        {
            return null;
        }

        var idle = values[3]!.Value + (values.Count > 4 ? values[4]!.Value : 0);
        return new CpuTimes(idle, values.Aggregate(0UL, static (sum, v) => sum + v!.Value));
    }

    /// <inheritdoc />
    public override MemoryInfo ReadMemory()
    {
        var meminfo = ReadText("proc/meminfo");
        long? Kilobytes(string key) => Field(meminfo, key) is { } value && long.TryParse(value.Split(' ')[0], NumberStyles.None, CultureInfo.InvariantCulture, out var kb) ? kb * 1024 : null;
        var buffers = Kilobytes("Buffers");
        var cached = Kilobytes("Cached");
        return new MemoryInfo
        {
            TotalBytes = Kilobytes("MemTotal"),
            AvailableBytes = Kilobytes("MemAvailable"),
            FreeBytes = Kilobytes("MemFree"),
            CachedBytes = buffers is null && cached is null ? null : (buffers ?? 0) + (cached ?? 0),
        };
    }

    /// <inheritdoc />
    /// <remarks>
    /// <c>/proc/uptime</c> 的第一列，含挂起的时间。
    /// </remarks>
    public override TimeSpan? ReadSystemUptime()
        => ReadText("proc/uptime")?.Split(' ')[0] is { } seconds && double.TryParse(seconds, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? TimeSpan.FromSeconds(value) : null;

    /// <inheritdoc />
    /// <remarks>
    /// 只取块设备，每个设备取第一个挂载点。
    /// </remarks>
    public override IReadOnlyList<DiskInfo> ReadDisks()
    {
        var disks = new List<DiskInfo>();
        foreach (var mount in BlockDevices(ParseMounts(ReadText("proc/self/mounts"))))
        {
            DriveInfo drive;
            try
            {
                drive = new DriveInfo(mount.MountPoint);
            }
            catch (ArgumentException)
            {
                continue;
            }

            if (Describe(drive, mount.Device, mount.FileSystem) is { } disk)
            {
                disks.Add(disk);
            }
        }

        return disks;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <c>/sys/class/drm/card&lt;N&gt;/device</c>；名称与厂商名查 <c>pci.ids</c>。
    /// </remarks>
    public override IReadOnlyList<GpuInfo> ReadGpus()
    {
        var drm = PathOf("sys/class/drm");
        if (!Directory.Exists(drm))
        {
            return [];
        }

        var gpus = new List<GpuInfo>();
        var names = new PciIds(_pciIdsPaths.Select(PathOf));
        foreach (var card in Directory.EnumerateDirectories(drm).Select(Path.GetFileName).Where(static n => n is ['c', 'a', 'r', 'd', .. var rest] && rest.Length > 0 && rest.All(char.IsAsciiDigit)).Order(StringComparer.Ordinal))
        {
            var device = $"sys/class/drm/{card}/device";
            var uevent = ReadText($"{device}/uevent");
            var driver = Field(uevent, "DRIVER", '=');
            var (vendorId, deviceId) = PciId(Field(uevent, "PCI_ID", '='), ReadLine($"{device}/vendor"), ReadLine($"{device}/device"));
            var (vendorName, deviceName) = names.Find(vendorId, deviceId);
            gpus.Add(new GpuInfo
            {
                Name = deviceName,
                Vendor = vendorName,
                VendorId = vendorId,
                DeviceId = deviceId,
                Driver = driver,
                DriverVersion = driver is null ? null : ReadLine($"sys/module/{driver}/version"),
                MemoryBytes = ReadNumber($"{device}/mem_info_vram_total"),
                TemperatureCelsius = Temperature(device),
            });
        }

        return gpus;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <c>/sys/class/dmi/id</c>；<c>board_serial</c> 只有 root 读得到。
    /// </remarks>
    public override BoardInfo ReadBoard() => new()
    {
        Manufacturer = ReadLine("sys/class/dmi/id/board_vendor"),
        Product = ReadLine("sys/class/dmi/id/board_name"),
        Version = ReadLine("sys/class/dmi/id/board_version"),
        SerialNumber = ReadLine("sys/class/dmi/id/board_serial"),
        SystemManufacturer = ReadLine("sys/class/dmi/id/sys_vendor"),
        SystemProduct = ReadLine("sys/class/dmi/id/product_name"),
        BiosVendor = ReadLine("sys/class/dmi/id/bios_vendor"),
        BiosVersion = ReadLine("sys/class/dmi/id/bios_version"),
        BiosDate = ReadLine("sys/class/dmi/id/bios_date"),
    };

    /// <summary>
    /// 解析 <c>/proc/self/mounts</c>。
    /// </summary>
    /// <remarks>
    /// 路径里的八进制转义（如 <c>\040</c> 表示空格）还原。
    /// </remarks>
    public static IReadOnlyList<MountEntry> ParseMounts(string? text)
    {
        if (text is null)
        {
            return [];
        }

        var mounts = new List<MountEntry>();
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.Split(' ');
            if (fields.Length >= 3)
            {
                mounts.Add(new MountEntry(Unescape(fields[0]), Unescape(fields[1]), fields[2]));
            }
        }

        return mounts;
    }

    /// <summary>
    /// 块设备的挂载：<c>/dev/</c> 开头、不是回环设备，每个设备取第一个。
    /// </summary>
    public static IReadOnlyList<MountEntry> BlockDevices(IReadOnlyList<MountEntry> mounts)
    {
        ArgumentNullException.ThrowIfNull(mounts);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return [.. mounts.Where(m => m.Device.StartsWith("/dev/", StringComparison.Ordinal) && !m.Device.StartsWith("/dev/loop", StringComparison.Ordinal) && seen.Add(m.Device))];
    }

    /// <summary>
    /// 还原挂载表里的八进制转义。
    /// </summary>
    private static string Unescape(string value)
    {
        if (!value.Contains('\\', StringComparison.Ordinal))
        {
            return value;
        }

        var builder = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 3 < value.Length && IsOctal(value[i + 1]) && IsOctal(value[i + 2]) && IsOctal(value[i + 3]))
            {
                builder.Append((char)(((value[i + 1] - '0') * 64) + ((value[i + 2] - '0') * 8) + (value[i + 3] - '0')));
                i += 3;
                continue;
            }

            builder.Append(value[i]);
        }

        return builder.ToString();

        static bool IsOctal(char c) => c is >= '0' and <= '7';
    }

    /// <summary>
    /// 规范成四位小写十六进制的 PCI 厂商与设备 ID。
    /// </summary>
    /// <remarks>
    /// 先用 <c>uevent</c> 的 <c>PCI_ID</c>（如 <c>1002:73BF</c>），没有再用 <c>vendor</c> / <c>device</c> 文件（如 <c>0x1002</c>）。
    /// </remarks>
    private static (string? VendorId, string? DeviceId) PciId(string? pciId, string? vendor, string? device)
    {
        static string? Normalize(string? value)
        {
            var hex = value?.StartsWith("0x", StringComparison.OrdinalIgnoreCase) == true ? value[2..] : value;
            return hex is { Length: 4 } && hex.All(char.IsAsciiHexDigit) ? hex.ToLowerInvariant() : null;
        }

        if (pciId?.Split(':') is [var v, var d])
        {
            return (Normalize(v), Normalize(d));
        }

        return (Normalize(vendor), Normalize(device));
    }

    /// <summary>
    /// <c>/proc/cpuinfo</c> 或 <c>uevent</c> 里第一个同名字段的值。
    /// </summary>
    private static string? Field(string? text, string key, char separator = ':')
    {
        if (text is null)
        {
            return null;
        }

        foreach (var line in text.Split('\n'))
        {
            var index = line.IndexOf(separator, StringComparison.Ordinal);
            if (index > 0 && line.AsSpan(0, index).Trim().SequenceEqual(key))
            {
                return Readings.Clean(line[(index + 1)..]);
            }
        }

        return null;
    }

    /// <summary>
    /// 把缓存大小（如 <c>32K</c>、<c>8M</c>）换成字节数。
    /// </summary>
    private static long? Size(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var multiplier = char.ToUpperInvariant(value[^1]) switch
        {
            'K' => 1024L,
            'M' => 1024L * 1024,
            'G' => 1024L * 1024 * 1024,
            _ => 1L,
        };
        var digits = multiplier == 1 ? value : value[..^1];
        return long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number * multiplier : null;
    }

    /// <summary>
    /// 物理核心数：不同的封装与核心编号组合。
    /// </summary>
    private int? PhysicalCores()
    {
        var cpus = PathOf("sys/devices/system/cpu");
        if (!Directory.Exists(cpus))
        {
            return null;
        }

        var cores = new HashSet<(long, long)>();
        foreach (var cpu in Directory.EnumerateDirectories(cpus).Select(Path.GetFileName).Where(static n => n is ['c', 'p', 'u', .. var rest] && rest.Length > 0 && rest.All(char.IsAsciiDigit)))
        {
            if (ReadNumber($"sys/devices/system/cpu/{cpu}/topology/physical_package_id") is { } package && ReadNumber($"sys/devices/system/cpu/{cpu}/topology/core_id") is { } core)
            {
                cores.Add((package, core));
            }
        }

        return cores.Count > 0 ? cores.Count : null;
    }

    /// <summary>
    /// 最后一级缓存：<c>cpu0</c> 的缓存里级别最高、不是指令缓存的那一项。
    /// </summary>
    private long? LastLevelCache()
    {
        var caches = PathOf("sys/devices/system/cpu/cpu0/cache");
        if (!Directory.Exists(caches))
        {
            return null;
        }

        long? bytes = null;
        long level = 0;
        foreach (var index in Directory.EnumerateDirectories(caches, "index*").Select(Path.GetFileName))
        {
            var prefix = $"sys/devices/system/cpu/cpu0/cache/{index}";
            if (ReadNumber($"{prefix}/level") is { } current && current > level && ReadLine($"{prefix}/type") != "Instruction" && Size(ReadLine($"{prefix}/size")) is { } size)
            {
                level = current;
                bytes = size;
            }
        }

        return bytes;
    }

    /// <summary>
    /// 显卡温度：<c>hwmon</c> 下第一个 <c>temp1_input</c>（毫摄氏度）。
    /// </summary>
    private double? Temperature(string device)
    {
        var hwmon = PathOf($"{device}/hwmon");
        if (!Directory.Exists(hwmon))
        {
            return null;
        }

        return Directory.EnumerateDirectories(hwmon).Select(Path.GetFileName).Order(StringComparer.Ordinal)
            .Select(name => ReadNumber($"{device}/hwmon/{name}/temp1_input"))
            .FirstOrDefault(static t => t is not null) / 1000d;
    }

    /// <summary>
    /// 相对根的路径。
    /// </summary>
    private string PathOf(string relative) => Path.Combine(root, relative);

    /// <summary>
    /// 读整个文件，读不到为空。
    /// </summary>
    private string? ReadText(string relative)
    {
        try
        {
            return File.ReadAllText(PathOf(relative));
        }
        catch (Exception exception) when (Readings.IsUnreadable(exception))
        {
            return null;
        }
    }

    /// <summary>
    /// 读文件的第一行，空白为空。
    /// </summary>
    private string? ReadLine(string relative) => Readings.Clean(ReadText(relative)?.Split('\n')[0]);

    /// <summary>
    /// 读一个整数文件。
    /// </summary>
    private long? ReadNumber(string relative)
        => ReadLine(relative) is { } text && long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value) ? value : null;
}
