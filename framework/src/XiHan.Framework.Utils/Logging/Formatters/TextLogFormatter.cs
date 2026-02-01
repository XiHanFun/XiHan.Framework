#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TextLogFormatter
// Guid:34256aca-2814-4b8c-8826-b837c41535ec
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/18 05:47:23
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
