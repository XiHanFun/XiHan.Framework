// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
    private readonly IExternalScopeProvider _scopeProvider;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="categoryName">分类名称</param>
    /// <param name="options">控制台日志选项</param>
    /// <param name="scopeProvider">作用域提供器</param>
    public XiHanConsoleLogger(string categoryName, XiHanConsoleLoggerOptions options, IExternalScopeProvider scopeProvider)
    {
        _categoryName = categoryName;
        _options = options;
        _scopeProvider = scopeProvider;
    }

    /// <summary>
    /// 开始日志作用域
    /// </summary>
    /// <typeparam name="TState">状态类型</typeparam>
    /// <param name="state">状态</param>
    /// <returns></returns>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return _scopeProvider.Push(state);
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
        var scopeText = XiHanLogEntryFormatter.BuildScopeText(_scopeProvider, _options.IncludeScopes);
        var template = ApplyTimestampFormat(_options.LogFormat, _options.TimestampFormat);
        var logEntry = XiHanLogEntryFormatter.Format(
            template,
            DateTimeOffset.Now,
            logLevel,
            _categoryName,
            message,
            exception,
            scopeText,
            includeTimestamp: _options.ShowTimestamp,
            includeLogLevel: _options.ShowLogLevel,
            includeCategory: _options.ShowCategoryName,
            singleLine: _options.SingleLine);

        lock (LockObj)
        {
            if (_options.EnableColors && _options.LogLevelColors.TryGetValue(logLevel, out var color))
            {
                var originalColor = Console.ForegroundColor;
                Console.ForegroundColor = color;

                WriteLogEntry(logEntry);

                Console.ForegroundColor = originalColor;
            }
            else
            {
                WriteLogEntry(logEntry);
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
    /// <param name="logEntry">日志条目</param>
    private void WriteLogEntry(string logEntry)
    {
        if (_options.EnableRainbow)
        {
            var lines = logEntry.Split(Environment.NewLine);
            foreach (var line in lines)
            {
                WriteRainbowText(line);
            }
        }
        else
        {
            Console.WriteLine(logEntry);
        }
    }

    private static string ApplyTimestampFormat(string template, string timestampFormat)
    {
        if (template.Contains("{Timestamp:", StringComparison.Ordinal))
        {
            return template;
        }

        return template.Replace("{Timestamp}", $"{{Timestamp:{timestampFormat}}}", StringComparison.Ordinal);
    }
}
