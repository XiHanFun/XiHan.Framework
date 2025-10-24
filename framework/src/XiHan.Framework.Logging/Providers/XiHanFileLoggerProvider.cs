#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanFileLoggerProvider
// Guid:e3f8a9b0-2c1d-4e3f-d0a7-8b9c0d1e3f5s
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/14 12:20:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XiHan.Framework.Logging.Options;

namespace XiHan.Framework.Logging.Providers;

/// <summary>
/// XiHan 文件日志提供器
/// </summary>
[ProviderAlias("XiHanFile")]
public class XiHanFileLoggerProvider : ILoggerProvider
{
    private readonly XiHanFileLoggerOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">文件日志选项</param>
    public XiHanFileLoggerProvider(IOptions<XiHanFileLoggerOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// 创建日志器
    /// </summary>
    /// <param name="categoryName">分类名称</param>
    /// <returns></returns>
    public ILogger CreateLogger(string categoryName)
    {
        return new XiHanFileLogger(categoryName, _options);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        // 清理资源
    }
}

/// <summary>
/// XiHan 文件日志器
/// </summary>
internal class XiHanFileLogger : ILogger
{
    private readonly string _categoryName;
    private readonly XiHanFileLoggerOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="categoryName">分类名称</param>
    /// <param name="options">文件日志选项</param>
    public XiHanFileLogger(string categoryName, XiHanFileLoggerOptions options)
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

        // 简化的文件写入实现
        var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{logLevel}] {_categoryName}: {message}";

        if (exception != null)
        {
            logEntry += Environment.NewLine + exception.ToString();
        }

        // 这里应该实现异步文件写入，简化起见使用同步写入
        try
        {
            var directory = Path.GetDirectoryName(_options.FilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(_options.FilePath, logEntry + Environment.NewLine);
        }
        catch
        {
            // 忽略文件写入错误
        }
    }
}
