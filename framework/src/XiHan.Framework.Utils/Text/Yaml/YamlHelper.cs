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

using System.Text;
using System.Text.RegularExpressions;

namespace XiHan.Framework.Utils.Text.Yaml;

/// <summary>
/// YAML解析器
/// </summary>
public static partial class YamlHelper
{
    private static readonly string[] _separator = ["\r\n", "\n"];

    /// <summary>
    /// 从YAML文件加载字典
    /// </summary>
    /// <param name="filePath">YAML文件路径</param>
    /// <returns>键值对字典</returns>
    public static Dictionary<string, string> LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        var yaml = File.ReadAllText(filePath, Encoding.UTF8);
        return ParseYaml(yaml);
    }

    /// <summary>
    /// 保存字典到YAML文件
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="data">要保存的数据</param>
    public static void SaveToFile(string filePath, Dictionary<string, string> data)
    {
        var yaml = ConvertToYaml(data);
        File.WriteAllText(filePath, yaml, Encoding.UTF8);
    }

    /// <summary>
    /// 解析YAML字符串为字典
    /// </summary>
    /// <param name="yaml">YAML字符串</param>
    /// <returns>键值对字典</returns>
    public static Dictionary<string, string> ParseYaml(string yaml)
    {
        var result = new Dictionary<string, string>();

        // 分行处理
        var lines = yaml.Split(_separator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            // 跳过注释行和空行
            var trimmedLine = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith('#'))
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
            if ((value.StartsWith('\'') && value.EndsWith('\'')) ||
                (value.StartsWith('\"') && value.EndsWith('\"')))
            {
                value = value[1..^1];
            }

            result[key] = value;
        }

        return result;
    }

    /// <summary>
    /// 将字典转换为YAML字符串
    /// </summary>
    /// <param name="data">字典数据</param>
    /// <returns>YAML格式字符串</returns>
    public static string ConvertToYaml(Dictionary<string, string> data)
    {
        var sb = new StringBuilder();

        foreach (var kvp in data)
        {
            var key = kvp.Key;
            var value = kvp.Value;

            // 检查值是否需要引号
            if (NeedsQuotes(value))
            {
                value = $"\"{EscapeYamlString(value)}\"";
            }

            sb.AppendLine($"{key}: {value}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// 解析多层级的YAML(扁平化处理)
    /// </summary>
    /// <param name="yaml">YAML字符串</param>
    /// <returns>扁平化的键值对字典，键使用点号分隔层级</returns>
    public static Dictionary<string, string> ParseNestedYaml(string yaml)
    {
        var result = new Dictionary<string, string>();
        var lines = yaml.Split(_separator, StringSplitOptions.None);

        var currentPrefix = "";
        var indentStack = new Stack<(string Prefix, int Indent)>();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.Trim().StartsWith('#'))
            {
                continue;
            }

            // 计算当前行的缩进级别
            var leadingSpaces = line.Length - line.TrimStart().Length;

            // 回退缩进栈，直到找到合适的父级
            while (indentStack.Count > 0 && leadingSpaces <= indentStack.Peek().Indent)
            {
                var (prefix, indent) = indentStack.Pop();
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

            var fullKey = string.IsNullOrEmpty(currentPrefix) ? key : $"{currentPrefix}.{key}";

            // 如果值不为空，则是叶子节点
            if (!string.IsNullOrEmpty(value))
            {
                // 处理引号包裹的值
                if ((value.StartsWith('\'') && value.EndsWith('\'')) ||
                    (value.StartsWith('\"') && value.EndsWith('\"')))
                {
                    value = value[1..^1];
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

    /// <summary>
    /// 判断字符串是否需要引号包裹
    /// </summary>
    /// <param name="value">要检查的字符串</param>
    /// <returns>是否需要引号</returns>
    private static bool NeedsQuotes(string value)
    {
        // 空字符串需要引号
        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

        // 包含特殊字符需要引号
        if (value.Contains(':') || value.Contains('#') || value.Contains('\n') ||
            value.Contains('{') || value.Contains('}') || value.Contains('[') ||
            value.Contains(']') || value.StartsWith(' ') || value.EndsWith(' '))
        {
            return true;
        }

        // 纯数字或布尔值需要引号来确保被视为字符串
        return value == "true" || value == "false" || value == "null" ||
            NumericValueRegex().IsMatch(value);
    }

    /// <summary>
    /// 转义YAML字符串中的特殊字符
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

    [GeneratedRegex(@"^([^:]+):\s*(.*)$")]
    private static partial Regex YamlKeyValueRegex();

    [GeneratedRegex(@"^-?\d+(\.\d+)?$")]
    private static partial Regex NumericValueRegex();
}
