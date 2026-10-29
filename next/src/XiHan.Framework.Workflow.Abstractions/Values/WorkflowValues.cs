// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Values;

/// <summary>
/// JSON 值与 .NET 值之间的转换（不用反射，AOT 可用）。
/// </summary>
/// <remarks>
/// 数字跨类型、字符串解析成数字 / 布尔 / 时间 / Guid / 枚举，数组转字符串列表。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class WorkflowValues
{
    /// <summary>
    /// 转成 <typeparamref name="T"/>。
    /// </summary>
    /// <remarks>
    /// <see langword="null"/>、转不成返回 <see langword="false"/>。支持 string、bool、int、long、double、decimal、float、DateTimeOffset、DateTime、TimeSpan、Guid、枚举、<see cref="JsonObject"/>、<see cref="JsonArray"/>、<see cref="JsonNode"/>。
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
    /// 转成 <typeparamref name="T"/>，转不成返回 <paramref name="fallback"/>。
    /// </summary>
    public static T? ConvertOr<T>(JsonNode? node, T? fallback) => TryConvert<T>(node, out var value) ? value : fallback;

    /// <summary>
    /// 转成字符串列表。
    /// </summary>
    /// <remarks>
    /// 数组逐项转字符串（跳过 null），字符串按逗号拆分并裁剪，其他标量单元素；<see langword="null"/> 为空列表。
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
    /// 标量的字符串形式：字符串原文，数字按不变文化，布尔 <c>true</c> / <c>false</c>。
    /// </summary>
    public static string AsString(JsonValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.TryGetValue<string>(out var text))
        {
            return text;
        }

        if (value.TryGetValue<bool>(out var flag))
        {
            return flag ? "true" : "false";
        }

        if (TryDecimal(value, out var number))
        {
            return number.ToString(CultureInfo.InvariantCulture);
        }

        if (value.TryGetValue<DateTimeOffset>(out var when))
        {
            return when.ToString("O", CultureInfo.InvariantCulture);
        }

        return value.ToJsonString().Trim('"');
    }

    /// <summary>
    /// 是否"真"：布尔按值，数字非零，字符串非空，数组与对象非空，<see langword="null"/> 为假。
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
    /// 标量转 decimal：数字直接取，字符串按不变文化解析。
    /// </summary>
    public static bool TryDecimal(JsonValue value, out decimal number)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.TryGetValue<decimal>(out number))
        {
            return true;
        }

        if (value.TryGetValue<double>(out var d))
        {
            if (double.IsFinite(d) && d is >= (double)decimal.MinValue and <= (double)decimal.MaxValue)
            {
                number = (decimal)d;
                return true;
            }

            return false;
        }

        if (value.TryGetValue<long>(out var l))
        {
            number = l;
            return true;
        }

        if (value.TryGetValue<int>(out var i))
        {
            number = i;
            return true;
        }

        if (value.TryGetValue<JsonElement>(out var element) && element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out number))
        {
            return true;
        }

        if (value.TryGetValue<string>(out var text) && decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
        {
            return true;
        }

        number = 0;
        return false;
    }

    /// <summary>
    /// 标量转布尔：布尔直接取，字符串 <c>true</c> / <c>false</c>（不区分大小写）。
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
    /// 标量转枚举：按名字（不区分大小写）或数字。
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
