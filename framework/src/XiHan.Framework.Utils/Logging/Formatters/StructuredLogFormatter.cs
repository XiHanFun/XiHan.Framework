#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:StructuredLogFormatter
// Guid:428aca67-b9a8-460c-92f3-6e043662f405
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/18 05:48:19
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
