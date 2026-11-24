// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;

namespace XiHan.Framework.Primitives;

/// <summary>
/// 时间戳文本的解析
/// </summary>
/// <remarks>
/// 时刻出进程时写成 RFC 3339 的文本，读入只有一条文法（RFC-0003 规则 8 的读法）：RFC 3339 <c>date-time</c> 的子集，不认闰秒，小数一到九位。截止时间的文本（<see cref="DeadlineText"/>）与消息信封的 <c>time</c>（RFC-0009「信封」，ADR-0283）共用这一个入口。读入按文法整串匹配，不交给 <see cref="DateTimeOffset.TryParse(string?, out DateTimeOffset)"/>：它接受空格分隔、缺偏移、只有日期、RFC 1123 等写法，没有偏移的文本还按本机时区读，同一段文本在不同机器上读出不同的时刻
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public static class TimestampText
{
    /// <summary>
    /// 一秒的小数位里 .NET 能表示的位数（100 纳秒）
    /// </summary>
    private const int TickDigits = 7;

    /// <summary>
    /// 小数最多的位数
    /// </summary>
    private const int MaxFractionDigits = 9;

    /// <summary>
    /// 不带小数、以 <c>Z</c> 结尾的最短写法的长度：<c>2026-11-18T03:25:00Z</c>
    /// </summary>
    private const int MinLength = 20;

    /// <summary>
    /// 换算成 UTC 之后（不计小数）允许的最晚时刻：9999-12-31T23:59:59
    /// </summary>
    private static readonly long _latestWholeSecond = DateTime.MaxValue.Ticks - (DateTime.MaxValue.Ticks % TimeSpan.TicksPerSecond);

    /// <summary>
    /// 按文法读时间戳
    /// </summary>
    /// <remarks>
    /// <c>^[0-9]{4}-[0-9]{2}-[0-9]{2}[Tt][0-9]{2}:[0-9]{2}:[0-9]{2}(\.[0-9]{1,9})?([Zz]|[+-][0-9]{2}:[0-9]{2})\z</c>，日期按公历、不认闰秒，偏移的时 00 到 23、分 00 到 59，换算成 UTC 之后（不计小数）在 0001-01-01T00:00:00 到 9999-12-31T23:59:59 之间；比 100 纳秒更细的小数截断、不进位。读出的时刻偏移为零：文法允许的偏移到 ±23:59，<see cref="DateTimeOffset"/> 装不下 ±14:00 之外的偏移，一律换算成 UTC。不合文法、越界、空串与 <see langword="null"/> 都返回 <see langword="false"/>；首尾的空白不裁剪，带着就读不出
    /// </remarks>
    /// <param name="text">载体里的文本</param>
    /// <param name="timestamp">读出的时刻（UTC）；读不出时为 <c>default</c></param>
    public static bool TryParse([NotNullWhen(true)] string? text, out DateTimeOffset timestamp)
    {
        timestamp = default;
        var span = text.AsSpan();
        if (span.Length < MinLength
            || !TryDigits(span[0..4], out var year) || span[4] != '-'
            || !TryDigits(span[5..7], out var month) || span[7] != '-'
            || !TryDigits(span[8..10], out var day) || span[10] is not ('T' or 't')
            || !TryDigits(span[11..13], out var hour) || span[13] != ':'
            || !TryDigits(span[14..16], out var minute) || span[16] != ':'
            || !TryDigits(span[17..19], out var second))
        {
            return false;
        }

        // 小数：一到九位，超过 7 位的部分截断
        var position = 19;
        long fraction = 0;
        if (span[position] == '.')
        {
            var start = ++position;
            while (position < span.Length && char.IsAsciiDigit(span[position]))
            {
                if (position - start < TickDigits)
                {
                    fraction = (fraction * 10) + (span[position] - '0');
                }

                position++;
            }

            var digits = position - start;
            if (digits is 0 or > MaxFractionDigits)
            {
                return false;
            }

            for (var padding = Math.Min(digits, TickDigits); padding < TickDigits; padding++)
            {
                fraction *= 10;
            }
        }

        if (!TryOffset(span[position..], out var offsetMinutes))
        {
            return false;
        }

        if (year < 1 || month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month) || hour > 23 || minute > 59 || second > 59)
        {
            return false;
        }

        // 先在整秒上换算成 UTC 再判范围：偏移可以把 0001-01-01 推到公元 1 年之前、把 9999-12-31 推到一万年
        var local = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified).Ticks;
        var utc = local - (offsetMinutes * TimeSpan.TicksPerMinute);
        if (utc < 0 || utc > _latestWholeSecond)
        {
            return false;
        }

        timestamp = new DateTimeOffset(utc + fraction, TimeSpan.Zero);
        return true;
    }

    /// <summary>
    /// 偏移：<c>Z</c>、<c>z</c> 或 <c>±HH:MM</c>，之后不能再有字符
    /// </summary>
    private static bool TryOffset(ReadOnlySpan<char> text, out long minutes)
    {
        minutes = 0;
        if (text.Length == 1)
        {
            return text[0] is 'Z' or 'z';
        }

        if (text.Length != 6 || text[0] is not ('+' or '-') || text[3] != ':' || !TryDigits(text[1..3], out var hours) || !TryDigits(text[4..6], out var rest) || hours > 23 || rest > 59)
        {
            return false;
        }

        minutes = (text[0] == '-' ? -1 : 1) * ((hours * 60L) + rest);
        return true;
    }

    /// <summary>
    /// 固定位数的 ASCII 十进制数字
    /// </summary>
    private static bool TryDigits(ReadOnlySpan<char> text, out int value)
    {
        value = 0;
        foreach (var character in text)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }

            value = (value * 10) + (character - '0');
        }

        return true;
    }
}
