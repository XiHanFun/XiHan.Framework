// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Json;

namespace XiHan.Framework.Plugins.Protocol.Mapping;

/// <summary>
/// 协议里 JSON 文本字段的编解码：错误细节与配置节
/// </summary>
internal static class PluginJson
{
    /// <summary>
    /// 错误细节（模板参数）写成 JSON 对象
    /// </summary>
    /// <remarks>
    /// 只处理 JSON 可表示的值，其余按 <see cref="object.ToString"/>
    /// </remarks>
    public static string SerializeDetails(IReadOnlyDictionary<string, object?> details)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var (name, value) in details)
            {
                writer.WritePropertyName(name);
                WriteValue(writer, value);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// 解析错误细节
    /// </summary>
    /// <remarks>
    /// 先过 RFC-0001 的共同 JSON 读取检查（<see cref="JsonText"/>：语法、64 层、合法 Unicode、任何一层的重复成员）；通不过、不是 JSON 对象、是空对象，或任何一层有读成双精度会溢出（±∞）的数，都当作没有细节，返回空（<c>invocation.md</c> §3，ADR-0237，向量 <c>error-reading.json</c>）。下溢的数读成 0，超出 64 位的整数读成双精度。以前直接交给 <see cref="JsonDocument"/>：重复成员取最后一个，转义出的孤立代理项在读值时抛出，溢出的数读成无穷
    /// </remarks>
    public static IReadOnlyDictionary<string, object?>? ParseDetails(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        var utf8 = Encoding.UTF8.GetBytes(json);
        if (JsonText.Check(utf8) is not null)
        {
            return null;
        }

        // 检查通过之后的文本 JsonDocument 一定读得出：深度同为 64 层，字符串都能取值
        using var document = JsonDocument.Parse(utf8, new JsonDocumentOptions { MaxDepth = JsonText.MaxDepth });
        // 空对象与没有细节相同；溢出的数在别的语言里读不出来，整份细节都不要，而不是带着无穷往下传
        if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.EnumerateObject().Any() || !IsFinite(document.RootElement))
        {
            return null;
        }

        var details = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            details[property.Name] = ReadValue(property.Value);
        }

        return details;
    }

    /// <summary>
    /// 任何一层的数读成双精度都不溢出
    /// </summary>
    /// <remarks>
    /// 64 位范围内的整数按整数读，不会溢出；其余的数在 .NET 上溢出时读成 ±∞
    /// </remarks>
    private static bool IsFinite(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Number => element.TryGetInt64(out _) || (element.TryGetDouble(out var value) && double.IsFinite(value)),
        JsonValueKind.Array => element.EnumerateArray().All(IsFinite),
        JsonValueKind.Object => element.EnumerateObject().All(static property => IsFinite(property.Value)),
        _ => true,
    };

    /// <summary>
    /// 配置节序列化为 JSON
    /// </summary>
    /// <remarks>
    /// <c>lifecycle.md</c> §2（ADR-0237）：根总是对象；没有子项的节是叶子，写成 JSON 字符串（值为 null 写 <c>null</c>）；子项名恰好是 <c>0</c>、<c>1</c>…（十进制、不带前导零与符号）的节写成数组，其余写成对象，成员按 <see cref="IConfiguration.GetChildren"/> 的顺序
    /// </remarks>
    public static string SerializeSection(IConfigurationSection section)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteSection(writer, section, root: true);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// 递归写一个配置节
    /// </summary>
    private static void WriteSection(Utf8JsonWriter writer, IConfigurationSection section, bool root)
    {
        var children = section.GetChildren().ToList();
        if (children.Count == 0)
        {
            // 根节没有子项时仍是一个对象（没有声明配置或配置为空）
            if (root)
            {
                writer.WriteStartObject();
                writer.WriteEndObject();
            }
            else
            {
                writer.WriteStringValue(section.Value);
            }

            return;
        }

        // 子项名要逐字等于下标：int.TryParse 认 "+0"、" 1"、"01"，那样的键在别的语言里读成对象成员
        var isArray = !root && children.Select(static (child, index) => string.Equals(child.Key, index.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)).All(static ok => ok);
        if (isArray)
        {
            writer.WriteStartArray();
            foreach (var child in children)
            {
                WriteSection(writer, child, root: false);
            }

            writer.WriteEndArray();
            return;
        }

        writer.WriteStartObject();
        foreach (var child in children)
        {
            writer.WritePropertyName(child.Key);
            WriteSection(writer, child, root: false);
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// 写一个细节值
    /// </summary>
    private static void WriteValue(Utf8JsonWriter writer, object? value)
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
            // 数字按 RFC-0010 的值模型（ADR-0236）：64 位范围内的整数原样，超出时与浮点一样写成双精度
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
                    WriteValue(writer, item);
                }

                writer.WriteEndObject();
                break;
            case IDictionary dictionary:
                // 非字符串键按键的文本（与模板里对象的写法一致）
                writer.WriteStartObject();
                foreach (DictionaryEntry entry in dictionary)
                {
                    writer.WritePropertyName(entry.Key as string ?? (entry.Key as IFormattable)?.ToString(null, CultureInfo.InvariantCulture) ?? entry.Key.ToString() ?? string.Empty);
                    WriteValue(writer, entry.Value);
                }

                writer.WriteEndObject();
                break;
            case IEnumerable items:
                writer.WriteStartArray();
                foreach (var item in items)
                {
                    WriteValue(writer, item);
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

    /// <summary>
    /// 读一个细节值
    /// </summary>
    /// <remarks>
    /// 按 RFC-0010 的值模型：字符串、64 位范围内整数写法的数字为整数、其余数字为双精度、布尔、空；数组为列表，对象为字典，模板里写出的文本与发出方相同
    /// </remarks>
    private static object? ReadValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
        JsonValueKind.Number => element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Array => element.EnumerateArray().Select(ReadValue).ToList(),
        JsonValueKind.Object => element.EnumerateObject().Aggregate(new Dictionary<string, object?>(StringComparer.Ordinal), static (members, property) =>
        {
            members[property.Name] = ReadValue(property.Value);
            return members;
        }),
        _ => null,
    };
}
