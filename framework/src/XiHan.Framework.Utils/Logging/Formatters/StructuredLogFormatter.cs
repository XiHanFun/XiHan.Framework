// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Utils.Logging.Formatters;

/// <summary>
/// 结构化格式化器
/// </summary>
public class StructuredLogFormatter : ILogFormatter
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
        var parts = new List<string>
        {
            $"time={timestamp:yyyy-MM-dd HH:mm:ss.fff}",
            $"level={level}",
            $"msg=\"{message}\""
        };

        if (context != null && context.Count > 0)
        {
            parts.AddRange(context.Select(kvp => $"{kvp.Key}=\"{kvp.Value}\""));
        }

        return string.Join(" ", parts);
    }
}
