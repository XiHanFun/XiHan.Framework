// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;

namespace XiHan.Framework.Configuration.Nacos;

/// <summary>
/// 把 Nacos 配置内容展平成 <c>IConfiguration</c> 的键值
/// </summary>
/// <remarks>
/// JSON 对象按 <c>A:B</c> 与数组下标展平，properties 按行解析（规范 RFC-0014「Nacos Provider」的「文档格式」，ADR-0286）。格式不合法抛出 <see cref="FormatException"/>；展平后的键不区分大小写，同一个键出现两次也是格式不合法，不取其中任何一个
/// </remarks>
internal static class NacosDocumentParser
{
    /// <summary>
    /// 解析
    /// </summary>
    /// <exception cref="FormatException">内容不是声明的格式，或展平后有重复的键</exception>
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
    /// JSON
    /// </summary>
    /// <remarks>
    /// 根必须是对象；照收注释与尾逗号，嵌套最多 64 层；字符串取原文，数字取 JSON 文本，布尔写成 <c>True</c> / <c>False</c>，<c>null</c> 保留为空值，重复的键不合法（都与 .NET 的 JSON 配置提供者一致）；空对象与空数组不产生键
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

            try
            {
                Visit(document.RootElement, string.Empty, values);
            }
            catch (InvalidOperationException exception)
            {
                // 成员名或字符串里落单的代理项转义（如 "\ud800"）要到取文本时才被发现，那时抛的不是 JsonException；它同样是文档不合法，不能当成客户端故障漏出去
                throw new FormatException(exception.Message, exception);
            }
        }
    }

    /// <summary>
    /// 递归展平一个元素
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
                Add(values, prefix, null);
                break;
            case JsonValueKind.String:
                Add(values, prefix, element.GetString());
                break;
            default:
                Add(values, prefix, element.ToString());
                break;
        }
    }

    /// <summary>
    /// properties
    /// </summary>
    /// <remarks>
    /// 按 <c>\n</c> 分行，行、键、值两侧的空白都去掉；空行与 <c>#</c> / <c>!</c> 开头的行跳过；第一个 <c>=</c> 或 <c>:</c> 分隔键值，没有分隔符的行值为空串；键里的点换成 <c>:</c>；不认转义与续行；键为空或重复不合法
    /// </remarks>
    private static void ParseProperties(string content, Dictionary<string, string?> values)
    {
        var number = 0;
        foreach (var raw in content.Split('\n'))
        {
            number++;
            var line = raw.Trim();
            if (line.Length == 0 || line[0] is '#' or '!')
            {
                continue;
            }

            var separator = line.IndexOfAny(['=', ':']);
            var key = (separator < 0 ? line : line[..separator]).Trim();
            if (key.Length == 0)
            {
                // 只报行号：这一行分隔符之后是配置的值，原因会进错误详情与日志，不能把值带出去
                throw new FormatException($"Line {number} of the properties document has an empty key.");
            }

            Add(values, key.Replace('.', ':'), separator < 0 ? string.Empty : line[(separator + 1)..].Trim());
        }
    }

    /// <summary>
    /// 记下一个键值
    /// </summary>
    /// <remarks>
    /// 键不区分大小写；键为空或已经有这个键时抛出 <see cref="FormatException"/>
    /// </remarks>
    private static void Add(Dictionary<string, string?> values, string key, string? value)
    {
        // JSON 顶层的成员名是空串时展平出空的键：文档的契约不收空键，留到构造文档时会以参数异常从客户端漏出去，在这里就归为格式不合法
        if (key.Length == 0)
        {
            throw new FormatException("The document has an empty configuration key.");
        }

        // 配置键不区分大小写，文档里写两次（哪怕只差大小写）只能留下一个：悄悄取后一个会让改了前一处的人看不出为什么不生效，与 .NET 的 JSON 配置提供者一样当成格式不合法
        if (!values.TryAdd(key, value))
        {
            throw new FormatException($"The configuration key '{key}' appears more than once; configuration keys are case-insensitive.");
        }
    }
}
