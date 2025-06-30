#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:YamlHelper
// Guid:df0be652-da53-4c24-8c5f-8b35e2b9c40a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 7:44:28
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace XiHan.Framework.Utils.Text.Yaml;

/// <summary>
/// YAML 操作帮助类
/// 提供 YAML 解析、序列化、反序列化等功能
/// </summary>
public static partial class YamlHelper
{
    private static readonly string[] Separator = ["\r\n", "\n"];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    #region 文件操作

    /// <summary>
    /// 从 YAML 文件加载字典
    /// </summary>
    /// <param name="filePath">YAML 文件路径</param>
    /// <param name="options">解析选项</param>
    /// <returns>键值对字典</returns>
    public static Dictionary<string, string> LoadFromFile(string filePath, YamlParseOptions? options = null)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        var yaml = File.ReadAllText(filePath, Encoding.UTF8);
        return ParseYaml(yaml, options);
    }

    /// <summary>
    /// 异步从 YAML 文件加载字典
    /// </summary>
    /// <param name="filePath">YAML 文件路径</param>
    /// <param name="options">解析选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>键值对字典</returns>
    public static async Task<Dictionary<string, string>> LoadFromFileAsync(string filePath, YamlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        var yaml = await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken);
        return ParseYaml(yaml, options);
    }

    /// <summary>
    /// 保存字典到 YAML 文件
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="data">要保存的数据</param>
    /// <param name="options">序列化选项</param>
    public static void SaveToFile(string filePath, Dictionary<string, string> data, YamlSerializeOptions? options = null)
    {
        var yaml = ConvertToYaml(data, options);
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(filePath, yaml, Encoding.UTF8);
    }

    /// <summary>
    /// 异步保存字典到 YAML 文件
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="data">要保存的数据</param>
    /// <param name="options">序列化选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    public static async Task SaveToFileAsync(string filePath, Dictionary<string, string> data, YamlSerializeOptions? options = null, CancellationToken cancellationToken = default)
    {
        var yaml = ConvertToYaml(data, options);
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        await File.WriteAllTextAsync(filePath, yaml, Encoding.UTF8, cancellationToken);
    }

    /// <summary>
    /// 从 YAML 文件反序列化对象
    /// </summary>
    /// <typeparam name="T">目标对象类型</typeparam>
    /// <param name="filePath">文件路径</param>
    /// <param name="options">反序列化选项</param>
    /// <returns>反序列化的对象</returns>
    public static T DeserializeFromFile<T>(string filePath, YamlDeserializeOptions? options = null)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"文件不存在：{filePath}");
        }

        var yaml = File.ReadAllText(filePath, Encoding.UTF8);
        return Deserialize<T>(yaml, options);
    }

    /// <summary>
    /// 异步从 YAML 文件反序列化对象
    /// </summary>
    /// <typeparam name="T">目标对象类型</typeparam>
    /// <param name="filePath">文件路径</param>
    /// <param name="options">反序列化选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>反序列化的对象</returns>
    public static async Task<T> DeserializeFromFileAsync<T>(string filePath, YamlDeserializeOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"文件不存在：{filePath}");
        }

        var yaml = await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken);
        return await DeserializeAsync<T>(yaml, options, cancellationToken);
    }

    /// <summary>
    /// 将对象序列化并保存到 YAML 文件
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要序列化的对象</param>
    /// <param name="filePath">文件路径</param>
    /// <param name="options">序列化选项</param>
    public static void SerializeToFile<T>(T obj, string filePath, YamlSerializeOptions? options = null)
    {
        var yaml = Serialize(obj, options);
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(filePath, yaml, Encoding.UTF8);
    }

    /// <summary>
    /// 异步将对象序列化并保存到 YAML 文件
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要序列化的对象</param>
    /// <param name="filePath">文件路径</param>
    /// <param name="options">序列化选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    public static async Task SerializeToFileAsync<T>(T obj, string filePath, YamlSerializeOptions? options = null, CancellationToken cancellationToken = default)
    {
        var yaml = await SerializeAsync(obj, options, cancellationToken);
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        await File.WriteAllTextAsync(filePath, yaml, Encoding.UTF8, cancellationToken);
    }

    #endregion 文件操作

    #region 对象序列化与反序列化

    /// <summary>
    /// 将对象序列化为 YAML 字符串
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要序列化的对象</param>
    /// <param name="options">序列化选项</param>
    /// <returns>YAML 字符串</returns>
    /// <exception cref="ArgumentNullException">当对象为空时抛出</exception>
    public static string Serialize<T>(T obj, YamlSerializeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(obj);

        options ??= new YamlSerializeOptions();

        try
        {
            // 先转换为 JSON，再转换为 YAML
            var json = JsonSerializer.Serialize(obj, JsonOptions);
            var jsonElement = JsonSerializer.Deserialize<JsonElement>(json);

            return ConvertJsonElementToYaml(jsonElement, options);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"序列化失败：{ex.Message}", ex);
        }
    }

    /// <summary>
    /// 异步将对象序列化为 YAML 字符串
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要序列化的对象</param>
    /// <param name="options">序列化选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>YAML 字符串</returns>
    public static async Task<string> SerializeAsync<T>(T obj, YamlSerializeOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => Serialize(obj, options), cancellationToken);
    }

    /// <summary>
    /// 从 YAML 字符串反序列化为对象
    /// </summary>
    /// <typeparam name="T">目标对象类型</typeparam>
    /// <param name="yaml">YAML 字符串</param>
    /// <param name="options">反序列化选项</param>
    /// <returns>反序列化的对象</returns>
    /// <exception cref="ArgumentException">当 YAML 字符串为空时抛出</exception>
    /// <exception cref="InvalidOperationException">当反序列化失败时抛出</exception>
    public static T Deserialize<T>(string yaml, YamlDeserializeOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            throw new ArgumentException("YAML 字符串不能为空", nameof(yaml));
        }

        options ??= new YamlDeserializeOptions();

        try
        {
            // 先转换为 JSON，再反序列化
            var json = ConvertYamlToJson(yaml, options);
            var result = JsonSerializer.Deserialize<T>(json, JsonOptions);

            return result ?? throw new InvalidOperationException("反序列化失败：结果为空");
        }
        catch (Exception ex) when (ex is not (ArgumentException or InvalidOperationException))
        {
            throw new InvalidOperationException($"反序列化失败：{ex.Message}", ex);
        }
    }

    /// <summary>
    /// 异步从 YAML 字符串反序列化为对象
    /// </summary>
    /// <typeparam name="T">目标对象类型</typeparam>
    /// <param name="yaml">YAML 字符串</param>
    /// <param name="options">反序列化选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>反序列化的对象</returns>
    public static async Task<T> DeserializeAsync<T>(string yaml, YamlDeserializeOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => Deserialize<T>(yaml, options), cancellationToken);
    }

    #endregion 对象序列化与反序列化

    #region 字典操作

    /// <summary>
    /// 解析 YAML 字符串为字典
    /// </summary>
    /// <param name="yaml">YAML 字符串</param>
    /// <param name="options">解析选项</param>
    /// <returns>键值对字典</returns>
    public static Dictionary<string, string> ParseYaml(string yaml, YamlParseOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return [];
        }

        options ??= new YamlParseOptions();
        var result = new Dictionary<string, string>();

        // 分行处理
        var lines = yaml.Split(Separator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            // 跳过注释行和空行
            var trimmedLine = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmedLine) ||
                (options.IgnoreComments && trimmedLine.StartsWith('#')))
            {
                continue;
            }

            // 解析键值对(格式：key: value)
            var match = YamlKeyValueRegex().Match(trimmedLine);
            if (!match.Success)
            {
                continue;
            }

            var key = match.Groups[1].Value.Trim();
            var value = match.Groups[2].Value.Trim();

            // 处理引号包裹的值
            value = ProcessQuotedValue(value);

            // 类型转换
            if (options.ConvertTypes)
            {
                value = ConvertValueType(value);
            }

            result[key] = value;
        }

        return result;
    }

    /// <summary>
    /// 将字典转换为 YAML 字符串
    /// </summary>
    /// <param name="data">字典数据</param>
    /// <param name="options">序列化选项</param>
    /// <returns>YAML 格式字符串</returns>
    public static string ConvertToYaml(Dictionary<string, string> data, YamlSerializeOptions? options = null)
    {
        if (data.Count == 0)
        {
            return string.Empty;
        }

        options ??= new YamlSerializeOptions();
        var sb = new StringBuilder();

        // 添加文档头
        if (options.IncludeDocumentMarkers)
        {
            sb.AppendLine("---");
        }

        // 添加注释
        if (!string.IsNullOrEmpty(options.HeaderComment))
        {
            foreach (var commentLine in options.HeaderComment.Split('\n'))
            {
                sb.AppendLine($"# {commentLine.Trim()}");
            }
        }

        foreach (var kvp in data.OrderBy(x => x.Key))
        {
            var key = kvp.Key;
            var value = kvp.Value;

            // 检查值是否需要引号
            if (NeedsQuotes(value, options))
            {
                value = $"\"{EscapeYamlString(value)}\"";
            }

            sb.AppendLine($"{key}: {value}");
        }

        // 添加文档尾
        if (options.IncludeDocumentMarkers)
        {
            sb.AppendLine("...");
        }

        return sb.ToString();
    }

    /// <summary>
    /// 解析多层级的 YAML（扁平化处理）
    /// </summary>
    /// <param name="yaml">YAML 字符串</param>
    /// <param name="options">解析选项</param>
    /// <returns>扁平化的键值对字典，键使用点号分隔层级</returns>
    public static Dictionary<string, string> ParseNestedYaml(string yaml, YamlParseOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return [];
        }

        options ??= new YamlParseOptions();
        var result = new Dictionary<string, string>();
        var lines = yaml.Split(Separator, StringSplitOptions.None);

        var currentPrefix = "";
        var indentStack = new Stack<(string Prefix, int Indent)>();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) ||
                (options.IgnoreComments && line.Trim().StartsWith('#')))
            {
                continue;
            }

            // 计算当前行的缩进级别
            var leadingSpaces = line.Length - line.TrimStart().Length;

            // 回退缩进栈，直到找到合适的父级
            while (indentStack.Count > 0 && leadingSpaces <= indentStack.Peek().Indent)
            {
                indentStack.Pop();
                currentPrefix = indentStack.Count > 0 ? indentStack.Peek().Prefix : "";
            }

            var trimmedLine = line.Trim();
            var keyValueMatch = YamlKeyValueRegex().Match(trimmedLine);

            if (!keyValueMatch.Success)
            {
                continue;
            }

            var key = keyValueMatch.Groups[1].Value.Trim();
            var value = keyValueMatch.Groups[2].Value.Trim();

            var fullKey = string.IsNullOrEmpty(currentPrefix) ? key : $"{currentPrefix}{options.KeySeparator}{key}";

            // 如果值不为空，则是叶子节点
            if (!string.IsNullOrEmpty(value))
            {
                value = ProcessQuotedValue(value);

                if (options.ConvertTypes)
                {
                    value = ConvertValueType(value);
                }

                result[fullKey] = value;
            }
            // 如果值为空，则是中间节点
            else
            {
                indentStack.Push((fullKey, leadingSpaces));
                currentPrefix = fullKey;
            }
        }

        return result;
    }

    #endregion 字典操作

    #region 验证和转换

    /// <summary>
    /// 验证 YAML 字符串是否有效
    /// </summary>
    /// <param name="yaml">YAML 字符串</param>
    /// <param name="errorMessage">错误信息</param>
    /// <returns>是否有效</returns>
    public static bool IsValidYaml(string yaml, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(yaml))
        {
            errorMessage = "YAML 字符串为空";
            return false;
        }

        try
        {
            ParseYaml(yaml);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// 验证 YAML 字符串是否有效（简化版本）
    /// </summary>
    /// <param name="yaml">YAML 字符串</param>
    /// <returns>是否有效</returns>
    public static bool IsValidYaml(string yaml)
    {
        return IsValidYaml(yaml, out _);
    }

    /// <summary>
    /// YAML 转 JSON 字符串
    /// </summary>
    /// <param name="yaml">YAML 字符串</param>
    /// <param name="options">转换选项</param>
    /// <returns>JSON 字符串</returns>
    public static string YamlToJson(string yaml, YamlDeserializeOptions? options = null)
    {
        return ConvertYamlToJson(yaml, options);
    }

    /// <summary>
    /// JSON 转 YAML 字符串
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="options">序列化选项</param>
    /// <returns>YAML 字符串</returns>
    public static string JsonToYaml(string json, YamlSerializeOptions? options = null)
    {
        try
        {
            var jsonElement = JsonSerializer.Deserialize<JsonElement>(json);
            return ConvertJsonElementToYaml(jsonElement, options);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"JSON 转 YAML 失败：{ex.Message}", ex);
        }
    }

    /// <summary>
    /// 格式化 YAML 字符串
    /// </summary>
    /// <param name="yaml">YAML 字符串</param>
    /// <param name="options">序列化选项</param>
    /// <returns>格式化后的 YAML 字符串</returns>
    public static string FormatYaml(string yaml, YamlSerializeOptions? options = null)
    {
        try
        {
            var dict = ParseNestedYaml(yaml);
            return ConvertToYaml(dict, options);
        }
        catch
        {
            return yaml; // 如果格式化失败，返回原始字符串
        }
    }

    #endregion 验证和转换

    #region 私有辅助方法

    /// <summary>
    /// 处理引号包裹的值
    /// </summary>
    /// <param name="value">原始值</param>
    /// <returns>处理后的值</returns>
    private static string ProcessQuotedValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        // 处理引号包裹的值
        if ((value.StartsWith('\'') && value.EndsWith('\'')) ||
            (value.StartsWith('\"') && value.EndsWith('\"')))
        {
            value = value[1..^1];
            // 处理转义字符
            value = UnescapeYamlString(value);
        }

        return value;
    }

    /// <summary>
    /// 转换值类型
    /// </summary>
    /// <param name="value">原始值</param>
    /// <returns>转换后的值</returns>
    private static string ConvertValueType(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        // 布尔值转换
        if (value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            return value.ToLowerInvariant();
        }

        // null 值转换
        if (value.Equals("null", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("~", StringComparison.OrdinalIgnoreCase))
        {
            return "null";
        }

        // 数字格式化
        if (NumericValueRegex().IsMatch(value))
        {
            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var decimalValue))
            {
                return decimalValue.ToString(CultureInfo.InvariantCulture);
            }
        }

        return value;
    }

    /// <summary>
    /// 判断字符串是否需要引号包裹
    /// </summary>
    /// <param name="value">要检查的字符串</param>
    /// <param name="options">序列化选项</param>
    /// <returns>是否需要引号</returns>
    private static bool NeedsQuotes(string value, YamlSerializeOptions? options = null)
    {
        options ??= new YamlSerializeOptions();

        // 空字符串需要引号
        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

        // 包含特殊字符需要引号
        if (value.Contains(':') || value.Contains('#') || value.Contains('\n') ||
            value.Contains('{') || value.Contains('}') || value.Contains('[') ||
            value.Contains(']') || value.StartsWith(' ') || value.EndsWith(' ') ||
            value.Contains('\t') || value.Contains('\r'))
        {
            return true;
        }

        // 如果强制引号字符串
        if (options.ForceQuoteStrings)
        {
            return !NumericValueRegex().IsMatch(value) &&
                   !value.Equals("true", StringComparison.OrdinalIgnoreCase) &&
                   !value.Equals("false", StringComparison.OrdinalIgnoreCase) &&
                   !value.Equals("null", StringComparison.OrdinalIgnoreCase);
        }

        // 纯数字或布尔值需要引号来确保被视为字符串
        return value == "true" || value == "false" || value == "null" ||
               NumericValueRegex().IsMatch(value);
    }

    /// <summary>
    /// 转义 YAML 字符串中的特殊字符
    /// </summary>
    /// <param name="value">需要转义的字符串</param>
    /// <returns>转义后的字符串</returns>
    private static string EscapeYamlString(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    /// <summary>
    /// 反转义 YAML 字符串中的特殊字符
    /// </summary>
    /// <param name="value">需要反转义的字符串</param>
    /// <returns>反转义后的字符串</returns>
    private static string UnescapeYamlString(string value)
    {
        return value
            .Replace("\\\"", "\"")
            .Replace("\\n", "\n")
            .Replace("\\r", "\r")
            .Replace("\\t", "\t")
            .Replace("\\\\", "\\");
    }

    /// <summary>
    /// 将 JsonElement 转换为 YAML 字符串
    /// </summary>
    /// <param name="element">JSON 元素</param>
    /// <param name="options">序列化选项</param>
    /// <param name="indent">缩进级别</param>
    /// <returns>YAML 字符串</returns>
    private static string ConvertJsonElementToYaml(JsonElement element, YamlSerializeOptions? options = null, int indent = 0)
    {
        options ??= new YamlSerializeOptions();
        var sb = new StringBuilder();
        var indentStr = new string(' ', indent * options.IndentSize);

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    sb.Append($"{indentStr}{property.Name}:");

                    if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    {
                        sb.AppendLine();
                        sb.Append(ConvertJsonElementToYaml(property.Value, options, indent + 1));
                    }
                    else
                    {
                        sb.AppendLine($" {ConvertJsonElementToYaml(property.Value, options).Trim()}");
                    }
                }
                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    sb.Append($"{indentStr}- ");
                    if (item.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    {
                        sb.AppendLine();
                        sb.Append(ConvertJsonElementToYaml(item, options, indent + 1));
                    }
                    else
                    {
                        sb.AppendLine(ConvertJsonElementToYaml(item, options).Trim());
                    }
                }
                break;

            case JsonValueKind.String:
                var stringValue = element.GetString() ?? "";
                if (NeedsQuotes(stringValue, options))
                {
                    sb.Append($"\"{EscapeYamlString(stringValue)}\"");
                }
                else
                {
                    sb.Append(stringValue);
                }
                break;

            case JsonValueKind.Number:
                sb.Append(element.GetRawText());
                break;

            case JsonValueKind.True:
                sb.Append("true");
                break;

            case JsonValueKind.False:
                sb.Append("false");
                break;

            case JsonValueKind.Null:
                sb.Append("null");
                break;
        }

        return sb.ToString();
    }

    /// <summary>
    /// 将 YAML 转换为 JSON 字符串
    /// </summary>
    /// <param name="yaml">YAML 字符串</param>
    /// <param name="options">反序列化选项</param>
    /// <returns>JSON 字符串</returns>
    private static string ConvertYamlToJson(string yaml, YamlDeserializeOptions? options = null)
    {
        options ??= new YamlDeserializeOptions();

        try
        {
            var dict = ParseNestedYaml(yaml, new YamlParseOptions
            {
                IgnoreComments = options.IgnoreComments,
                ConvertTypes = options.ConvertTypes,
                KeySeparator = options.KeySeparator
            });

            // 重建嵌套结构
            var nested = BuildNestedStructure(dict, options.KeySeparator);

            return JsonSerializer.Serialize(nested, JsonOptions);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"YAML 转 JSON 失败：{ex.Message}", ex);
        }
    }

    /// <summary>
    /// 从扁平化字典重建嵌套结构
    /// </summary>
    /// <param name="flatDict">扁平化字典</param>
    /// <param name="separator">键分隔符</param>
    /// <returns>嵌套结构</returns>
    private static object BuildNestedStructure(Dictionary<string, string> flatDict, string separator)
    {
        var result = new Dictionary<string, object>();

        foreach (var kvp in flatDict)
        {
            var keys = kvp.Key.Split(separator);
            var current = result;

            for (var i = 0; i < keys.Length - 1; i++)
            {
                if (!current.ContainsKey(keys[i]))
                {
                    current[keys[i]] = new Dictionary<string, object>();
                }
                current = (Dictionary<string, object>)current[keys[i]];
            }

            // 尝试转换值类型
            var value = ConvertStringValue(kvp.Value);
            current[keys[^1]] = value;
        }

        return result;
    }

    /// <summary>
    /// 转换字符串值为适当的类型
    /// </summary>
    /// <param name="value">字符串值</param>
    /// <returns>转换后的值</returns>
    private static object ConvertStringValue(string value)
    {
        return string.IsNullOrEmpty(value) || value == "null"
            ? null!
            : value.Equals("true", StringComparison.OrdinalIgnoreCase)
            ? true
            : value.Equals("false", StringComparison.OrdinalIgnoreCase)
            ? false
            : int.TryParse(value, out var intValue)
            ? intValue
            : double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var doubleValue) ? doubleValue : value;
    }

    #endregion 私有辅助方法

    #region 正则表达式

    [GeneratedRegex(@"^([^:]+):\s*(.*)$")]
    private static partial Regex YamlKeyValueRegex();

    [GeneratedRegex(@"^-?\d+(\.\d+)?$")]
    private static partial Regex NumericValueRegex();

    #endregion 正则表达式
}
