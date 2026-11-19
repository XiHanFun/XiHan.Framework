// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
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
    /// 值按内核的值模型写出（<see cref="JsonValues"/>，与 Result 和 Error 的线上编码同一份）：只处理 JSON 可表示的值，其余按 <see cref="object.ToString"/>
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
                JsonValues.Write(writer, value);
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
        if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.EnumerateObject().Any() || !JsonValues.IsFinite(document.RootElement))
        {
            return null;
        }

        return JsonValues.ReadObject(document.RootElement);
    }

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
}
