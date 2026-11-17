// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using System.Text.Json.Nodes;

namespace EchoPlugin;

/// <summary>
/// 读 <c>ConfigureRequest.configuration</c>
/// </summary>
/// <remarks>
/// <c>spec/plugin/lifecycle.md</c> §7.2：空等同 <c>{}</c>；文本要通过 RFC-0001 的共同 JSON 读取检查（RFC 8259、64 层、合法 Unicode、任何一层都没有重复成员）且是对象，否则插件进入 Failed
/// </remarks>
internal static class ConfigurationText
{
    /// <summary>
    /// 最大嵌套层数
    /// </summary>
    private const int MaxDepth = 64;

    /// <summary>
    /// UTF-8 BOM 解码后的字符
    /// </summary>
    private const char ByteOrderMark = (char)0xFEFF;

    /// <summary>
    /// 读出配置节对象
    /// </summary>
    /// <remarks>
    /// 不合法或不是对象时为 <see langword="null"/>
    /// </remarks>
    public static JsonObject? Parse(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        // BOM 是语法错误：JsonDocument 读字符串时不认它，这里先挡掉，免得以后换读法时悄悄放过
        if (text[0] == ByteOrderMark)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = MaxDepth, AllowDuplicateProperties = false });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            // JsonDocument 不校验字符串内容：逐个取一遍，转义出的孤立代理项在这里失败
            Visit(document.RootElement);
            return JsonObject.Create(document.RootElement.Clone());
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// 取出每个成员名与字符串值
    /// </summary>
    private static void Visit(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    _ = property.Name;
                    Visit(property.Value);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    Visit(item);
                }

                break;
            case JsonValueKind.String:
                _ = element.GetString();
                break;
            default:
                break;
        }
    }
}
