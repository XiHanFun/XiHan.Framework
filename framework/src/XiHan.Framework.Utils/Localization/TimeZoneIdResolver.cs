// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace XiHan.Framework.Utils.Localization;

/// <summary>
/// 时区标识解析器
/// </summary>
/// <remarks>
/// 把规范 IANA ID、旧 IANA 别名与 Windows ID 落到时区目录里已有的时区。
/// 新版 tzdata（以及把 backward 链接拆进 tzdata-legacy 的发行版与容器镜像）不再带 Asia/Calcutta、America/Indianapolis 这类旧别名，
/// 而浏览器报出的时区、Windows 节点上存下的时区常常正是这些别名。
/// </remarks>
internal sealed class TimeZoneIdResolver
{
    private readonly Func<string, TimeZoneInfo?> _findById;
    private readonly bool _isWindows;
    private readonly Lazy<IReadOnlyDictionary<string, TimeZoneInfo>> _windowsZoneIndex;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="findById">按标识直接查找时区，找不到时返回 null</param>
    /// <param name="getSystemTimeZones">列出时区目录里的全部时区</param>
    /// <param name="isWindows">时区目录是否为 Windows 时区目录</param>
    internal TimeZoneIdResolver(Func<string, TimeZoneInfo?> findById, Func<IEnumerable<TimeZoneInfo>> getSystemTimeZones, bool isWindows)
    {
        _findById = findById;
        _isWindows = isWindows;
        // 索引只在前两步都找不到时才建，大小以 Windows 时区个数为上限；建索引失败不缓存异常，下次再建
        _windowsZoneIndex = new Lazy<IReadOnlyDictionary<string, TimeZoneInfo>>(
            () => BuildWindowsZoneIndex(getSystemTimeZones()),
            LazyThreadSafetyMode.PublicationOnly);
    }

    /// <summary>
    /// 按当前操作系统时区目录解析的共享实例
    /// </summary>
    public static TimeZoneIdResolver Shared { get; } = new(FindSystemTimeZone, TimeZoneInfo.GetSystemTimeZones, OperatingSystem.IsWindows());

    /// <summary>
    /// 尝试解析时区标识
    /// </summary>
    /// <param name="timeZoneId">IANA ID（含旧别名）或 Windows ID；前后空白会被忽略</param>
    /// <param name="timeZone">成功时返回时区目录里的时区，其 ID 可能与传入的标识不同</param>
    /// <returns>找到该时区或与它等价的时区时返回 true</returns>
    public bool TryResolve(string? timeZoneId, [NotNullWhen(true)] out TimeZoneInfo? timeZone)
    {
        timeZone = null;
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return false;
        }

        var id = timeZoneId.Trim();
        timeZone = _findById(id);
        if (timeZone is not null)
        {
            return true;
        }

        // 值可能来自另一种操作系统的节点，换成本机目录用的那套标识再找一次
        var hasAlternateId = _isWindows
            ? TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out var alternateId)
            : TimeZoneInfo.TryConvertWindowsIdToIanaId(id, out alternateId);
        if (hasAlternateId && !string.IsNullOrWhiteSpace(alternateId))
        {
            timeZone = _findById(alternateId);
            if (timeZone is not null)
            {
                return true;
            }
        }

        // Windows 目录本身就是 Windows 时区，前两步找不到就没有等价时区
        if (_isWindows)
        {
            return false;
        }

        // 目录里没有旧别名时，按同一个 Windows 时区落到已安装的时区
        var windowsId = GetWindowsId(id);
        return windowsId is not null && _windowsZoneIndex.Value.TryGetValue(windowsId, out timeZone);
    }

    /// <summary>
    /// 按标识在当前操作系统时区目录里查找
    /// </summary>
    /// <param name="timeZoneId">时区标识</param>
    /// <returns>找到的时区；找不到或时区数据无效时返回 null</returns>
    private static TimeZoneInfo? FindSystemTimeZone(string timeZoneId)
    {
        return TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var timeZone) ? timeZone : null;
    }

    /// <summary>
    /// 取标识所属的 Windows 时区
    /// </summary>
    /// <param name="timeZoneId">IANA ID（含旧别名）或 Windows ID</param>
    /// <returns>Windows 时区 ID；不属于任何 Windows 时区时返回 null</returns>
    private static string? GetWindowsId(string timeZoneId)
    {
        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZoneId, out var windowsId))
        {
            return windowsId;
        }

        return TimeZoneInfo.TryConvertWindowsIdToIanaId(timeZoneId, out _) ? timeZoneId : null;
    }

    /// <summary>
    /// 按 Windows 时区给时区目录归组，每组取 ID 按序数排在最前的时区
    /// </summary>
    /// <param name="systemTimeZones">时区目录里的全部时区</param>
    /// <returns>Windows 时区 ID 到代表时区的索引</returns>
    /// <remarks>
    /// 同一个 Windows 时区下的时区现行规则一致，当前时间的换算结果与 Windows 节点相同；
    /// 按序数取第一个，结果不随目录枚举顺序变化。
    /// </remarks>
    private static IReadOnlyDictionary<string, TimeZoneInfo> BuildWindowsZoneIndex(IEnumerable<TimeZoneInfo> systemTimeZones)
    {
        var index = new Dictionary<string, TimeZoneInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in systemTimeZones)
        {
            if (!TimeZoneInfo.TryConvertIanaIdToWindowsId(candidate.Id, out var windowsId))
            {
                continue;
            }

            if (!index.TryGetValue(windowsId, out var current) || string.CompareOrdinal(candidate.Id, current.Id) < 0)
            {
                index[windowsId] = candidate;
            }
        }

        return index;
    }
}
