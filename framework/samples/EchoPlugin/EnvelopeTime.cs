// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text.RegularExpressions;

namespace EchoPlugin;

/// <summary>
/// 读宿主写来的信封 <c>time</c>
/// </summary>
/// <remarks>
/// <c>spec/plugin/invocation.md</c> §4「SDK 读宿主写来的」：空串是没有时间；否则按 RFC-0003 规则 8 的文法整串匹配、不裁剪空白，年 0001–9999、月日按公历、时 00–23、分与秒 00–59（不认闰秒）、偏移的时 00–23 与分 00–59，
/// 换算成 UTC 之后（不计小数）在 0001-01-01T00:00:00 到 9999-12-31T23:59:59 之间。读不出时宿主的这次投递以 gRPC <c>InvalidArgument</c> 拒绝（ADR-0306）。示例不引用 XiHan 包，这里按规范自己读
/// </remarks>
internal static partial class EnvelopeTime
{
    /// <summary>
    /// 换算成 UTC 之后最晚的整秒，从 0001-01-01T00:00:00 起算
    /// </summary>
    private static readonly long _latest = DateTime.MaxValue.Ticks / TimeSpan.TicksPerSecond;

    /// <summary>
    /// 信封 <c>time</c> 是否读得出
    /// </summary>
    /// <param name="text">宿主写来的文本</param>
    /// <returns>空串或读得出的文本为真</returns>
    public static bool IsReadable(string text)
    {
        if (text.Length == 0)
        {
            return true;
        }

        var match = Grammar().Match(text);
        if (!match.Success)
        {
            return false;
        }

        var year = Number(match, "year");
        var month = Number(match, "month");
        var day = Number(match, "day");
        var hour = Number(match, "hour");
        var minute = Number(match, "minute");
        var second = Number(match, "second");
        if (year < 1 || month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month) || hour > 23 || minute > 59 || second > 59)
        {
            return false;
        }

        var offset = 0;
        if (match.Groups["sign"].Success)
        {
            var offsetHour = Number(match, "offsetHour");
            var offsetMinute = Number(match, "offsetMinute");
            if (offsetHour > 23 || offsetMinute > 59)
            {
                return false;
            }

            offset = (match.Groups["sign"].Value == "-" ? -1 : 1) * ((offsetHour * 3600) + (offsetMinute * 60));
        }

        // 本地的整秒减去偏移就是 UTC：0001 年 1 月 1 日的偏移为正、9999 年 12 月 31 日的偏移为负时会越过范围
        var local = (new DateTime(year, month, day).Ticks / TimeSpan.TicksPerSecond) + (hour * 3600L) + (minute * 60L) + second;
        var utc = local - offset;
        return utc >= 0 && utc <= _latest;
    }

    /// <summary>
    /// 取一段数字
    /// </summary>
    private static int Number(Match match, string group) => int.Parse(match.Groups[group].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture);

    /// <summary>
    /// RFC-0003 规则 8 的文法
    /// </summary>
    /// <remarks>
    /// <c>[0-9]</c> 只认 ASCII 数字（<c>\d</c> 会认全角等别的数字），<c>\z</c> 不让结尾的换行通过
    /// </remarks>
    [GeneratedRegex(@"^(?<year>[0-9]{4})-(?<month>[0-9]{2})-(?<day>[0-9]{2})[Tt](?<hour>[0-9]{2}):(?<minute>[0-9]{2}):(?<second>[0-9]{2})(\.[0-9]{1,9})?([Zz]|(?<sign>[+-])(?<offsetHour>[0-9]{2}):(?<offsetMinute>[0-9]{2}))\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex Grammar();
}
