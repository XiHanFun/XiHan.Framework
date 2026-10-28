// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace XiHan.Framework.Security.Abstractions.Conditions;

/// <summary>
/// 条件值的解析、转换与比较。各值种类统一成一种 CLR 表示：字符串 → <see cref="string"/>、数字 → <see cref="decimal"/>、布尔 → <see cref="bool"/>、
/// 日期时间 → <see cref="DateTimeOffset"/>、日期 → <see cref="DateOnly"/>、时刻 → <see cref="TimeOnly"/>、IP → <see cref="IPAddress"/>（IPv4 映射地址还原成 IPv4），条件值集合里的网段 → <see cref="IPNetwork"/>。
/// </summary>
internal static partial class ConditionValues
{
    /// <summary>
    /// 解析条件值原文里的一个元素；<paramref name="allowNetwork"/> 时 IP 可写成 CIDR 网段。
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
            case ConditionValueKind.Number when decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out var number):
                value = number;
                return true;
            case ConditionValueKind.Boolean when TryParseBoolean(trimmed, out var boolean):
                value = boolean;
                return true;
            case ConditionValueKind.DateTime when TryParseDateTime(trimmed, timeZone, out var dateTime):
                value = dateTime;
                return true;
            case ConditionValueKind.Date when DateOnly.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date):
                value = date;
                return true;
            case ConditionValueKind.TimeOfDay when TimeOnly.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time):
                value = time;
                return true;
            case ConditionValueKind.IpAddress when allowNetwork && trimmed.Contains('/'):
                if (TryParseNetwork(trimmed, out var network))
                {
                    value = network;
                    return true;
                }

                return false;
            case ConditionValueKind.IpAddress when TryParseAddress(trimmed, out var address):
                value = address;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// 把属性的原始 CLR 值转换成值种类的统一表示；类型对不上时按文本再解析一次。
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
                    _ => raw.ToString() ?? string.Empty,
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
            case ConditionValueKind.Boolean when raw is bool boolean:
                value = boolean;
                return true;
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
            case ConditionValueKind.IpAddress when raw is IPAddress address:
                value = Normalize(address);
                return true;
        }

        // 其余情况一律按文本再解析一次（字符串形式的数字、日期、地址等）
        return raw is string text2 && TryParseText(text2, kind, timeZone, allowNetwork: false, out value);
    }

    /// <summary>
    /// 两个同种类的值是否相等：字符串不区分大小写，IP 与网段按包含。
    /// </summary>
    public static bool AreEqual(object left, object right, ConditionValueKind kind) => kind switch
    {
        ConditionValueKind.String => string.Equals((string)left, (string)right, StringComparison.OrdinalIgnoreCase),
        ConditionValueKind.IpAddress => right is IPNetwork network ? network.Contains((IPAddress)left) : ((IPAddress)left).Equals((IPAddress)right),
        _ => Compare(left, right, kind) == 0,
    };

    /// <summary>
    /// 比较两个同种类的值（数字、布尔、日期时间、日期、时刻，以及字符串的不区分大小写序数比较）。
    /// </summary>
    public static int Compare(object left, object right, ConditionValueKind kind) => kind switch
    {
        ConditionValueKind.Number => ((decimal)left).CompareTo((decimal)right),
        ConditionValueKind.Boolean => ((bool)left).CompareTo((bool)right),
        ConditionValueKind.DateTime => ((DateTimeOffset)left).CompareTo((DateTimeOffset)right),
        ConditionValueKind.Date => ((DateOnly)left).CompareTo((DateOnly)right),
        ConditionValueKind.TimeOfDay => ((TimeOnly)left).CompareTo((TimeOnly)right),
        ConditionValueKind.String => string.Compare((string)left, (string)right, StringComparison.OrdinalIgnoreCase),
        _ => throw new InvalidOperationException($"Values of kind '{kind}' cannot be ordered."),
    };

    /// <summary>
    /// 是否为可比较大小的值种类。
    /// </summary>
    public static bool IsOrdered(ConditionValueKind kind)
        => kind is ConditionValueKind.Number or ConditionValueKind.DateTime or ConditionValueKind.Date or ConditionValueKind.TimeOfDay;

    /// <summary>
    /// 输出给说明用的不变文化文本。
    /// </summary>
    public static string Format(object value) => value is IFormattable formattable ? formattable.ToString(null, CultureInfo.InvariantCulture) : value.ToString() ?? string.Empty;

    /// <summary>
    /// 把 IPv4 映射的 IPv6 地址还原成 IPv4。
    /// </summary>
    public static IPAddress Normalize(IPAddress address) => address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

    /// <summary>
    /// 布尔：<c>true</c> / <c>false</c> / <c>1</c> / <c>0</c>（不区分大小写）。
    /// </summary>
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

    /// <summary>
    /// 日期时间：带偏移或 Z 的按原偏移，不带的按给定时区的墙钟解释。
    /// </summary>
    private static bool TryParseDateTime(string text, TimeZoneInfo timeZone, out DateTimeOffset value)
    {
        value = default;
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

    /// <summary>
    /// <see cref="DateTime"/> 按 Kind 换成时刻：UTC 与本地按其含义，未指定的按给定时区的墙钟。
    /// </summary>
    private static DateTimeOffset FromDateTime(DateTime dateTime, TimeZoneInfo timeZone) => dateTime.Kind switch
    {
        DateTimeKind.Utc => new DateTimeOffset(dateTime, TimeSpan.Zero),
        DateTimeKind.Local => new DateTimeOffset(dateTime),
        _ => new DateTimeOffset(dateTime, timeZone.GetUtcOffset(dateTime)),
    };

    /// <summary>
    /// IP 地址：IPv4 只收四段点分（<see cref="IPAddress.TryParse(string, out IPAddress)"/> 认 <c>10.1</c> 这类简写），IPv6 不收区域标识。
    /// </summary>
    private static bool TryParseAddress(string text, out IPAddress address)
    {
        address = IPAddress.None;
        if (text.Length == 0
            || text.Contains('%')
            || (!text.Contains(':') && text.Count(static ch => ch == '.') != 3)
            || !IPAddress.TryParse(text, out var parsed))
        {
            return false;
        }

        address = Normalize(parsed);
        return true;
    }

    /// <summary>
    /// CIDR 网段：前缀不超过地址位数，主机位必须为零（如 <c>203.0.113.5/24</c> 多半是笔误，不替使用者猜是哪个网段）。
    /// </summary>
    private static bool TryParseNetwork(string text, out IPNetwork network)
    {
        network = default;
        var slash = text.IndexOf('/', StringComparison.Ordinal);
        if (!TryParseAddress(text[..slash], out var address))
        {
            return false;
        }

        var maxPrefix = address.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
        if (!int.TryParse(text.AsSpan(slash + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var prefix) || prefix > maxPrefix || !HostBitsAreZero(address, prefix))
        {
            return false;
        }

        network = new IPNetwork(address, prefix);
        return true;
    }

    /// <summary>
    /// 前缀之后的位是否全为零。
    /// </summary>
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

    /// <summary>
    /// 日期时间文本末尾的偏移（<c>Z</c> 或 <c>±hh:mm</c>）。
    /// </summary>
    [GeneratedRegex(@"(Z|[+-]\d{2}:?\d{2})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OffsetSuffix();
}
