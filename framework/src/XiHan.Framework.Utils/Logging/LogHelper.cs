#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LogHelper
// Guid:824ca05d-f5be-49a9-96f9-8a6502e5b064
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2022-05-30 上午 12:12:17
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text;

namespace XiHan.Framework.Utils.Logging;

/// <summary>
/// 简单的控制台日志输出类
/// </summary>
public static class LogHelper
{
    private static readonly Lock ObjLock = new();
    private static bool _isWriteToFile = false;
    private static bool _isDisplayHeader = true;

    /// <summary>
    /// 设置是否写入文件,默认不写入
    /// </summary>
    /// <param name="isWriteToFile">是否写入文件</param>
    public static void SetIsWriteToFile(bool isWriteToFile)
    {
        _isWriteToFile = isWriteToFile;
    }

    /// <summary>
    /// 设置是否显示日志头，默认显示
    /// </summary>
    /// <param name="isDisplayHeader">是否显示日志头</param>
    public static void SetIsDisplayHeader(bool isDisplayHeader)
    {
        _isDisplayHeader = isDisplayHeader;
    }

    /// <summary>
    /// 正常信息
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <param name="frontColor">前景色</param>
    public static void Info(string? message, ConsoleColor frontColor = ConsoleColor.White)
    {
        WriteColorLine(message, "INFO", frontColor);
        if (_isWriteToFile)
        {
            LogFileHelper.Info(message);
        }
    }

    /// <summary>
    /// 正常信息
    /// </summary>
    /// <param name="message">消息模板</param>
    /// <param name="args">格式化参数</param>
    public static void Info(string? message, params object[] args)
    {
        var formattedMessage = FormatMessage(message, args);
        Info(formattedMessage);
    }

    /// <summary>
    /// 成功信息
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <param name="frontColor">前景色</param>
    public static void Success(string? message, ConsoleColor frontColor = ConsoleColor.Green)
    {
        WriteColorLine(message, "SUCCESS", frontColor);
        if (_isWriteToFile)
        {
            LogFileHelper.Success(message);
        }
    }

    /// <summary>
    /// 成功信息
    /// </summary>
    /// <param name="message">消息模板</param>
    /// <param name="args">格式化参数</param>
    public static void Success(string? message, params object[] args)
    {
        var formattedMessage = FormatMessage(message, args);
        Success(formattedMessage);
    }

    /// <summary>
    /// 处理、查询信息
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <param name="frontColor">前景色</param>
    public static void Handle(string? message, ConsoleColor frontColor = ConsoleColor.Blue)
    {
        WriteColorLine(message, "HANDLE", frontColor);
        if (_isWriteToFile)
        {
            LogFileHelper.Handle(message);
        }
    }

    /// <summary>
    /// 处理、查询信息
    /// </summary>
    /// <param name="message">消息模板</param>
    /// <param name="args">格式化参数</param>
    public static void Handle(string? message, params object[] args)
    {
        var formattedMessage = FormatMessage(message, args);
        Handle(formattedMessage);
    }

    /// <summary>
    /// 警告、新增、更新信息
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <param name="frontColor">前景色</param>
    public static void Warn(string? message, ConsoleColor frontColor = ConsoleColor.Yellow)
    {
        WriteColorLine(message, "WARN", frontColor);
        if (_isWriteToFile)
        {
            LogFileHelper.Warn(message);
        }
    }

    /// <summary>
    /// 警告、新增、更新信息
    /// </summary>
    /// <param name="message">消息模板</param>
    /// <param name="args">格式化参数</param>
    public static void Warn(string? message, params object[] args)
    {
        var formattedMessage = FormatMessage(message, args);
        Warn(formattedMessage);
    }

    /// <summary>
    /// 错误、删除、危险、异常信息
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <param name="frontColor">前景色</param>
    public static void Error(string? message, ConsoleColor frontColor = ConsoleColor.Red)
    {
        WriteColorLine(message, "ERROR", frontColor);
        if (_isWriteToFile)
        {
            LogFileHelper.Error(message);
        }
    }

    /// <summary>
    /// 错误、删除、危险、异常信息
    /// </summary>
    /// <param name="message">消息模板</param>
    /// <param name="ex">异常</param>
    public static void Error(string? message, Exception ex)
    {
        var errorMessage = $"{message} {ex}";
        Error(errorMessage);
    }

    /// <summary>
    /// 错误、删除、危险、异常信息
    /// </summary>
    /// <param name="message">消息模板</param>
    /// <param name="args">格式化参数</param>
    public static void Error(string? message, params object[] args)
    {
        var formattedMessage = FormatMessage(message, args);
        Error(formattedMessage);
    }

    /// <summary>
    /// 渐变信息
    /// </summary>
    /// <remarks>
    /// 一般为展示项目信息(如LOGO)使用，不记录文件日志，不显示日志头
    /// </remarks>
    /// <param name="message">消息内容</param>
    public static void Rainbow(string? message)
    {
        WriteColorLineRainbow(message);
    }

    /// <summary>
    /// 渐变信息
    /// </summary>
    /// <remarks>
    /// 一般为展示项目信息(如LOGO)使用，不记录文件日志，不显示日志头
    /// </remarks>
    /// <param name="message">消息模板</param>
    /// <param name="args">格式化参数</param>
    public static void Rainbow(string? message, params object[] args)
    {
        var formattedMessage = FormatMessage(message, args);
        Rainbow(formattedMessage);
    }

    /// <summary>
    /// 清除控制台内容
    /// </summary>
    public static void Clear()
    {
        lock (ObjLock)
        {
            try
            {
                Console.Clear();
                if (_isWriteToFile)
                {
                    LogFileHelper.Clear();
                }
            }
            catch
            {
                // 忽略清除失败的异常，某些环境下可能不支持清除
            }
        }
    }

    #region 内部方法

    /// <summary>
    /// 格式化消息
    /// </summary>
    /// <param name="message">消息模板</param>
    /// <param name="args">格式化参数</param>
    /// <returns>格式化后的消息</returns>
    private static string? FormatMessage(string? message, params object[] args)
    {
        if (string.IsNullOrEmpty(message))
        {
            return message;
        }

        try
        {
            // 如果没有参数，直接返回原消息
            if (args == null || args.Length == 0)
            {
                return message;
            }

            // 使用 string.Format 进行格式化
            return string.Format(message, args);
        }
        catch (FormatException)
        {
            // 如果格式化失败，返回原消息并附加参数信息
            var argStr = string.Join(", ", args?.Select(arg => arg?.ToString() ?? "null") ?? []);
            return $"{message} [Args: {argStr}]";
        }
        catch
        {
            // 其他异常情况，返回原消息
            return message;
        }
    }

    /// <summary>
    /// 在控制台输出
    /// </summary>
    /// <param name="message">打印文本</param>
    /// <param name="logType">日志类型</param>
    /// <param name="frontColor">前置颜色</param>
    private static void WriteColorLine(string? message, string logType, ConsoleColor frontColor)
    {
        // 格式化日志内容
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logLine = _isDisplayHeader ? $"[{timestamp} {logType}] {message}" : message;

        lock (ObjLock)
        {
            var currentForeColor = Console.ForegroundColor;
            Console.ForegroundColor = frontColor;
            Console.WriteLine(logLine);
            Console.ForegroundColor = currentForeColor;
        }
    }

    /// <summary>
    /// 打印彩虹渐变文本（支持单行和多行）
    /// </summary>
    /// <param name="message">要打印的文本</param>
    /// <param name="addNewLine">是否在末尾添加换行符，默认为true</param>
    private static void WriteColorLineRainbow(string? message, bool addNewLine = true)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        lock (ObjLock)
        {
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

                // 为每个字符应用彩虹渐变
                for (var i = 0; i < line.Length; i++)
                {
                    var c = line[i];

                    // 只对非空格字符应用颜色
                    if (c != ' ')
                    {
                        var progress = (double)i / Math.Max(1, maxLength - 1);
                        var (r, g, b) = GetRainbowColor(progress);
                        SetConsoleColor(r, g, b);
                    }

                    Console.Write(c);
                }
                Console.ResetColor();

                // 如果不是最后一行，或者需要添加换行符，则换行
                if (lineIndex < lines.Length - 1 || addNewLine)
                {
                    Console.WriteLine();
                }
            }
        }
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
