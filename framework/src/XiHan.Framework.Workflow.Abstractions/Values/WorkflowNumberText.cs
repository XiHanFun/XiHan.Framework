// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;

namespace XiHan.Framework.Workflow.Abstractions.Values;

/// <summary>
/// 数字文本的读入与数字的规范文本
/// </summary>
/// <remarks>
/// 规则来自 RFC-0021「表达式」的「数字」（ADR-0287），不交给平台的解析：<see cref="decimal.TryParse(string?, NumberStyles, IFormatProvider?, out decimal)"/> 的 <see cref="NumberStyles.Float"/> 还接受结尾的 NUL 字符，指数超过一千位数时与数学值不符。数字是 m × 10^(-s)（m 的绝对值不超过 2^96 - 1，s 在 0 到 28 之间），读入时放不下的按「一半取偶」舍入到放得下的最多小数位，整数部分放不下就不是数字；写出时不带多余的零、不用指数
/// </remarks>
internal static class WorkflowNumberText
{
    /// <summary>
    /// 整数部分最多的位数：2^96 - 1 是 29 位
    /// </summary>
    private const int MaxIntegerDigits = 29;

    /// <summary>
    /// 小数最多的位数
    /// </summary>
    private const int MaxScale = 28;

    /// <summary>
    /// 改写时放在栈上的最多字符数，更长的才分配
    /// </summary>
    private const int StackLimit = 256;

    /// <summary>
    /// 指数累加的饱和值
    /// </summary>
    /// <remarks>
    /// 远大于字符串能有的位数：指数到了这个量级，数字要么放不下、要么舍入成零，再往上加位数不改变结果
    /// </remarks>
    private const long ExponentLimit = 1_000_000_000_000_000;

    /// <summary>
    /// 按文法读数字文本
    /// </summary>
    /// <remarks>
    /// 文法：可选的首尾空白（U+0009–U+000D 与空格）、可选的 <c>+</c> 或 <c>-</c>、ASCII 数字（可带一个小数点，小数点前后至少一侧有数字）、可选的指数（<c>e</c> 或 <c>E</c>、可选的符号、至少一位数字），按整串匹配。不合文法、数值放不下返回 <see langword="false"/>；比 10^(-28) 更细的部分按一半取偶舍入，舍入成零也算读出
    /// </remarks>
    /// <param name="text">数字文本</param>
    /// <param name="number">读出的数字；读不出时为 0</param>
    public static bool TryParse(ReadOnlySpan<char> text, out decimal number)
    {
        number = 0;
        var position = 0;
        while (position < text.Length && IsWhitespace(text[position]))
        {
            position++;
        }

        var end = text.Length;
        while (end > position && IsWhitespace(text[end - 1]))
        {
            end--;
        }

        var negative = false;
        if (position < end && text[position] is '+' or '-')
        {
            negative = text[position] == '-';
            position++;
        }

        var integerStart = position;
        while (position < end && char.IsAsciiDigit(text[position]))
        {
            position++;
        }

        var integer = text[integerStart..position];
        var fraction = ReadOnlySpan<char>.Empty;
        if (position < end && text[position] == '.')
        {
            var fractionStart = ++position;
            while (position < end && char.IsAsciiDigit(text[position]))
            {
                position++;
            }

            fraction = text[fractionStart..position];
        }

        if (integer.Length == 0 && fraction.Length == 0)
        {
            return false;
        }

        var mantissaEnd = position;
        long exponent = 0;
        if (position < end && text[position] is 'e' or 'E')
        {
            position++;
            var negativeExponent = false;
            if (position < end && text[position] is '+' or '-')
            {
                negativeExponent = text[position] == '-';
                position++;
            }

            var exponentStart = position;
            while (position < end && char.IsAsciiDigit(text[position]))
            {
                // 饱和累加：指数再长也不溢出
                exponent = Math.Min((exponent * 10) + (text[position] - '0'), ExponentLimit);
                position++;
            }

            if (position == exponentStart)
            {
                return false;
            }

            exponent = negativeExponent ? -exponent : exponent;
        }

        if (position != end)
        {
            return false;
        }

        if (exponent == 0)
        {
            // 没有指数（或指数是零）的写法平台直接读：舍入（一半取偶，后面还有非零数字时进位）与范围判断都与规则一致
            if (!decimal.TryParse(text[integerStart..mantissaEnd], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var plainMagnitude))
            {
                return false;
            }

            number = negative && plainMagnitude != 0 ? -plainMagnitude : plainMagnitude;
            return true;
        }

        // 有效数字：整数部分接小数部分，去掉开头的零；小数点落在第 point 位有效数字之后（可以为负或超过位数）
        var leadingZeros = 0;
        var total = integer.Length + fraction.Length;
        while (leadingZeros < total && DigitAt(integer, fraction, leadingZeros) == '0')
        {
            leadingZeros++;
        }

        var digits = total - leadingZeros;
        if (digits == 0)
        {
            return true;
        }

        var point = integer.Length - leadingZeros + exponent;
        if (point > MaxIntegerDigits)
        {
            // 至少 10^29，大于 2^96 - 1
            return false;
        }

        if (point < -MaxScale)
        {
            // 小于 10^(-29)，不到最小单位的一半，舍入成零
            return true;
        }

        // 带指数的改写成不带指数的写法再交给平台：平台自己读指数时把它截在一千，位数与指数都上千的数读出来不是数学值
        var zerosBefore = point < 0 ? (int)-point : 0;
        var zerosAfter = point > digits ? (int)point - digits : 0;
        var size = digits + zerosBefore + zerosAfter + 2;
        Span<char> plain = size <= StackLimit ? stackalloc char[size] : new char[size];
        var length = 0;
        if (point <= 0)
        {
            plain[length++] = '0';
            plain[length++] = '.';
            plain.Slice(length, zerosBefore).Fill('0');
            length += zerosBefore;
        }

        for (var index = 0; index < digits; index++)
        {
            if (index == point && point > 0)
            {
                plain[length++] = '.';
            }

            plain[length++] = DigitAt(integer, fraction, leadingZeros + index);
        }

        plain.Slice(length, zerosAfter).Fill('0');
        length += zerosAfter;
        if (!decimal.TryParse(plain[..length], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var magnitude))
        {
            return false;
        }

        // 舍入成零的不留负号：数字没有负零
        number = negative && magnitude != 0 ? -magnitude : magnitude;
        return true;
    }

    /// <summary>
    /// 去掉 decimal 内部记着的结尾的零与负零，数值不变
    /// </summary>
    /// <remarks>
    /// decimal 记着小数位数：<c>1.5 + 1.5</c> 是 <c>3.0</c>，字面量 <c>1.50</c> 是 <c>1.50</c>，写成 JSON 时照这个位数写。表达式得出的数字先经这里（RFC-0021「表达式」的「数字的文本」，ADR-0303）：写成 JSON 就是数字的文本，存进变量再读回来的原文与没存之前相同，读它的工作量也就相同
    /// </remarks>
    public static decimal Normalize(decimal number)
    {
        Span<int> bits = stackalloc int[4];
        decimal.GetBits(number, bits);
        var scale = (bits[3] >> 16) & 0xFF;
        var negative = bits[3] < 0;
        var mantissa = ((UInt128)(uint)bits[2] << 64) | ((UInt128)(uint)bits[1] << 32) | (uint)bits[0];
        if (mantissa == 0)
        {
            // 零没有负号，也没有小数位
            return 0m;
        }

        while (scale > 0 && mantissa % 10 == 0)
        {
            mantissa /= 10;
            scale--;
        }

        return new decimal((int)(uint)mantissa, (int)(uint)(mantissa >> 32), (int)(uint)(mantissa >> 64), negative, (byte)scale);
    }

    /// <summary>
    /// 数字写成恰好 <paramref name="places"/> 位小数的定长文本
    /// </summary>
    /// <remarks>
    /// 内置函数 <c>format</c> 的写法（RFC-0021「表达式」的「内置函数」，ADR-0296）：规范文本补零到 <paramref name="places"/> 位小数，<paramref name="places"/> 为 0 时不写小数点；零（含舍入出来的负零）没有负号，不带千分位、不随区域。调用方先把数字舍入到不超过 <paramref name="places"/> 位小数
    /// </remarks>
    /// <param name="number">已舍入的数字</param>
    /// <param name="places">小数位数，0 到 28</param>
    public static string Format(decimal number, int places)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(places);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(places, MaxScale);
        var text = Format(number);
        var point = text.IndexOf('.', StringComparison.Ordinal);
        var decimals = point < 0 ? 0 : text.Length - point - 1;
        if (decimals > places)
        {
            throw new ArgumentOutOfRangeException(nameof(number), number, $"The number has more than {places} decimal places.");
        }

        if (places == 0)
        {
            return text;
        }

        // 规范文本不带结尾的零：补够位数，整数再补小数点
        return point < 0 ? string.Concat(text, ".", new string('0', places)) : string.Concat(text, new string('0', places - decimals));
    }

    /// <summary>
    /// 数字的规范文本
    /// </summary>
    /// <remarks>
    /// 十进制、不用指数；小数部分不带结尾的零，没有小数部分就不写小数点；负数以 <c>-</c> 开头，零写 <c>0</c>。只取决于数值：<c>1.50</c> 与 <c>1.5</c> 写出相同
    /// </remarks>
    public static string Format(decimal number)
    {
        if (number == 0)
        {
            return "0";
        }

        // decimal 的缺省写法不用指数，但保留它内部的小数位数（1.50 写成 1.50）
        var text = number.ToString(CultureInfo.InvariantCulture);
        return text.Contains('.') ? text.TrimEnd('0').TrimEnd('.') : text;
    }

    /// <summary>
    /// 数字文本首尾允许的空白
    /// </summary>
    private static bool IsWhitespace(char character) => character is ' ' or (>= '\t' and <= '\r');

    /// <summary>
    /// 整数部分接小数部分之后的第 <paramref name="index"/> 位数字
    /// </summary>
    private static char DigitAt(ReadOnlySpan<char> integer, ReadOnlySpan<char> fraction, int index)
        => index < integer.Length ? integer[index] : fraction[index - integer.Length];
}
