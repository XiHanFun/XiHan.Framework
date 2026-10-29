// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Configuration.Nacos;

/// <summary>
/// 把 Nacos 配置内容展平成 <c>IConfiguration</c> 的键值。
/// </summary>
/// <remarks>
/// JSON 对象按 <c>A:B</c> 与数组下标展平，properties 按行解析。格式不合法抛出 <see cref="FormatException"/>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class NacosDocumentParser
{
    /// <summary>
    /// 解析。
    /// </summary>
    /// <exception cref="FormatException">内容不是声明的格式。</exception>
    public static IReadOnlyDictionary<string, string?> Parse(string content, NacosConfigFormat format)
    {
        ArgumentNullException.ThrowIfNull(content);
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        switch (format)
        {
            case NacosConfigFormat.Json:
                ParseJson(content, values);
                break;
            case NacosConfigFormat.Properties:
                ParseProperties(content, values);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown configuration format.");
        }

        return values;
    }

    /// <summary>
    /// JSON。
    /// </summary>
    /// <remarks>
    /// 根必须是对象；字符串取原文，数字与布尔取 JSON 文本，<c>null</c> 保留为空值（与 .NET 的 JSON 配置提供者一致）。
    /// </remarks>
    private static void ParseJson(string content, Dictionary<string, string?> values)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(content, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        }
        catch (JsonException exception)
        {
            throw new FormatException(exception.Message, exception);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException("The top-level JSON element must be an object.");
            }

            Visit(document.RootElement, string.Empty, values);
        }
    }

    /// <summary>
    /// 递归展平一个元素。
    /// </summary>
    private static void Visit(JsonElement element, string prefix, Dictionary<string, string?> values)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    Visit(property.Value, prefix.Length == 0 ? property.Name : prefix + ":" + property.Name, values);
                }

                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    Visit(item, prefix + ":" + index, values);
                    index++;
                }

                break;
            case JsonValueKind.Null:
                values[prefix] = null;
                break;
            case JsonValueKind.String:
                values[prefix] = element.GetString();
                break;
            default:
                values[prefix] = element.ToString();
                break;
        }
    }

    /// <summary>
    /// properties。
    /// </summary>
    /// <remarks>
    /// 空行与 <c>#</c> / <c>!</c> 开头的行跳过；第一个 <c>=</c> 或 <c>:</c> 分隔键值，没有分隔符的行值为空串；键里的点换成 <c>:</c>。
    /// </remarks>
    private static void ParseProperties(string content, Dictionary<string, string?> values)
    {
        foreach (var raw in content.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] is '#' or '!')
            {
                continue;
            }

            var separator = line.IndexOfAny(['=', ':']);
            var key = (separator < 0 ? line : line[..separator]).Trim();
            if (key.Length == 0)
            {
                throw new FormatException($"The properties line '{line}' has an empty key.");
            }

            values[key.Replace('.', ':')] = separator < 0 ? string.Empty : line[(separator + 1)..].Trim();
        }
    }
}
