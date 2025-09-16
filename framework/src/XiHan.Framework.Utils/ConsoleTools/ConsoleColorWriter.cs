#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ConsoleColorWriter
// Guid:d9e0f234-c6e3-5f40-9d8c-a0fd84c18135
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/16 23:50:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text.RegularExpressions;

namespace XiHan.Framework.Utils.ConsoleTools;

/// <summary>
/// 控制台彩色输出工具
/// </summary>
public static class ConsoleColorWriter
{
    /// <summary>
    /// 写成功消息
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <param name="newLine">是否换行</param>
    public static void WriteSuccess(string message, bool newLine = true)
    {
        WriteColoredMessage(message, ConsoleColor.Green, newLine);
    }

    /// <summary>
    /// 写错误消息
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <param name="newLine">是否换行</param>
    public static void WriteError(string message, bool newLine = true)
    {
        WriteColoredMessage(message, ConsoleColor.Red, newLine);
    }

    /// <summary>
    /// 写警告消息
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <param name="newLine">是否换行</param>
    public static void WriteWarning(string message, bool newLine = true)
    {
        WriteColoredMessage(message, ConsoleColor.Yellow, newLine);
    }

    /// <summary>
    /// 写信息消息
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <param name="newLine">是否换行</param>
    public static void WriteInfo(string message, bool newLine = true)
    {
        WriteColoredMessage(message, ConsoleColor.Cyan, newLine);
    }

    /// <summary>
    /// 写调试消息
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <param name="newLine">是否换行</param>
    public static void WriteDebug(string message, bool newLine = true)
    {
        WriteColoredMessage(message, ConsoleColor.Gray, newLine);
    }

    /// <summary>
    /// 写带颜色的消息
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <param name="color">前景色</param>
    /// <param name="newLine">是否换行</param>
    /// <param name="backgroundColor">背景色</param>
    public static void WriteColoredMessage(string message, ConsoleColor color, bool newLine = true, ConsoleColor? backgroundColor = null)
    {
        var originalForegroundColor = Console.ForegroundColor;
        var originalBackgroundColor = Console.BackgroundColor;

        try
        {
            Console.ForegroundColor = color;
            if (backgroundColor.HasValue)
            {
                Console.BackgroundColor = backgroundColor.Value;
            }

            if (newLine)
            {
                Console.WriteLine(message);
            }
            else
            {
                Console.Write(message);
            }
        }
        finally
        {
            Console.ForegroundColor = originalForegroundColor;
            Console.BackgroundColor = originalBackgroundColor;
        }
    }

    /// <summary>
    /// 写多彩消息（支持内联颜色标记）
    /// 格式：{color:message} 例如：{red:错误}{green:成功}
    /// </summary>
    /// <param name="message">带颜色标记的消息</param>
    /// <param name="newLine">是否换行</param>
    public static void WriteMultiColorMessage(string message, bool newLine = true)
    {
        var pattern = @"\{(\w+):([^}]*)\}";
        var matches = Regex.Matches(message, pattern);

        if (matches.Count == 0)
        {
            // 没有颜色标记，直接输出
            if (newLine)
            {
                Console.WriteLine(message);
            }
            else
            {
                Console.Write(message);
            }
            return;
        }

        var lastIndex = 0;
        foreach (Match match in matches)
        {
            // 输出标记前的普通文本
            if (match.Index > lastIndex)
            {
                Console.Write(message[lastIndex..match.Index]);
            }

            // 解析颜色并输出带颜色的文本
            var colorName = match.Groups[1].Value.ToLowerInvariant();
            var text = match.Groups[2].Value;

            if (TryParseColor(colorName, out var color))
            {
                WriteColoredMessage(text, color, false);
            }
            else
            {
                Console.Write(text);
            }

            lastIndex = match.Index + match.Length;
        }

        // 输出最后的普通文本
        if (lastIndex < message.Length)
        {
            Console.Write(message[lastIndex..]);
        }

        if (newLine)
        {
            Console.WriteLine();
        }
    }

    /// <summary>
    /// 写带关键字高亮的消息
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <param name="keywords">要高亮的关键字</param>
    /// <param name="highlightColor">高亮颜色</param>
    /// <param name="ignoreCase">是否忽略大小写</param>
    /// <param name="newLine">是否换行</param>
    public static void WriteWithHighlight(string message, string[] keywords, ConsoleColor highlightColor = ConsoleColor.Yellow, bool ignoreCase = true, bool newLine = true)
    {
        if (keywords == null || keywords.Length == 0)
        {
            if (newLine)
            {
                Console.WriteLine(message);
            }
            else
            {
                Console.Write(message);
            }
            return;
        }

        var originalColor = Console.ForegroundColor;
        var segments = new List<(string text, bool isHighlight)>();
        var remainingText = message;

        // 查找并标记所有关键字
        foreach (var keyword in keywords.Where(k => !string.IsNullOrEmpty(k)))
        {
            var tempSegments = new List<(string text, bool isHighlight)>();

            foreach (var segment in segments.DefaultIfEmpty((text: remainingText, isHighlight: false)))
            {
                if (segment.isHighlight)
                {
                    tempSegments.Add(segment);
                    continue;
                }

                // 简化的字符串分割方法
                var parts = new List<string>();
                var text = segment.text;
                var keywordToFind = ignoreCase ? keyword.ToLower() : keyword;
                var textToSearch = ignoreCase ? text.ToLower() : text;

                var startIndex = 0;
                int foundIndex;

                while ((foundIndex = textToSearch.IndexOf(keywordToFind, startIndex)) >= 0)
                {
                    // 添加关键字前的文本
                    if (foundIndex > startIndex)
                    {
                        parts.Add(text[startIndex..foundIndex]);
                    }
                    else
                    {
                        parts.Add("");
                    }

                    startIndex = foundIndex + keyword.Length;
                }

                // 添加最后的文本
                if (startIndex < text.Length)
                {
                    parts.Add(text[startIndex..]);
                }
                else if (startIndex == text.Length && parts.Count > 0)
                {
                    parts.Add("");
                }

                // 如果没有找到关键字，添加整个文本
                if (parts.Count == 0)
                {
                    parts.Add(text);
                }

                var partsArray = parts.ToArray();

                for (var i = 0; i < partsArray.Length; i++)
                {
                    if (!string.IsNullOrEmpty(partsArray[i]))
                    {
                        tempSegments.Add((partsArray[i], false));
                    }

                    if (i < partsArray.Length - 1)
                    {
                        tempSegments.Add((keyword, true));
                    }
                }
            }

            segments = tempSegments;
            if (segments.Count == 0 && !string.IsNullOrEmpty(remainingText))
            {
                segments.Add((remainingText, false));
            }
            remainingText = "";
        }

        // 如果没有找到任何关键字，添加原始文本
        if (segments.Count == 0)
        {
            segments.Add((message, false));
        }

        // 输出分段文本
        try
        {
            foreach (var (text, isHighlight) in segments)
            {
                if (isHighlight)
                {
                    Console.ForegroundColor = highlightColor;
                    Console.Write(text);
                    Console.ForegroundColor = originalColor;
                }
                else
                {
                    Console.Write(text);
                }
            }

            if (newLine)
            {
                Console.WriteLine();
            }
        }
        finally
        {
            Console.ForegroundColor = originalColor;
        }
    }

    /// <summary>
    /// 写日志级别消息
    /// </summary>
    /// <param name="level">日志级别</param>
    /// <param name="message">消息内容</param>
    /// <param name="timestamp">是否显示时间戳</param>
    public static void WriteLog(LogLevel level, string message, bool timestamp = true)
    {
        var prefix = timestamp ? $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] " : "";

        var (levelText, color) = level switch
        {
            LogLevel.Trace => ("TRACE", ConsoleColor.DarkGray),
            LogLevel.Debug => ("DEBUG", ConsoleColor.Gray),
            LogLevel.Information => ("INFO", ConsoleColor.White),
            LogLevel.Warning => ("WARN", ConsoleColor.Yellow),
            LogLevel.Error => ("ERROR", ConsoleColor.Red),
            LogLevel.Critical => ("FATAL", ConsoleColor.Magenta),
            _ => ("INFO", ConsoleColor.White)
        };

        Console.Write(prefix);
        WriteColoredMessage($"[{levelText}]", color, false);
        Console.WriteLine($" {message}");
    }

    /// <summary>
    /// 写带标签的消息
    /// </summary>
    /// <param name="tag">标签</param>
    /// <param name="message">消息</param>
    /// <param name="tagColor">标签颜色</param>
    /// <param name="messageColor">消息颜色</param>
    public static void WriteTaggedMessage(string tag, string message, ConsoleColor tagColor = ConsoleColor.Blue, ConsoleColor? messageColor = null)
    {
        WriteColoredMessage($"[{tag}]", tagColor, false);
        Console.Write(" ");

        if (messageColor.HasValue)
        {
            WriteColoredMessage(message, messageColor.Value);
        }
        else
        {
            Console.WriteLine(message);
        }
    }

    /// <summary>
    /// 解析颜色名称
    /// </summary>
    /// <param name="colorName">颜色名称</param>
    /// <param name="color">解析出的颜色</param>
    /// <returns>是否解析成功</returns>
    private static bool TryParseColor(string colorName, out ConsoleColor color)
    {
        return Enum.TryParse(colorName, true, out color) ||
               colorName switch
               {
                   "red" => (color = ConsoleColor.Red) == ConsoleColor.Red,
                   "green" => (color = ConsoleColor.Green) == ConsoleColor.Green,
                   "blue" => (color = ConsoleColor.Blue) == ConsoleColor.Blue,
                   "yellow" => (color = ConsoleColor.Yellow) == ConsoleColor.Yellow,
                   "cyan" => (color = ConsoleColor.Cyan) == ConsoleColor.Cyan,
                   "magenta" => (color = ConsoleColor.Magenta) == ConsoleColor.Magenta,
                   "white" => (color = ConsoleColor.White) == ConsoleColor.White,
                   "black" => (color = ConsoleColor.Black) == ConsoleColor.Black,
                   "gray" => (color = ConsoleColor.Gray) == ConsoleColor.Gray,
                   "grey" => (color = ConsoleColor.Gray) == ConsoleColor.Gray,
                   _ => (color = ConsoleColor.White) == ConsoleColor.Black // 永远为false
               };
    }

    /// <summary>
    /// 日志级别枚举
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        ///
        /// </summary>
        Trace,

        /// <summary>
        ///
        /// </summary>
        Debug,

        /// <summary>
        ///
        /// </summary>
        Information,

        /// <summary>
        ///
        /// </summary>
        Warning,

        /// <summary>
        ///
        /// </summary>
        Error,

        /// <summary>
        ///
        /// </summary>
        Critical
    }
}

/// <summary>
/// 彩色文本构建器
/// </summary>
public class ColoredTextBuilder
{
    private readonly List<(string text, ConsoleColor? foreground, ConsoleColor? background)> _segments = [];

    /// <summary>
    /// 添加文本段
    /// </summary>
    /// <param name="text">文本</param>
    /// <param name="foreground">前景色</param>
    /// <param name="background">背景色</param>
    /// <returns>构建器实例</returns>
    public ColoredTextBuilder Add(string text, ConsoleColor? foreground = null, ConsoleColor? background = null)
    {
        _segments.Add((text, foreground, background));
        return this;
    }

    /// <summary>
    /// 添加成功文本
    /// </summary>
    /// <param name="text">文本</param>
    /// <returns>构建器实例</returns>
    public ColoredTextBuilder Success(string text) => Add(text, ConsoleColor.Green);

    /// <summary>
    /// 添加错误文本
    /// </summary>
    /// <param name="text">文本</param>
    /// <returns>构建器实例</returns>
    public ColoredTextBuilder Error(string text) => Add(text, ConsoleColor.Red);

    /// <summary>
    /// 添加警告文本
    /// </summary>
    /// <param name="text">文本</param>
    /// <returns>构建器实例</returns>
    public ColoredTextBuilder Warning(string text) => Add(text, ConsoleColor.Yellow);

    /// <summary>
    /// 添加信息文本
    /// </summary>
    /// <param name="text">文本</param>
    /// <returns>构建器实例</returns>
    public ColoredTextBuilder Info(string text) => Add(text, ConsoleColor.Cyan);

    /// <summary>
    /// 输出所有文本段
    /// </summary>
    /// <param name="newLine">是否换行</param>
    public void Write(bool newLine = true)
    {
        var originalForeground = Console.ForegroundColor;
        var originalBackground = Console.BackgroundColor;

        try
        {
            foreach (var (text, foreground, background) in _segments)
            {
                if (foreground.HasValue)
                {
                    Console.ForegroundColor = foreground.Value;
                }

                if (background.HasValue)
                {
                    Console.BackgroundColor = background.Value;
                }

                Console.Write(text);

                if (foreground.HasValue)
                {
                    Console.ForegroundColor = originalForeground;
                }

                if (background.HasValue)
                {
                    Console.BackgroundColor = originalBackground;
                }
            }

            if (newLine)
            {
                Console.WriteLine();
            }
        }
        finally
        {
            Console.ForegroundColor = originalForeground;
            Console.BackgroundColor = originalBackground;
        }
    }

    /// <summary>
    /// 清空所有文本段
    /// </summary>
    /// <returns>构建器实例</returns>
    public ColoredTextBuilder Clear()
    {
        _segments.Clear();
        return this;
    }
}
