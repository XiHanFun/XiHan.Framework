// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Values;

/// <summary>
/// JSON 值与 .NET 值之间的转换（不用反射，AOT 可用）
/// </summary>
/// <remarks>
/// 数字跨类型、字符串解析成数字 / 布尔 / 时间 / Guid / 枚举，数组转字符串列表
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class WorkflowValues
{
    /// <summary>
    /// 转成 <typeparamref name="T"/>
    /// </summary>
    /// <remarks>
    /// <see langword="null"/>、转不成返回 <see langword="false"/>。支持 string、bool、int、long、double、decimal、float、DateTimeOffset、DateTime、TimeSpan、Guid、枚举、<see cref="JsonObject"/>、<see cref="JsonArray"/>、<see cref="JsonNode"/>
    /// </remarks>
    public static bool TryConvert<T>(JsonNode? node, out T? value)
    {
        value = default;
        if (node is null)
        {
            return false;
        }

        // 容器类型直接按节点类型给出
        if (typeof(T) == typeof(JsonNode))
        {
            value = (T)(object)node;
            return true;
        }

        if (typeof(T) == typeof(JsonObject))
        {
            if (node is JsonObject obj)
            {
                value = (T)(object)obj;
                return true;
            }

            return false;
        }

        if (typeof(T) == typeof(JsonArray))
        {
            if (node is JsonArray array)
            {
                value = (T)(object)array;
                return true;
            }

            return false;
        }

        if (node is not JsonValue scalar)
        {
            return false;
        }

        if (typeof(T) == typeof(string))
        {
            value = (T)(object)AsString(scalar);
            return true;
        }

        if (typeof(T) == typeof(bool))
        {
            if (TryBool(scalar, out var b))
            {
                value = (T)(object)b;
                return true;
            }

            return false;
        }

        if (typeof(T) == typeof(decimal) || typeof(T) == typeof(double) || typeof(T) == typeof(float) || typeof(T) == typeof(int) || typeof(T) == typeof(long))
        {
            if (!TryDecimal(scalar, out var number))
            {
                return false;
            }

            if (typeof(T) == typeof(decimal))
            {
                value = (T)(object)number;
            }
            else if (typeof(T) == typeof(double))
            {
                value = (T)(object)(double)number;
            }
            else if (typeof(T) == typeof(float))
            {
                value = (T)(object)(float)number;
            }
            else if (typeof(T) == typeof(int))
            {
                if (number is < int.MinValue or > int.MaxValue)
                {
                    return false;
                }

                value = (T)(object)(int)number;
            }
            else
            {
                if (number is < long.MinValue or > long.MaxValue)
                {
                    return false;
                }

                value = (T)(object)(long)number;
            }

            return true;
        }

        var text = AsString(scalar);
        if (typeof(T) == typeof(DateTimeOffset))
        {
            if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var when))
            {
                value = (T)(object)when;
                return true;
            }

            return false;
        }

        if (typeof(T) == typeof(DateTime))
        {
            if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var when))
            {
                value = (T)(object)when;
                return true;
            }

            return false;
        }

        if (typeof(T) == typeof(TimeSpan))
        {
            if (TryDecimal(scalar, out var seconds))
            {
                value = (T)(object)TimeSpan.FromSeconds((double)seconds);
                return true;
            }

            if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var span))
            {
                value = (T)(object)span;
                return true;
            }

            return false;
        }

        if (typeof(T) == typeof(Guid))
        {
            if (Guid.TryParse(text, out var guid))
            {
                value = (T)(object)guid;
                return true;
            }

            return false;
        }

        if (typeof(T).IsEnum)
        {
            return TryEnum(scalar, text, out value);
        }

        return false;
    }

    /// <summary>
    /// 转成 <typeparamref name="T"/>，转不成返回 <paramref name="fallback"/>
    /// </summary>
    public static T? ConvertOr<T>(JsonNode? node, T? fallback) => TryConvert<T>(node, out var value) ? value : fallback;

    /// <summary>
    /// 转成字符串列表
    /// </summary>
    /// <remarks>
    /// 数组逐项转字符串（跳过 null），字符串按逗号拆分并裁剪，其他标量单元素；<see langword="null"/> 为空列表
    /// </remarks>
    public static IReadOnlyList<string> ToStringList(JsonNode? node)
    {
        switch (node)
        {
            case null:
                return [];
            case JsonArray array:
                var items = new List<string>(array.Count);
                foreach (var item in array)
                {
                    if (item is JsonValue scalar)
                    {
                        items.Add(AsString(scalar));
                    }
                }

                return items;
            case JsonValue value:
                var text = AsString(value);
                return text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            default:
                return [];
        }
    }

    /// <summary>
    /// 标量的字符串形式：字符串原文，数字的规范文本，布尔 <c>true</c> / <c>false</c>
    /// </summary>
    /// <remarks>
    /// 数字的规范文本只取决于数值（RFC-0021「表达式」的「数字」，ADR-0287）：不用指数，小数部分不带结尾的零，<c>1.50</c> 写成 <c>1.5</c>、<c>3.0</c> 写成 <c>3</c>；超出数字范围的 JSON 数字照 JSON 里的原文。由 CLR 类型构造的值（<see cref="DateTimeOffset"/>、<see cref="Guid"/> 等）按它的 JSON 形式，与过一遍存储之后读到的文本相同
    /// </remarks>
    public static string AsString(JsonValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.TryGetValue<string>(out var text))
        {
            return text;
        }

        switch (value.GetValueKind())
        {
            case JsonValueKind.True:
                return "true";
            case JsonValueKind.False:
                return "false";
            case JsonValueKind.Number:
                return TryDecimal(value, out var number) ? WorkflowNumberText.Format(number) : value.ToJsonString();
            case JsonValueKind.String:
                // 不是字符串的 CLR 值写成 JSON 是字符串：取转义还原之后的内容，不带引号也不带 \uXXXX
                using (var document = JsonDocument.Parse(value.ToJsonString()))
                {
                    return document.RootElement.GetString() ?? string.Empty;
                }

            default:
                return value.ToJsonString();
        }
    }

    /// <summary>
    /// 是否"真"：布尔按值，数字非零，字符串非空，数组与对象非空，<see langword="null"/> 为假
    /// </summary>
    public static bool IsTruthy(JsonNode? node) => node switch
    {
        null => false,
        JsonArray array => array.Count > 0,
        JsonObject obj => obj.Count > 0,
        JsonValue value when TryBool(value, out var flag) => flag,
        JsonValue value when TryDecimal(value, out var number) => number != 0,
        JsonValue value => AsString(value).Length > 0,
        _ => false,
    };

    /// <summary>
    /// 标量转 decimal：数字按数值，字符串按数字文本的文法读
    /// </summary>
    /// <remarks>
    /// 规则见 RFC-0021「表达式」的「数字」（ADR-0287）：数字文本是可选的首尾空白（U+0009–U+000D 与空格）、可选的符号、ASCII 数字（可带小数点）与可选的指数，按整串匹配；数值放不下 96 位整数（绝对值超过 79228162514264337593543950335）返回 <see langword="false"/>，比 10^(-28) 更细的部分按一半取偶舍入。JSON 里的数字按它的原文读，由 CLR 的 <see cref="double"/>、<see cref="float"/> 等构造的数字按它的 JSON 形式读，与过一遍存储之后的值相同
    /// </remarks>
    public static bool TryDecimal(JsonValue value, out decimal number)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.TryGetValue<JsonElement>(out var element))
        {
            // 从 JSON 文本读来的值：数字与字符串都按原文过同一条文法，不经平台的数字解析
            switch (element.ValueKind)
            {
                case JsonValueKind.Number:
                    return TryNumberToken(JsonMarshal.GetRawUtf8Value(element), out number);
                case JsonValueKind.String:
                    return WorkflowNumberText.TryParse(element.GetString(), out number);
                default:
                    number = 0;
                    return false;
            }
        }

        if (value.TryGetValue<decimal>(out number))
        {
            return true;
        }

        if (value.TryGetValue<int>(out var i))
        {
            number = i;
            return true;
        }

        if (value.TryGetValue<long>(out var l))
        {
            number = l;
            return true;
        }

        if (value.TryGetValue<string>(out var text))
        {
            return WorkflowNumberText.TryParse(text, out number);
        }

        if ((value.TryGetValue<double>(out var d) && !double.IsFinite(d)) || (value.TryGetValue<float>(out var f) && !float.IsFinite(f)))
        {
            // 非数与无穷写不成 JSON
            number = 0;
            return false;
        }

        if (value.GetValueKind() == JsonValueKind.Number)
        {
            // 其余 CLR 类型的数字（double、float、short、ulong 等）按它的 JSON 形式：存储写下去再读回来就是这段文本
            return WorkflowNumberText.TryParse(value.ToJsonString(), out number);
        }

        number = 0;
        return false;
    }

    /// <summary>
    /// 按名字取对象的成员，名字区分大小写
    /// </summary>
    /// <remarks>
    /// <see cref="JsonObject"/> 区不区分大小写取决于它是怎样造出来的：按 Web 缺省选项反序列化出来的不区分，写进存储再读回来的区分。表达式里成员名一律按序数比较（RFC-0021「表达式」，ADR-0287）：不区分大小写的对象找到的成员名与要找的不是同一个字符串时当作没有，同一个对象过不过存储结果相同
    /// </remarks>
    internal static bool TryMember(JsonObject obj, string name, out JsonNode? value)
    {
        if (obj.TryGetPropertyValue(name, out value, out var index) && string.Equals(obj.GetAt(index).Key, name, StringComparison.Ordinal))
        {
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// 读 JSON 文本里的一个数字
    /// </summary>
    /// <remarks>
    /// JSON 的数字只有 ASCII 字符，逐字节放宽成字符再过数字文本的文法；短的不分配
    /// </remarks>
    private static bool TryNumberToken(ReadOnlySpan<byte> utf8, out decimal number)
    {
        Span<char> text = utf8.Length <= 128 ? stackalloc char[utf8.Length] : new char[utf8.Length];
        for (var index = 0; index < utf8.Length; index++)
        {
            text[index] = (char)utf8[index];
        }

        return WorkflowNumberText.TryParse(text, out number);
    }

    /// <summary>
    /// 标量转布尔：布尔直接取，字符串 <c>true</c> / <c>false</c>（不区分大小写）
    /// </summary>
    public static bool TryBool(JsonValue value, out bool flag)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.TryGetValue<bool>(out flag))
        {
            return true;
        }

        if (value.TryGetValue<JsonElement>(out var element) && element.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            flag = element.GetBoolean();
            return true;
        }

        if (value.TryGetValue<string>(out var text) && bool.TryParse(text, out flag))
        {
            return true;
        }

        flag = false;
        return false;
    }

    /// <summary>
    /// 标量转枚举：按名字（不区分大小写）或数字
    /// </summary>
    private static bool TryEnum<T>(JsonValue scalar, string text, out T? value)
    {
        value = default;
        if (TryDecimal(scalar, out var number) && !scalar.TryGetValue<string>(out _))
        {
            if (number is < int.MinValue or > int.MaxValue)
            {
                return false;
            }

            // 数字形式：按底层整数值转换（框架的枚举都是 int 底层）
            if (Enum.TryParse(typeof(T), ((int)number).ToString(CultureInfo.InvariantCulture), ignoreCase: true, out var parsed) && Enum.IsDefined(typeof(T), parsed))
            {
                value = (T)parsed;
                return true;
            }

            return false;
        }

        if (Enum.TryParse(typeof(T), text, ignoreCase: true, out var named) && Enum.IsDefined(typeof(T), named))
        {
            value = (T)named;
            return true;
        }

        return false;
    }
}
