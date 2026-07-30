// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Utils.Logging.Formatters;

/// <summary>
/// 日志格式化器接口
/// </summary>
public interface ILogFormatter
{
    /// <summary>
    /// 格式化日志消息
    /// </summary>
    /// <param name="timestamp">时间戳</param>
    /// <param name="level">日志级别</param>
    /// <param name="message">消息内容</param>
    /// <param name="context">上下文信息（可选）</param>
    /// <returns>格式化后的日志字符串</returns>
    string Format(DateTimeOffset timestamp, LogLevel level, string message, Dictionary<string, object>? context = null);
}
