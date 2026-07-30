// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Utils.Logging.Formatters;

/// <summary>
/// 文本格式化器（默认）
/// </summary>
public class TextLogFormatter : ILogFormatter
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
        return $"[{timestamp:yyyy-MM-dd HH:mm:ss.fff} {level.ToString().ToUpperInvariant()}] {message}";
    }
}
