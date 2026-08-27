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
        // 原来只做 logLevel >= MinLevel 的数值比较，而 LogLevel.None(6) 是枚举里的最大值，
        // 于是任何 MinLevel 下 IsEnabled(LogLevel.None) 都返回 true，Log(LogLevel.None, ...) 也会照常输出。
        // None 是「不写任何日志」的哨兵值（框架自带提供器一律 logLevel != LogLevel.None），必须先排除；
        // 顺带也让 MinLevel 被设成 None 时等价于「整个提供器关闭」。
        return logLevel != LogLevel.None && logLevel >= _options.MinLevel;
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

        // 原来无论什么级别都恒定写 Console.Out，XiHanConsoleLoggerOptions.UseStdErrorForErrors 全文没有读取点，
        // 宿主把它配成 true 也拿不到任何效果，是对宿主的空承诺。这里把它接进写入路径：开关打开时错误级及以上
        // 改走标准错误流，与 Microsoft 官方控制台提供器 LogToStandardErrorThreshold 的语义一致
        // （容器编排与 shell 管道普遍按 stdout/stderr 分流告警，这正是该开关存在的理由）。
        // 开关默认 false，因此默认输出目标与修复前完全一致，不影响既有宿主与既有用例。
        // 取 Console.Error / Console.Out 而不是缓存 TextWriter，是为了让宿主的 Console.SetOut/SetError 重定向随时生效。
        var writer = _options.UseStdErrorForErrors && logLevel >= LogLevel.Error
            ? Console.Error
            : Console.Out;

        lock (LockObj)
        {
            if (_options.EnableColors && _options.LogLevelColors.TryGetValue(logLevel, out var color))
            {
                var originalColor = Console.ForegroundColor;
                Console.ForegroundColor = color;

                WriteLogEntry(writer, logEntry);

                Console.ForegroundColor = originalColor;
            }
            else
            {
                WriteLogEntry(writer, logEntry);
            }
        }
    }

    /// <summary>
    /// 写入彩虹文本
    /// </summary>
    /// <param name="writer">输出流</param>
    /// <param name="text">文本</param>
    private static void WriteRainbowText(TextWriter writer, string text)
    {
        // 简化的彩虹输出实现
        for (var i = 0; i < text.Length; i++)
        {
            var hue = (double)i / text.Length * 360;
            var color = HsvToConsoleColor(hue, 1.0, 1.0);

            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            writer.Write(text[i]);
            Console.ForegroundColor = originalColor;
        }
        writer.WriteLine();
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
    /// <param name="writer">输出流，由 UseStdErrorForErrors 与日志级别共同决定</param>
    /// <param name="logEntry">日志条目</param>
    private void WriteLogEntry(TextWriter writer, string logEntry)
    {
        if (_options.EnableRainbow)
        {
            var lines = logEntry.Split(Environment.NewLine);
            foreach (var line in lines)
            {
                WriteRainbowText(writer, line);
            }
        }
        else
        {
            writer.WriteLine(logEntry);
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
