#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:JsonLogFormatter
// Guid:0eceaf38-a704-4ac5-88e4-5785baf05e71
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/18 5:47:49
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Logging.Formatters;

/// <summary>
/// JSON 格式化器
/// </summary>
public class JsonLogFormatter : ILogFormatter
{
    /// <summary>
    /// 格式化日志消息
    /// </summary>
    /// <param name="timestamp"></param>
    /// <param name="level"></param>
    /// <param name="message"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public string Format(DateTimeOffset timestamp, LogLevel level, string message, Dictionary<string, object>? context = null)
    {
        var jsonParts = new List<string>
        {
            $"\"timestamp\":\"{timestamp:yyyy-MM-ddTHH:mm:ss.fffZ}\"",
            $"\"level\":\"{level}\"",
            $"\"message\":\"{EscapeJson(message)}\""
        };

        if (context != null && context.Count > 0)
        {
            var contextJson = string.Join(",", context.Select(kvp => $"\"{kvp.Key}\":\"{EscapeJson(kvp.Value?.ToString() ?? "null")}\""));
            jsonParts.Add($"\"context\":{{{contextJson}}}");
        }

        return "{" + string.Join(",", jsonParts) + "}";
    }

    /// <summary>
    /// 转义 JSON 字符串
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    private static string EscapeJson(string text)
    {
        return text.Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}
