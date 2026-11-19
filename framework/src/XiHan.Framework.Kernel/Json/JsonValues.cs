// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections;
using System.Globalization;
using System.Numerics;
using System.Text.Json;

namespace XiHan.Framework.Kernel.Json;

/// <summary>
/// 按值模型读写错误细节这类无类型的 JSON 值
/// </summary>
/// <remarks>
/// RFC-0010「模板」的值模型（RFC-0001「Result」，ADR-0236）：字符串、64 位范围内整数写法的数字为整数、其余数字为双精度、布尔、空；数组为列表，对象为字典。
/// 写出时整数值的浮点带小数点或指数，非有限的浮点写成字符串，对方按同一模型读回同一个值。Result 与 Error 的线上编码（<see cref="ResultJson"/>）与插件协议的错误细节共用这一份
/// </remarks>
internal static class JsonValues
{
    /// <summary>
    /// 写一个值
    /// </summary>
    /// <remarks>
    /// 只处理 JSON 可表示的值，其余按 <see cref="IFormattable"/> 的固定区域文本或 <see cref="object.ToString"/>
    /// </remarks>
    public static void Write(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case string text:
                writer.WriteStringValue(text);
                break;
            case bool boolean:
                writer.WriteBooleanValue(boolean);
                break;
            // 64 位范围内的整数原样，超出时与浮点一样写成双精度
            case int or long or short or byte or sbyte or ushort or uint:
                writer.WriteNumberValue(Convert.ToInt64(value, CultureInfo.InvariantCulture));
                break;
            case ulong unsigned:
                WriteInteger(writer, unsigned <= long.MaxValue ? (long)unsigned : null, unsigned);
                break;
            case Int128 wide:
                WriteInteger(writer, wide >= long.MinValue && wide <= long.MaxValue ? (long)wide : null, (double)wide);
                break;
            case UInt128 wide:
                WriteInteger(writer, wide <= long.MaxValue ? (long)wide : null, (double)wide);
                break;
            case BigInteger big:
                WriteInteger(writer, big >= long.MinValue && big <= long.MaxValue ? (long)big : null, (double)big);
                break;
            case float or double or decimal:
                WriteFloat(writer, Convert.ToDouble(value, CultureInfo.InvariantCulture));
                break;
            case Half half:
                WriteFloat(writer, (double)half);
                break;
            case char character:
                writer.WriteStringValue(character.ToString());
                break;
            case JsonElement element:
                element.WriteTo(writer);
                break;
            case IReadOnlyDictionary<string, object?> nested:
                writer.WriteStartObject();
                foreach (var (name, item) in nested)
                {
                    writer.WritePropertyName(name);
                    Write(writer, item);
                }

                writer.WriteEndObject();
                break;
            case IDictionary dictionary:
                // 非字符串键按键的文本（与模板里对象的写法一致）
                writer.WriteStartObject();
                foreach (DictionaryEntry entry in dictionary)
                {
                    writer.WritePropertyName(entry.Key as string ?? (entry.Key as IFormattable)?.ToString(null, CultureInfo.InvariantCulture) ?? entry.Key.ToString() ?? string.Empty);
                    Write(writer, entry.Value);
                }

                writer.WriteEndObject();
                break;
            case IEnumerable items:
                writer.WriteStartArray();
                foreach (var item in items)
                {
                    Write(writer, item);
                }

                writer.WriteEndArray();
                break;
            case IFormattable formattable:
                writer.WriteStringValue(formattable.ToString(null, CultureInfo.InvariantCulture));
                break;
            default:
                writer.WriteStringValue(value.ToString());
                break;
        }
    }

    /// <summary>
    /// 读一个值
    /// </summary>
    /// <remarks>
    /// 超出双精度范围的数读成 ±∞，下溢的读成 0，与 System.Text.Json 一致
    /// </remarks>
    public static object? Read(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
        JsonValueKind.Number => element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Array => element.EnumerateArray().Select(Read).ToList(),
        JsonValueKind.Object => ReadObject(element),
        _ => null,
    };

    /// <summary>
    /// 读一个 JSON 对象的全部成员
    /// </summary>
    /// <remarks>
    /// 成员名按序数比较；调用方已过共同的 JSON 读取检查，没有重复成员
    /// </remarks>
    public static Dictionary<string, object?> ReadObject(JsonElement element)
    {
        var members = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            members[property.Name] = Read(property.Value);
        }

        return members;
    }

    /// <summary>
    /// 任何一层的数读成双精度都不溢出
    /// </summary>
    /// <remarks>
    /// 64 位范围内的整数按整数读，不会溢出；其余的数在 .NET 上溢出时读成 ±∞
    /// </remarks>
    public static bool IsFinite(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Number => element.TryGetInt64(out _) || (element.TryGetDouble(out var value) && double.IsFinite(value)),
        JsonValueKind.Array => element.EnumerateArray().All(IsFinite),
        JsonValueKind.Object => element.EnumerateObject().All(static property => IsFinite(property.Value)),
        _ => true,
    };

    /// <summary>
    /// 写一个整数：64 位范围内原样，超出时写成双精度
    /// </summary>
    private static void WriteInteger(Utf8JsonWriter writer, long? exact, double approximate)
    {
        if (exact is { } integer)
        {
            writer.WriteNumberValue(integer);
        }
        else
        {
            WriteFloat(writer, approximate);
        }
    }

    /// <summary>
    /// 写一个双精度浮点，往返后仍是浮点
    /// </summary>
    /// <remarks>
    /// 整数值的浮点带 <c>.0</c>（<c>2.0</c>、<c>-0.0</c>），否则对方读成整数，<c>D</c> 与 <c>X</c> 的结果就变了；非有限值在 JSON 里写不出，写成字符串 <c>NaN</c>、<c>Infinity</c>、<c>-Infinity</c>，与模板里的文本相同（RFC-0001「Result」，ADR-0236）
    /// </remarks>
    private static void WriteFloat(Utf8JsonWriter writer, double value)
    {
        if (!double.IsFinite(value))
        {
            writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
            return;
        }

        var text = value.ToString("R", CultureInfo.InvariantCulture);
        writer.WriteRawValue(text.AsSpan().IndexOfAny('.', 'E') < 0 ? text + ".0" : text, skipInputValidation: true);
    }
}
