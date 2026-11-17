// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;

namespace XiHan.Framework.Plugins.Protocol.Handshake;

/// <summary>
/// <c>InvocationMetadata.deadline</c> 的读写与生效的截止时间
/// </summary>
/// <remarks>
/// <c>spec/plugin/invocation.md</c> §2（ADR-0237）：写出用 UTC、七位小数、<c>+00:00</c>（.NET 的 <c>O</c> 格式）；读入按规范的文法严格解析，不交给 <see cref="DateTimeOffset.TryParse(string?, out DateTimeOffset)"/>（它接受空格分隔、缺偏移、RFC 1123 等写法，别的语言做不到同样宽松）；读不出就当作没有，生效的截止时间取它与 gRPC 调用自身截止时间中较早的一个
/// </remarks>
internal static class PluginDeadlines
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
    /// 换算成 UTC 之后（不计小数）允许的最晚时刻：9999-12-31T23:59:59
    /// </summary>
    private static readonly long _latestWholeSecond = DateTime.MaxValue.Ticks - (DateTime.MaxValue.Ticks % TimeSpan.TicksPerSecond);

    /// <summary>
    /// 写成协议的截止时间文本
    /// </summary>
    public static string Format(DateTimeOffset deadline) => deadline.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    /// <summary>
    /// 按规范的文法读截止时间
    /// </summary>
    /// <remarks>
    /// <c>^[0-9]{4}-[0-9]{2}-[0-9]{2}[Tt][0-9]{2}:[0-9]{2}:[0-9]{2}(\.[0-9]{1,9})?([Zz]|[+-][0-9]{2}:[0-9]{2})\z</c>，日期按公历、不认闰秒，换算成 UTC 之后（不计小数）在 0001-01-01T00:00:00 到 9999-12-31T23:59:59 之间；比 100 纳秒更细的小数截断、不进位
    /// </remarks>
    public static bool TryParse(string? text, out DateTimeOffset deadline)
    {
        deadline = default;
        var span = text.AsSpan();
        if (span.Length < 20
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

        deadline = new DateTimeOffset(utc + fraction, TimeSpan.Zero);
        return true;
    }

    /// <summary>
    /// 生效的截止时间
    /// </summary>
    /// <remarks>
    /// 元数据里的截止时间（读不出当作没有）与 gRPC 调用自身截止时间中较早的一个；两者都没有为 <see langword="null"/>
    /// </remarks>
    /// <param name="metadata">元数据的 <c>deadline</c></param>
    /// <param name="call">gRPC 调用自身的截止时间；没有为 <see langword="null"/></param>
    public static DateTimeOffset? Effective(string? metadata, DateTimeOffset? call)
    {
        DateTimeOffset? declared = TryParse(metadata, out var parsed) ? parsed : null;
        return (declared, call) switch
        {
            ({ } a, { } b) => a <= b ? a : b,
            ({ } a, null) => a,
            (null, { } b) => b,
            _ => null,
        };
    }

    /// <summary>
    /// gRPC 服务端看到的调用截止时间
    /// </summary>
    /// <remarks>
    /// <c>ServerCallContext.Deadline</c> 以 <see cref="DateTime.MaxValue"/> 表示没有截止时间，其余是 UTC
    /// </remarks>
    public static DateTimeOffset? FromCall(DateTime deadline)
        => deadline == DateTime.MaxValue ? null : new DateTimeOffset(DateTime.SpecifyKind(deadline, DateTimeKind.Utc));

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
