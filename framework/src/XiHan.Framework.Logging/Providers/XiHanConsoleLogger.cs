#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanConsoleLogger
// Guid:f4a9b0c1-3d2e-4f4a-e1b8-9c0d1e2f4a6t
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/14 12:25:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using XiHan.Framework.Logging.Options;

namespace XiHan.Framework.Logging.Providers;

/// <summary>
/// XiHan 控制台日志器
/// </summary>
internal class XiHanConsoleLogger : ILogger
{
    private static readonly Lock LockObj = new();
    private readonly string _categoryName;
    private readonly XiHanConsoleLoggerOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="categoryName">分类名称</param>
    /// <param name="options">控制台日志选项</param>
    public XiHanConsoleLogger(string categoryName, XiHanConsoleLoggerOptions options)
    {
        _categoryName = categoryName;
        _options = options;
    }

    /// <summary>
    /// 开始日志作用域
    /// </summary>
    /// <typeparam name="TState">状态类型</typeparam>
    /// <param name="state">状态</param>
    /// <returns></returns>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null; // 简化实现
    }

    /// <summary>
    /// 检查是否启用指定日志级别
    /// </summary>
    /// <param name="logLevel">日志级别</param>
    /// <returns></returns>
    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= _options.MinLevel;
    }

    /// <summary>
    /// 记录日志
    /// </summary>
    /// <typeparam name="TState">状态类型</typeparam>
    /// <param name="logLevel">日志级别</param>
    /// <param name="eventId">事件唯一标识</param>
    /// <param name="state">状态</param>
    /// <param name="exception">异常</param>
    /// <param name="formatter">格式化器</param>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);

        lock (LockObj)
        {
            if (_options.EnableColors && _options.LogLevelColors.TryGetValue(logLevel, out var color))
            {
                var originalColor = Console.ForegroundColor;
                Console.ForegroundColor = color;

                WriteLogEntry(logLevel, message, exception);

                Console.ForegroundColor = originalColor;
            }
            else
            {
                WriteLogEntry(logLevel, message, exception);
            }
        }
    }

    /// <summary>
    /// 写入彩虹文本
    /// </summary>
    /// <param name="text">文本</param>
    private static void WriteRainbowText(string text)
    {
        // 简化的彩虹输出实现
        for (var i = 0; i < text.Length; i++)
        {
            var hue = (double)i / text.Length * 360;
            var color = HsvToConsoleColor(hue, 1.0, 1.0);

            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.Write(text[i]);
            Console.ForegroundColor = originalColor;
        }
        Console.WriteLine();
    }

    /// <summary>
    /// HSV 颜色转换为控制台颜色
    /// </summary>
    /// <param name="hue">色相</param>
    /// <param name="saturation">饱和度</param>
    /// <param name="value">明度</param>
    /// <returns></returns>
    private static ConsoleColor HsvToConsoleColor(double hue, double saturation, double value)
    {
        // 简化的 HSV 到控制台颜色的转换
        return hue switch
        {
            < 60 => ConsoleColor.Red,
            < 120 => ConsoleColor.Yellow,
            < 180 => ConsoleColor.Green,
            < 240 => ConsoleColor.Cyan,
            < 300 => ConsoleColor.Blue,
            _ => ConsoleColor.Magenta
        };
    }

    /// <summary>
    /// 写入日志条目
    /// </summary>
    /// <param name="logLevel">日志级别</param>
    /// <param name="message">消息</param>
    /// <param name="exception">异常</param>
    private void WriteLogEntry(LogLevel logLevel, string message, Exception? exception)
    {
        var timestamp = _options.ShowTimestamp ? DateTime.Now.ToString(_options.TimestampFormat) : string.Empty;
        var level = _options.ShowLogLevel ? $"[{logLevel}]" : string.Empty;
        var category = _options.ShowCategoryName ? _categoryName : string.Empty;

        var logEntry = string.Join(" ", new[] { timestamp, level, category, message }.Where(s => !string.IsNullOrEmpty(s)));

        if (_options.EnableRainbow)
        {
            WriteRainbowText(logEntry);
        }
        else
        {
            Console.WriteLine(logEntry);
        }

        if (exception != null)
        {
            Console.WriteLine(exception.ToString());
        }
    }
}
