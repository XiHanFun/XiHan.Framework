// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// V4-09 的解析与生成：读 4.x 的 <c>{ resource, culture, texts }</c>，推出 5.0 的平铺键 <c>&lt;领域&gt;.&lt;资源&gt;.&lt;键&gt;</c>，写 <c>Localization/&lt;区域&gt;.json</c>。
/// </summary>
internal static class V4LocalizationGenerator
{
    /// <summary>
    /// 写 JSON 用的选项：缩进、不转义非 ASCII。
    /// </summary>
    private static readonly JsonWriterOptions _writer = new() { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// 解析 4.x 资源文件；不是 <c>{ resource, culture, texts }</c> 形态返回 <see langword="false"/>。值不是字符串的取原始 JSON 文本。
    /// </summary>
    public static bool TryParse(string text, out string resource, out string culture, out List<KeyValuePair<string, string>> texts)
    {
        resource = string.Empty;
        culture = string.Empty;
        texts = [];
        try
        {
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("resource", out var resourceElement) || resourceElement.ValueKind != JsonValueKind.String
                || !root.TryGetProperty("culture", out var cultureElement) || cultureElement.ValueKind != JsonValueKind.String
                || !root.TryGetProperty("texts", out var textsElement) || textsElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            resource = resourceElement.GetString()!;
            culture = cultureElement.GetString()!;
            foreach (var property in textsElement.EnumerateObject())
            {
                texts.Add(new KeyValuePair<string, string>(property.Name, property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString()! : property.Value.GetRawText()));
            }

            return resource.Length > 0 && culture.Length > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// 领域段：项目名末段的小写字母与数字，以字母开头。
    /// </summary>
    public static string Domain(string project)
    {
        var stem = V4OperationsGenerator.Stem(project);
        var letters = new string(stem.ToLowerInvariant().Where(static c => c is >= 'a' and <= 'z' or >= '0' and <= '9').ToArray());
        return letters.Length == 0 ? "app" : char.IsAsciiDigit(letters[0]) ? "a" + letters : letters;
    }

    /// <summary>
    /// 平铺键：<c>&lt;领域&gt;.&lt;资源小写&gt;.&lt;键 snake_case&gt;</c>，合消息键文法。
    /// </summary>
    public static string Key(string domain, string resource, string key)
    {
        var resourceSegment = new string(resource.ToLowerInvariant().Where(static c => c is >= 'a' and <= 'z' or >= '0' and <= '9').ToArray());
        if (resourceSegment.Length == 0)
        {
            resourceSegment = "resource";
        }
        else if (char.IsAsciiDigit(resourceSegment[0]))
        {
            resourceSegment = "r" + resourceSegment;
        }

        return $"{domain}.{resourceSegment}.{Snake(key)}";
    }

    /// <summary>
    /// PascalCase / 任意文本 → snake_case：大小写边界插下划线，非字母数字换成下划线并合并，以字母开头。
    /// </summary>
    public static string Snake(string text)
    {
        var builder = new StringBuilder();
        var previousLower = false;
        foreach (var c in text)
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                if (char.IsAsciiLetterUpper(c) && previousLower)
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(c));
                previousLower = char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c);
            }
            else if (builder.Length > 0 && builder[^1] != '_')
            {
                builder.Append('_');
                previousLower = false;
            }
        }

        var snake = builder.ToString().Trim('_');
        return snake.Length == 0 ? "text" : char.IsAsciiDigit(snake[0]) ? "k_" + snake : snake;
    }

    /// <summary>
    /// 读已有的区域文件（键 → 模板）；不是对象返回空。
    /// </summary>
    public static Dictionary<string, string> ReadExisting(string text)
    {
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return entries;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                entries[property.Name] = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString()! : property.Value.GetRawText();
            }
        }
        catch (JsonException)
        {
            // 读不出来就当空：生成的内容整份覆盖
        }

        return entries;
    }

    /// <summary>
    /// 写区域文件：键按字典序，两空格缩进，末尾换行。
    /// </summary>
    public static string Write(IReadOnlyDictionary<string, string> entries, string newLine)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, _writer with { NewLine = newLine }))
        {
            writer.WriteStartObject();
            foreach (var (key, value) in entries.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
            {
                writer.WriteString(key, value);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + newLine;
    }
}
