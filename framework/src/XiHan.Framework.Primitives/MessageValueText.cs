// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace XiHan.Framework.Primitives;

/// <summary>
/// 模板参数值的文本形式
/// </summary>
/// <remarks>
/// 规则全部来自 RFC-0010「模板」（ADR-0236），不交给平台的格式化：先把值归到可移植的值模型（空、布尔、字符串、64 位整数、IEEE 754 双精度浮点、数组、对象），再按可移植的格式说明符与区域的数字符号写出，各语言 SDK 写出逐字相同的文本
/// </remarks>
internal static class MessageValueText
{
    /// <summary>
    /// 数组与对象的最大嵌套层数
    /// </summary>
    /// <remarks>
    /// 与 JSON 读取的上限相同；更深的容器写成 <c>null</c>，自引用的集合也因此不会无限递归
    /// </remarks>
    private const int MaxDepth = 64;

    /// <summary>
    /// 区域的小数点与分组符
    /// </summary>
    /// <remarks>
    /// 内置的 10 种区域取自 .NET 10（ICU）的实测，表外的区域与不变文化一样是 <c>.</c> 与 <c>,</c>；不读平台的区域数据，不同 ICU 版本与全球化不变模式下结果相同
    /// </remarks>
    private static readonly Dictionary<string, (char Decimal, char Group)> _symbols = new(StringComparer.OrdinalIgnoreCase)
    {
        ["de-DE"] = (',', '.'),
        ["es-ES"] = (',', '.'),
        ["fr-FR"] = (',', '\u202F'),
        ["pt-BR"] = (',', '.'),
        ["ru-RU"] = (',', '\u00A0'),
    };

    /// <summary>
    /// 写出一个参数值
    /// </summary>
    /// <remarks>
    /// <paramref name="format"/> 不是可移植的说明符或不适用于该值时按无格式写出；<paramref name="provider"/> 是 <see cref="CultureInfo"/> 时按它的名字取数字符号，其余按不变文化
    /// </remarks>
    public static string Format(object? value, string? format, IFormatProvider provider)
    {
        var normalized = Normalize(value);
        var (decimalSeparator, groupSeparator) = provider is CultureInfo culture && _symbols.TryGetValue(culture.Name, out var found) ? found : ('.', ',');
        var specifier = Specifier.Parse(format);
        return normalized switch
        {
            null => string.Empty,
            bool flag => flag ? "true" : "false",
            string text => text,
            long integer => Localize(FormatInteger(integer, specifier), decimalSeparator, groupSeparator),
            double number => Localize(FormatFloat(number, specifier), decimalSeparator, groupSeparator),
            _ => Json(normalized),
        };
    }

    /// <summary>
    /// 把值归到可移植的值模型
    /// </summary>
    /// <remarks>
    /// 各宽度的整数在 64 位范围内是 <see cref="long"/>，超出时是 <see cref="double"/>；<see cref="float"/>、<see cref="Half"/> 与 <see cref="decimal"/> 是 <see cref="double"/>；字符是字符串；
    /// 字符串键的字典是对象，其余可枚举是数组（仍是原对象，写出时再展开）；其他 <see cref="IFormattable"/> 取不变文化的无格式文本，其余取 <see cref="object.ToString"/>
    /// </remarks>
    private static object? Normalize(object? value)
        => value switch
        {
            null => null,
            bool or string or long or double => value,
            char character => character.ToString(),
            sbyte or byte or short or ushort or int or uint => Convert.ToInt64(value, CultureInfo.InvariantCulture),
            ulong unsigned => unsigned <= long.MaxValue ? (object)(long)unsigned : (double)unsigned,
            nint native => (long)native,
            nuint native => native <= long.MaxValue ? (object)(long)native : (double)native,
            Int128 wide => wide >= long.MinValue && wide <= long.MaxValue ? (object)(long)wide : (double)wide,
            UInt128 wide => wide <= long.MaxValue ? (object)(long)wide : (double)wide,
            BigInteger big => big >= long.MinValue && big <= long.MaxValue ? (object)(long)big : (double)big,
            float single => (double)single,
            Half half => (double)half,
            decimal exact => (double)exact,
            IDictionary or IEnumerable => value,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };

    /// <summary>
    /// 按说明符写一个整数（不变文化）
    /// </summary>
    private static string FormatInteger(long value, Specifier specifier)
        => specifier.Letter switch
        {
            'D' => value.ToString(specifier.Precision is { } digits ? "D" + digits.ToString(CultureInfo.InvariantCulture) : "D", CultureInfo.InvariantCulture),
            // 负数没有可移植的十六进制写法：.NET 按类型宽度写补码，各语言宽度不同，这里按无格式写出
            'X' or 'x' when value >= 0 => value.ToString(specifier.Letter + (specifier.Precision?.ToString(CultureInfo.InvariantCulture) ?? string.Empty), CultureInfo.InvariantCulture),
            'F' or 'N' => value.ToString(specifier.Letter + (specifier.Precision ?? 2).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
            _ => value.ToString(CultureInfo.InvariantCulture),
        };

    /// <summary>
    /// 按说明符写一个浮点数（不变文化）
    /// </summary>
    /// <remarks>
    /// <c>D</c> 与 <c>X</c> 不适用于浮点，按无格式写出；无格式是能往返的最短十进制，.NET 10 在 1e-4 ≤ |x| &lt; 1e17 时不用指数
    /// </remarks>
    private static string FormatFloat(double value, Specifier specifier)
        => specifier.Letter switch
        {
            'F' or 'N' => value.ToString(specifier.Letter + (specifier.Precision ?? 2).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
            _ => value.ToString(CultureInfo.InvariantCulture),
        };

    /// <summary>
    /// 把不变文化的数字文本换成区域的小数点与分组符
    /// </summary>
    /// <remarks>
    /// 不变文化的数字文本里 <c>.</c> 只是小数点、<c>,</c> 只是分组符，逐字替换即可；两者互换的区域（如 de-DE）不能分两次替换
    /// </remarks>
    private static string Localize(string invariant, char decimalSeparator, char groupSeparator)
    {
        if (decimalSeparator == '.' && groupSeparator == ',')
        {
            return invariant;
        }

        return string.Create(invariant.Length, (invariant, decimalSeparator, groupSeparator), static (span, state) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                var c = state.invariant[i];
                span[i] = c switch
                {
                    '.' => state.decimalSeparator,
                    ',' => state.groupSeparator,
                    _ => c,
                };
            }
        });
    }

    /// <summary>
    /// 数组与对象写成紧凑的 JSON 文本
    /// </summary>
    /// <remarks>
    /// 总按不变文化：嵌套的数字不随区域换小数点，否则分不清分隔符
    /// </remarks>
    private static string Json(object value)
    {
        var builder = new StringBuilder();
        WriteJson(builder, value, 1);
        return builder.ToString();
    }

    /// <summary>
    /// 递归写一个值的 JSON 文本
    /// </summary>
    /// <remarks>
    /// 对象成员按名字的序数顺序（逐个 UTF-16 码元比较），同名成员保持原顺序；超过 <see cref="MaxDepth"/> 层的容器写成 <c>null</c>
    /// </remarks>
    /// <param name="builder">输出</param>
    /// <param name="value">已归一或待归一的值</param>
    /// <param name="depth">本值作为容器时所在的层数，最外层为 1</param>
    private static void WriteJson(StringBuilder builder, object? value, int depth)
    {
        switch (Normalize(value))
        {
            case null:
                builder.Append("null");
                return;
            case bool flag:
                builder.Append(flag ? "true" : "false");
                return;
            case string text:
                WriteString(builder, text);
                return;
            case long integer:
                builder.Append(integer.ToString(CultureInfo.InvariantCulture));
                return;
            case double number:
                builder.Append(number.ToString(CultureInfo.InvariantCulture));
                return;
            case IDictionary dictionary when depth <= MaxDepth:
                var members = new List<(string Name, object? Value)>(dictionary.Count);
                foreach (DictionaryEntry entry in dictionary)
                {
                    members.Add((Format(entry.Key, null, CultureInfo.InvariantCulture), entry.Value));
                }

                WriteObject(builder, members, depth);
                return;
            case IEnumerable items when depth <= MaxDepth && IsReadOnlyDictionary(items, out var pairs):
                WriteObject(builder, pairs, depth);
                return;
            case IEnumerable items when depth <= MaxDepth:
                builder.Append('[');
                var first = true;
                foreach (var item in items)
                {
                    if (!first)
                    {
                        builder.Append(',');
                    }

                    first = false;
                    WriteJson(builder, item, depth + 1);
                }

                builder.Append(']');
                return;
            default:
                // 超过层数上限的容器
                builder.Append("null");
                return;
        }
    }

    /// <summary>
    /// 只读字典也是对象
    /// </summary>
    /// <remarks>
    /// 只实现 <see cref="IReadOnlyDictionary{TKey, TValue}"/>（字符串键、对象值）而不实现 <see cref="IDictionary"/> 的集合
    /// </remarks>
    private static bool IsReadOnlyDictionary(IEnumerable items, out List<(string Name, object? Value)> members)
    {
        if (items is IReadOnlyDictionary<string, object?> dictionary)
        {
            members = new List<(string, object?)>(dictionary.Count);
            foreach (var (name, value) in dictionary)
            {
                members.Add((name, value));
            }

            return true;
        }

        members = null!;
        return false;
    }

    /// <summary>
    /// 写一个对象：成员按名字的序数顺序，排序稳定
    /// </summary>
    private static void WriteObject(StringBuilder builder, List<(string Name, object? Value)> members, int depth)
    {
        builder.Append('{');
        var first = true;
        foreach (var (name, value) in members.OrderBy(static member => member.Name, StringComparer.Ordinal))
        {
            if (!first)
            {
                builder.Append(',');
            }

            first = false;
            WriteString(builder, name);
            builder.Append(':');
            WriteJson(builder, value, depth + 1);
        }

        builder.Append('}');
    }

    /// <summary>
    /// 写一个 JSON 字符串，只转义必须转义的字符
    /// </summary>
    /// <remarks>
    /// <c>"</c> 与 <c>\</c> 加反斜杠，退格、换页、换行、回车、制表符用短写，其余 U+0000–U+001F 与孤立的代理项写成小写十六进制的 <c>\uXXXX</c>，别的字符原样
    /// </remarks>
    private static void WriteString(StringBuilder builder, string text)
    {
        builder.Append('"');
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            switch (c)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (c < ' ')
                    {
                        builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                    {
                        // 成对的代理项是一个字符，原样写出
                        builder.Append(c).Append(text[i + 1]);
                        i++;
                    }
                    else if (char.IsSurrogate(c))
                    {
                        builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(c);
                    }

                    break;
            }
        }

        builder.Append('"');
    }

    /// <summary>
    /// 可移植的格式说明符
    /// </summary>
    /// <remarks>
    /// 文法 <c>^[DdFfNnXx][0-9]{0,2}$</c>：<c>D</c>、<c>F</c>、<c>N</c> 不区分大小写，<c>X</c> 与 <c>x</c> 分别是大写与小写的十六进制；不合文法时 <see cref="Letter"/> 为 <c>'\0'</c>，按无格式写出
    /// </remarks>
    /// <param name="Letter">规范化后的字母（D、F、N 取大写，X 保留大小写）</param>
    /// <param name="Precision">精度；没有写出时为 <see langword="null"/></param>
    private readonly record struct Specifier(char Letter, int? Precision)
    {
        /// <summary>
        /// 解析说明符文本
        /// </summary>
        public static Specifier Parse(string? format)
        {
            if (format is null || format.Length is 0 or > 3)
            {
                return default;
            }

            var letter = format[0] switch
            {
                'D' or 'd' => 'D',
                'F' or 'f' => 'F',
                'N' or 'n' => 'N',
                'X' => 'X',
                'x' => 'x',
                _ => '\0',
            };

            if (letter == '\0')
            {
                return default;
            }

            if (format.Length == 1)
            {
                return new Specifier(letter, null);
            }

            var precision = 0;
            for (var i = 1; i < format.Length; i++)
            {
                if (format[i] is < '0' or > '9')
                {
                    return default;
                }

                precision = precision * 10 + (format[i] - '0');
            }

            return new Specifier(letter, precision);
        }
    }
}
