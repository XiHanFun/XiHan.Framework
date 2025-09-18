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

using System.Text;
using System.Text.RegularExpressions;

namespace XiHan.Framework.Utils.ConsoleTools;

/// <summary>
/// 控制台彩色输出工具
/// </summary>
public static class ConsoleColorWriter
{
    private static ConsoleColor? _lastForegroundColor;
    private static ConsoleColor? _lastBackgroundColor;

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
            SetConsoleColorOptimized(color, backgroundColor);

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
            RestoreOriginalColors(originalForegroundColor, originalBackgroundColor);
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
    public static void WriteWithHighlightMessage(string message, string[] keywords, ConsoleColor highlightColor = ConsoleColor.Yellow, bool ignoreCase = true, bool newLine = true)
    {
        if (string.IsNullOrEmpty(message))
        {
            if (newLine)
            {
                Console.WriteLine();
            }

            return;
        }

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

        try
        {
            // 过滤有效关键字并转义正则表达式特殊字符
            var validKeywords = keywords
                .Where(k => !string.IsNullOrEmpty(k))
                .Select(k => Regex.Escape(k))
                .ToArray();

            if (validKeywords.Length == 0)
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

            // 构建正则表达式模式
            var pattern = string.Join("|", validKeywords);
            var regexOptions = ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
            var regex = new Regex($"({pattern})", regexOptions);

            // 分割文本并批量输出
            var parts = regex.Split(message);
            var segments = new List<(string text, bool isHighlight)>();

            for (var i = 0; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i]))
                {
                    continue;
                }

                // 检查是否为关键字（奇数索引为匹配的关键字）
                var isKeyword = i % 2 == 1;
                segments.Add((parts[i], isKeyword));
            }

            // 批量输出，减少颜色切换次数
            WriteBatchSegments(segments, highlightColor, originalColor);

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
    /// 在控制台输出彩虹渐变文本
    /// </summary>
    /// <param name="message">要打印的文本</param>
    /// <param name="newLine">是否换行</param>
    public static void WriteColoredRainbowMessage(string? message, bool newLine = true)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        Console.OutputEncoding = Encoding.UTF8;

        var lines = message.Split('\n');

        // 找到最长的行来计算渐变
        var maxLength = 0;
        foreach (var line in lines)
        {
            if (line.Length > maxLength)
            {
                maxLength = line.Length;
            }
        }

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex];

            if (string.IsNullOrEmpty(line))
            {
                if (lineIndex < lines.Length - 1) // 不是最后一行才换行
                {
                    Console.WriteLine();
                }

                continue;
            }

            // 批量处理彩虹渐变，减少频繁的颜色切换
            var lastColorHash = -1;
            var colorBuffer = new StringBuilder();

            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                var currentColorHash = -1;

                // 只对非空格字符应用颜色
                if (c != ' ')
                {
                    var progress = (double)i / Math.Max(1, maxLength - 1);
                    var (r, g, b) = GetRainbowColor(progress);
                    currentColorHash = HashCode.Combine(r, g, b);

                    // 颜色变化时，输出缓冲区内容并切换颜色
                    if (currentColorHash != lastColorHash)
                    {
                        if (colorBuffer.Length > 0)
                        {
                            Console.Write(colorBuffer.ToString());
                            colorBuffer.Clear();
                        }
                        SetConsoleColor(r, g, b);
                        lastColorHash = currentColorHash;
                    }
                }
                else
                {
                    // 空格字符，如果颜色状态发生变化，先输出缓冲区
                    if (currentColorHash != lastColorHash && colorBuffer.Length > 0)
                    {
                        Console.Write(colorBuffer.ToString());
                        colorBuffer.Clear();
                    }
                    lastColorHash = currentColorHash;
                }

                colorBuffer.Append(c);
            }

            // 输出最后的缓冲区内容
            if (colorBuffer.Length > 0)
            {
                Console.Write(colorBuffer.ToString());
            }
            Console.ResetColor();

            // 如果不是最后一行，或者需要添加换行符，则换行
            if (lineIndex < lines.Length - 1 || newLine)
            {
                Console.WriteLine();
            }
        }
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
        WriteColoredMessage(message, ConsoleColor.White, newLine);
    }

    /// <summary>
    /// 写处理消息
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <param name="newLine">是否换行</param>
    public static void WriteHandle(string message, bool newLine = true)
    {
        WriteColoredMessage(message, ConsoleColor.Gray, newLine);
    }

    /// <summary>
    /// 写带时间戳的日志消息
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <param name="logLevel">日志级别</param>
    /// <param name="color">日志颜色</param>
    /// <param name="showTimestamp">是否显示时间戳</param>
    public static void WriteLog(string message, string logLevel, ConsoleColor color, bool showTimestamp = true)
    {
        if (string.IsNullOrEmpty(message)) return;

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logLine = showTimestamp ? $"[{timestamp} {logLevel.ToUpperInvariant()}]\n{message}" : message;
        
        WriteColoredMessage(logLine, color);
    }

    #region 内部方法

    /// <summary>
    /// 重置颜色缓存状态
    /// </summary>
    public static void ResetColorCache()
    {
        _lastForegroundColor = null;
        _lastBackgroundColor = null;
    }

    /// <summary>
    /// 优化的颜色设置方法，避免重复设置相同颜色
    /// </summary>
    /// <param name="foregroundColor">前景色</param>
    /// <param name="backgroundColor">背景色</param>
    private static void SetConsoleColorOptimized(ConsoleColor foregroundColor, ConsoleColor? backgroundColor = null)
    {
        if (_lastForegroundColor != foregroundColor)
        {
            Console.ForegroundColor = foregroundColor;
            _lastForegroundColor = foregroundColor;
        }

        if (backgroundColor.HasValue && _lastBackgroundColor != backgroundColor.Value)
        {
            Console.BackgroundColor = backgroundColor.Value;
            _lastBackgroundColor = backgroundColor.Value;
        }
    }

    /// <summary>
    /// 恢复原始颜色
    /// </summary>
    /// <param name="originalForegroundColor">原始前景色</param>
    /// <param name="originalBackgroundColor">原始背景色</param>
    private static void RestoreOriginalColors(ConsoleColor originalForegroundColor, ConsoleColor originalBackgroundColor)
    {
        if (_lastForegroundColor != originalForegroundColor)
        {
            Console.ForegroundColor = originalForegroundColor;
            _lastForegroundColor = originalForegroundColor;
        }

        if (_lastBackgroundColor != originalBackgroundColor)
        {
            Console.BackgroundColor = originalBackgroundColor;
            _lastBackgroundColor = originalBackgroundColor;
        }
    }

    /// <summary>
    /// 批量输出文本段，减少颜色切换次数
    /// </summary>
    /// <param name="segments">文本段列表</param>
    /// <param name="highlightColor">高亮颜色</param>
    /// <param name="normalColor">正常颜色</param>
    private static void WriteBatchSegments(List<(string text, bool isHighlight)> segments, ConsoleColor highlightColor, ConsoleColor normalColor)
    {
        var currentColor = normalColor;
        var textBuffer = new StringBuilder();

        foreach (var (text, isHighlight) in segments)
        {
            var targetColor = isHighlight ? highlightColor : normalColor;

            if (targetColor != currentColor)
            {
                // 输出缓冲区内容
                if (textBuffer.Length > 0)
                {
                    Console.Write(textBuffer.ToString());
                    textBuffer.Clear();
                }

                // 切换颜色
                SetConsoleColorOptimized(targetColor);
                currentColor = targetColor;
            }

            textBuffer.Append(text);
        }

        // 输出最后的缓冲区内容
        if (textBuffer.Length > 0)
        {
            Console.Write(textBuffer.ToString());
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
                   _ => false // 未知颜色返回false
               };
    }

    /// <summary>
    /// 获取彩虹渐变颜色
    /// </summary>
    /// <param name="progress">进度值 (0.0 - 1.0)</param>
    /// <returns>RGB颜色值</returns>
    private static (int r, int g, int b) GetRainbowColor(double progress)
    {
        progress = Math.Max(0, Math.Min(1, progress));
        var hue = progress * 300; // 0-300度，避免回到红色
        return HsvToRgb((int)hue, 1.0, 1.0);
    }

    /// <summary>
    /// HSV颜色空间转RGB
    /// </summary>
    /// <param name="hue">色相 (0-360)</param>
    /// <param name="saturation">饱和度 (0.0-1.0)</param>
    /// <param name="value">明度 (0.0-1.0)</param>
    /// <returns>RGB颜色值</returns>
    private static (int r, int g, int b) HsvToRgb(int hue, double saturation, double value)
    {
        var h = hue / 60.0;
        var c = value * saturation;
        var x = c * (1 - Math.Abs((h % 2) - 1));
        var m = value - c;

        double r, g, b;

        if (h is >= 0 and < 1) { r = c; g = x; b = 0; }
        else if (h is >= 1 and < 2) { r = x; g = c; b = 0; }
        else if (h is >= 2 and < 3) { r = 0; g = c; b = x; }
        else if (h is >= 3 and < 4) { r = 0; g = x; b = c; }
        else if (h is >= 4 and < 5) { r = x; g = 0; b = c; }
        else { r = c; g = 0; b = x; }

        return ((int)((r + m) * 255), (int)((g + m) * 255), (int)((b + m) * 255));
    }

    /// <summary>
    /// 设置控制台颜色 (ANSI)
    /// </summary>
    /// <param name="r">红色分量 (0-255)</param>
    /// <param name="g">绿色分量 (0-255)</param>
    /// <param name="b">蓝色分量 (0-255)</param>
    private static void SetConsoleColor(int r, int g, int b)
    {
        Console.Write($"\x1b[38;2;{r};{g};{b}m");
    }

    #endregion 内部方法
}
