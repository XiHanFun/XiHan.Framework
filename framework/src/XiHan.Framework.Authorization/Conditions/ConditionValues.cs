// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace XiHan.Framework.Authorization.Conditions;

/// <summary>
/// 条件值的解析、转换与比较
/// </summary>
/// <remarks>
/// 各值种类统一成一种 CLR 表示：字符串 → <see cref="string"/>、数字 → <see cref="decimal"/>、布尔 → <see cref="bool"/>、
/// 日期时间 → <see cref="DateTimeOffset"/>、日期 → <see cref="DateOnly"/>、时刻 → <see cref="TimeOnly"/>、
/// IP → <see cref="IPAddress"/>（IPv4 映射地址还原成 IPv4），条件值集合里的网段 → <see cref="IPNetwork"/>。
/// </remarks>
internal static partial class ConditionValues
{
    /// <summary>
    /// 解析条件值原文里的一个元素
    /// </summary>
    public static bool TryParseText(string text, ConditionValueKind kind, TimeZoneInfo timeZone, bool allowNetwork, out object value)
    {
        value = null!;
        var trimmed = text.Trim();
        switch (kind)
        {
            case ConditionValueKind.String:
                value = trimmed;
                return true;
            case ConditionValueKind.Number:
                if (decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                {
                    value = number;
                    return true;
                }

                return false;
            case ConditionValueKind.Boolean:
                if (TryParseBoolean(trimmed, out var boolean))
                {
                    value = boolean;
                    return true;
                }

                return false;
            case ConditionValueKind.DateTime:
                if (TryParseDateTime(trimmed, timeZone, out var dateTime))
                {
                    value = dateTime;
                    return true;
                }

                return false;
            case ConditionValueKind.Date:
                if (DateOnly.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    value = date;
                    return true;
                }

                return false;
            case ConditionValueKind.TimeOfDay:
                if (TimeOnly.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
                {
                    value = time;
                    return true;
                }

                return false;
            case ConditionValueKind.IpAddress:
                if (allowNetwork && trimmed.Contains('/'))
                {
                    if (TryParseNetwork(trimmed, out var network))
                    {
                        value = network;
                        return true;
                    }

                    return false;
                }

                if (TryParseAddress(trimmed, out var address))
                {
                    value = address;
                    return true;
                }

                return false;
            default:
                return false;
        }
    }

    /// <summary>
    /// 把属性的原始 CLR 值转换成值种类的统一表示
    /// </summary>
    public static bool TryConvertAttribute(object raw, ConditionValueKind kind, TimeZoneInfo timeZone, out object value)
    {
        value = null!;
        switch (kind)
        {
            case ConditionValueKind.String:
                value = raw switch
                {
                    string text => text,
                    IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                    _ => raw.ToString() ?? string.Empty
                };
                return true;
            case ConditionValueKind.Number:
                switch (raw)
                {
                    case decimal number:
                        value = number;
                        return true;
                    case byte or sbyte or short or ushort or int or uint or long or ulong or float or double:
                        try
                        {
                            value = Convert.ToDecimal(raw, CultureInfo.InvariantCulture);
                            return true;
                        }
                        catch (OverflowException)
                        {
                            return false;
                        }
                    case Enum:
                        value = Convert.ToDecimal(Convert.ToInt64(raw, CultureInfo.InvariantCulture));
                        return true;
                }

                break;
            case ConditionValueKind.Boolean:
                if (raw is bool boolean)
                {
                    value = boolean;
                    return true;
                }

                break;
            case ConditionValueKind.DateTime:
                switch (raw)
                {
                    case DateTimeOffset offset:
                        value = offset;
                        return true;
                    case DateTime dateTime:
                        value = FromDateTime(dateTime, timeZone);
                        return true;
                }

                break;
            case ConditionValueKind.Date:
                switch (raw)
                {
                    case DateOnly date:
                        value = date;
                        return true;
                    case DateTimeOffset offset:
                        value = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(offset, timeZone).DateTime);
                        return true;
                    case DateTime dateTime:
                        value = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(FromDateTime(dateTime, timeZone), timeZone).DateTime);
                        return true;
                }

                break;
            case ConditionValueKind.TimeOfDay:
                switch (raw)
                {
                    case TimeOnly time:
                        value = time;
                        return true;
                    case TimeSpan span when span >= TimeSpan.Zero && span < TimeSpan.FromDays(1):
                        value = TimeOnly.FromTimeSpan(span);
                        return true;
                    case DateTimeOffset offset:
                        value = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(offset, timeZone).DateTime);
                        return true;
                    case DateTime dateTime:
                        value = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(FromDateTime(dateTime, timeZone), timeZone).DateTime);
                        return true;
                }

                break;
            case ConditionValueKind.IpAddress:
                if (raw is IPAddress address)
                {
                    value = Normalize(address);
                    return true;
                }

                break;
        }

        // 其余情况一律按文本再解析一次（字符串形式的数字、日期、地址等）
        return raw is string text2 && TryParseText(text2, kind, timeZone, allowNetwork: false, out value);
    }

    /// <summary>
    /// 两个同种类的值是否相等
    /// </summary>
    public static bool AreEqual(object left, object right, ConditionValueKind kind)
    {
        return kind switch
        {
            ConditionValueKind.String => string.Equals((string)left, (string)right, StringComparison.OrdinalIgnoreCase),
            ConditionValueKind.IpAddress => right is IPNetwork network
                ? network.Contains((IPAddress)left)
                : ((IPAddress)left).Equals((IPAddress)right),
            _ => Compare(left, right, kind) == 0
        };
    }

    /// <summary>
    /// 比较两个同种类的有序值（数字、日期时间、日期、时刻，以及字符串的序数比较）
    /// </summary>
    public static int Compare(object left, object right, ConditionValueKind kind)
    {
        return kind switch
        {
            ConditionValueKind.Number => ((decimal)left).CompareTo((decimal)right),
            ConditionValueKind.Boolean => ((bool)left).CompareTo((bool)right),
            ConditionValueKind.DateTime => ((DateTimeOffset)left).CompareTo((DateTimeOffset)right),
            ConditionValueKind.Date => ((DateOnly)left).CompareTo((DateOnly)right),
            ConditionValueKind.TimeOfDay => ((TimeOnly)left).CompareTo((TimeOnly)right),
            ConditionValueKind.String => string.Compare((string)left, (string)right, StringComparison.OrdinalIgnoreCase),
            _ => throw new InvalidOperationException($"值种类 {kind} 不能比较大小")
        };
    }

    /// <summary>
    /// 是否为可比较大小的值种类
    /// </summary>
    public static bool IsOrdered(ConditionValueKind kind)
    {
        return kind is ConditionValueKind.Number or ConditionValueKind.DateTime or ConditionValueKind.Date or ConditionValueKind.TimeOfDay;
    }

    /// <summary>
    /// 输出给说明用的文本
    /// </summary>
    public static string Format(object value)
    {
        return value switch
        {
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    /// <summary>
    /// 把 IPv4 映射的 IPv6 地址还原成 IPv4
    /// </summary>
    public static IPAddress Normalize(IPAddress address)
    {
        return address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
    }

    private static bool TryParseBoolean(string text, out bool value)
    {
        switch (text.ToLowerInvariant())
        {
            case "true" or "1":
                value = true;
                return true;
            case "false" or "0":
                value = false;
                return true;
            default:
                value = false;
                return false;
        }
    }

    private static bool TryParseDateTime(string text, TimeZoneInfo timeZone, out DateTimeOffset value)
    {
        value = default;
        // 带偏移或 Z 的按原偏移；不带的按给定时区的墙钟解释
        if (OffsetSuffix().IsMatch(text))
        {
            return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
        }

        if (!DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            return false;
        }

        value = FromDateTime(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), timeZone);
        return true;
    }

    private static DateTimeOffset FromDateTime(DateTime dateTime, TimeZoneInfo timeZone)
    {
        return dateTime.Kind switch
        {
            DateTimeKind.Utc => new DateTimeOffset(dateTime, TimeSpan.Zero),
            DateTimeKind.Local => new DateTimeOffset(dateTime),
            _ => new DateTimeOffset(dateTime, timeZone.GetUtcOffset(dateTime))
        };
    }

    private static bool TryParseAddress(string text, out IPAddress address)
    {
        address = IPAddress.None;
        // IPAddress.TryParse 认 "10.1" 这类简写，会落到意料之外的地址；IPv4 只收四段点分，IPv6 不收区域标识
        if (text.Length == 0 || text.Contains('%')
            || (!text.Contains(':') && text.Count(static ch => ch == '.') != 3)
            || !IPAddress.TryParse(text, out var parsed))
        {
            return false;
        }

        address = Normalize(parsed);
        return true;
    }

    private static bool TryParseNetwork(string text, out IPNetwork network)
    {
        network = default;
        var slash = text.IndexOf('/');
        if (!TryParseAddress(text[..slash], out var address))
        {
            return false;
        }

        var maxPrefix = address.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
        if (!int.TryParse(text.AsSpan(slash + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var prefix) || prefix > maxPrefix)
        {
            return false;
        }

        // 主机位不为零的网段（如 203.0.113.5/24）多半是笔误，不替使用者猜是哪个网段
        if (!HostBitsAreZero(address, prefix))
        {
            return false;
        }

        network = new IPNetwork(address, prefix);
        return true;
    }

    private static bool HostBitsAreZero(IPAddress address, int prefix)
    {
        var bytes = address.GetAddressBytes();
        for (var bit = prefix; bit < bytes.Length * 8; bit++)
        {
            if ((bytes[bit / 8] & (0x80 >> (bit % 8))) != 0)
            {
                return false;
            }
        }

        return true;
    }

    [GeneratedRegex(@"(Z|[+-]\d{2}:?\d{2})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OffsetSuffix();
}
