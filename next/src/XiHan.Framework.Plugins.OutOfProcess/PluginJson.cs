// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace XiHan.Framework.Plugins.OutOfProcess;

/// <summary>
/// 协议里 JSON 文本字段的编解码：错误细节与配置节。
/// </summary>
internal static class PluginJson
{
    /// <summary>
    /// 错误细节（模板参数）写成 JSON 对象；只处理 JSON 可表示的值，其余按 <see cref="object.ToString"/>。
    /// </summary>
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
    /// 解析错误细节；不是 JSON 对象返回空。
    /// </summary>
    public static IReadOnlyDictionary<string, object?>? ParseDetails(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
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
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// 配置节序列化为 JSON：没有子项的节是标量，子键都是从 0 起的连续整数时是数组，否则是对象。
    /// </summary>
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
    /// 递归写一个配置节。
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

        var isArray = children.Select(static (child, index) => int.TryParse(child.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) && number == index).All(static ok => ok);
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
    /// 写一个细节值。
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
            case int or long or short or byte or sbyte or ushort or uint or ulong:
                writer.WriteNumberValue(Convert.ToInt64(value, CultureInfo.InvariantCulture));
                break;
            case float or double or decimal:
                writer.WriteNumberValue(Convert.ToDouble(value, CultureInfo.InvariantCulture));
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
            case System.Collections.IEnumerable items:
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
    /// 读一个细节值：字符串、数字（整数优先）、布尔、空；对象与数组保留为 <see cref="JsonElement"/>。
    /// </summary>
    private static object? ReadValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
        JsonValueKind.Number => element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        _ => element.Clone(),
    };
}
