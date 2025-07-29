#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanLogger
// Guid:f2a7b8c9-1d0e-4f2a-d9b6-7c8d9e0f2a4h
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/14 11:20:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using XiHan.Framework.Logging.Options;

namespace XiHan.Framework.Logging.Services;

/// <summary>
/// XiHan 日志器实现
/// </summary>
public class XiHanLogger : IXiHanLogger
{
    private readonly ILogger<XiHanLogger> _logger;
    private readonly XiHanLoggingOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志器</param>
    /// <param name="options">日志配置选项</param>
    public XiHanLogger(ILogger<XiHanLogger> logger, Microsoft.Extensions.Options.IOptions<XiHanLoggingOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// 记录跟踪日志
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="args">参数</param>
    public void LogTrace(string message, params object[] args)
    {
        if (!_options.IsEnabled)
        {
            return;
        }

        _logger.LogTrace(message, args);
    }

    /// <summary>
    /// 记录调试日志
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="args">参数</param>
    public void LogDebug(string message, params object[] args)
    {
        if (!_options.IsEnabled)
        {
            return;
        }

        _logger.LogDebug(message, args);
    }

    /// <summary>
    /// 记录信息日志
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="args">参数</param>
    public void LogInformation(string message, params object[] args)
    {
        if (!_options.IsEnabled)
        {
            return;
        }

        _logger.LogInformation(message, args);
    }

    /// <summary>
    /// 记录警告日志
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="args">参数</param>
    public void LogWarning(string message, params object[] args)
    {
        if (!_options.IsEnabled)
        {
            return;
        }

        _logger.LogWarning(message, args);
    }

    /// <summary>
    /// 记录错误日志
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="args">参数</param>
    public void LogError(string message, params object[] args)
    {
        if (!_options.IsEnabled)
        {
            return;
        }

        _logger.LogError(message, args);
    }

    /// <summary>
    /// 记录错误日志
    /// </summary>
    /// <param name="exception">异常</param>
    /// <param name="message">消息</param>
    /// <param name="args">参数</param>
    public void LogError(Exception exception, string message, params object[] args)
    {
        if (!_options.IsEnabled)
        {
            return;
        }

        _logger.LogError(exception, message, args);
    }

    /// <summary>
    /// 记录严重错误日志
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="args">参数</param>
    public void LogCritical(string message, params object[] args)
    {
        if (!_options.IsEnabled)
        {
            return;
        }

        _logger.LogCritical(message, args);
    }

    /// <summary>
    /// 记录严重错误日志
    /// </summary>
    /// <param name="exception">异常</param>
    /// <param name="message">消息</param>
    /// <param name="args">参数</param>
    public void LogCritical(Exception exception, string message, params object[] args)
    {
        if (!_options.IsEnabled)
        {
            return;
        }

        _logger.LogCritical(exception, message, args);
    }

    /// <summary>
    /// 记录结构化日志
    /// </summary>
    /// <param name="level">日志级别</param>
    /// <param name="message">消息</param>
    /// <param name="properties">属性</param>
    public void LogStructured(LogLevel level, string message, object properties)
    {
        if (!_options.IsEnabled || !_options.EnableStructuredLogging)
        {
            return;
        }

        using (Serilog.Context.LogContext.PushProperty("StructuredData", properties, true))
        {
            _logger.Log(level, message);
        }
    }

    /// <summary>
    /// 记录性能日志
    /// </summary>
    /// <param name="operationName">操作名称</param>
    /// <param name="duration">持续时间</param>
    /// <param name="properties">附加属性</param>
    public void LogPerformance(string operationName, TimeSpan duration, object? properties = null)
    {
        if (!_options.IsEnabled || !_options.EnablePerformanceCounters)
        {
            return;
        }

        var performanceData = new
        {
            OperationName = operationName,
            Duration = duration.TotalMilliseconds,
            Properties = properties
        };

        using (Serilog.Context.LogContext.PushProperty("PerformanceData", performanceData, true))
        {
            _logger.LogInformation("Performance: {OperationName} completed in {Duration}ms", operationName, duration.TotalMilliseconds);
        }
    }

    /// <summary>
    /// 检查是否启用指定日志级别
    /// </summary>
    /// <param name="logLevel">日志级别</param>
    /// <returns></returns>
    public bool IsEnabled(LogLevel logLevel)
    {
        return _options.IsEnabled && _logger.IsEnabled(logLevel);
    }

    /// <summary>
    /// 开始日志作用域
    /// </summary>
    /// <typeparam name="TState">状态类型</typeparam>
    /// <param name="state">状态</param>
    /// <returns></returns>
    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        return _logger.BeginScope(state);
    }
}

/// <summary>
/// 泛型 XiHan 日志器实现
/// </summary>
/// <typeparam name="T">类型</typeparam>
public class XiHanLogger<T> : IXiHanLogger<T>
{
    private readonly ILogger<T> _logger;
    private readonly XiHanLoggingOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志器</param>
    /// <param name="options">日志配置选项</param>
    public XiHanLogger(ILogger<T> logger, Microsoft.Extensions.Options.IOptions<XiHanLoggingOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// 记录跟踪日志
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="args">参数</param>
    public void LogTrace(string message, params object[] args)
    {
        if (!_options.IsEnabled)
        {
            return;
        }

        _logger.LogTrace(message, args);
    }

    /// <summary>
    /// 记录调试日志
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="args">参数</param>
    public void LogDebug(string message, params object[] args)
    {
        if (!_options.IsEnabled)
        {
            return;
        }

        _logger.LogDebug(message, args);
    }

    /// <summary>
    /// 记录信息日志
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="args">参数</param>
    public void LogInformation(string message, params object[] args)
    {
        if (!_options.IsEnabled)
        {
            return;
        }

        _logger.LogInformation(message, args);
    }

    /// <summary>
    /// 记录警告日志
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="args">参数</param>
    public void LogWarning(string message, params object[] args)
    {
        if (!_options.IsEnabled)
        {
            return;
        }

        _logger.LogWarning(message, args);
    }

    /// <summary>
    /// 记录错误日志
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="args">参数</param>
    public void LogError(string message, params object[] args)
    {
        if (!_options.IsEnabled)
        {
            return;
        }

        _logger.LogError(message, args);
    }

    /// <summary>
    /// 记录错误日志
    /// </summary>
    /// <param name="exception">异常</param>
    /// <param name="message">消息</param>
    /// <param name="args">参数</param>
    public void LogError(Exception exception, string message, params object[] args)
    {
        if (!_options.IsEnabled)
        {
            return;
        }

        _logger.LogError(exception, message, args);
    }

    /// <summary>
    /// 记录严重错误日志
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="args">参数</param>
    public void LogCritical(string message, params object[] args)
    {
        if (!_options.IsEnabled)
        {
            return;
        }

        _logger.LogCritical(message, args);
    }

    /// <summary>
    /// 记录严重错误日志
    /// </summary>
    /// <param name="exception">异常</param>
    /// <param name="message">消息</param>
    /// <param name="args">参数</param>
    public void LogCritical(Exception exception, string message, params object[] args)
    {
        if (!_options.IsEnabled)
        {
            return;
        }

        _logger.LogCritical(exception, message, args);
    }

    /// <summary>
    /// 记录结构化日志
    /// </summary>
    /// <param name="level">日志级别</param>
    /// <param name="message">消息</param>
    /// <param name="properties">属性</param>
    public void LogStructured(LogLevel level, string message, object properties)
    {
        if (!_options.IsEnabled || !_options.EnableStructuredLogging)
        {
            return;
        }

        using (Serilog.Context.LogContext.PushProperty("StructuredData", properties, true))
        {
            _logger.Log(level, message);
        }
    }

    /// <summary>
    /// 记录性能日志
    /// </summary>
    /// <param name="operationName">操作名称</param>
    /// <param name="duration">持续时间</param>
    /// <param name="properties">附加属性</param>
    public void LogPerformance(string operationName, TimeSpan duration, object? properties = null)
    {
        if (!_options.IsEnabled || !_options.EnablePerformanceCounters)
        {
            return;
        }

        var performanceData = new
        {
            OperationName = operationName,
            Duration = duration.TotalMilliseconds,
            Properties = properties
        };

        using (Serilog.Context.LogContext.PushProperty("PerformanceData", performanceData, true))
        {
            _logger.LogInformation("Performance: {OperationName} completed in {Duration}ms", operationName, duration.TotalMilliseconds);
        }
    }

    /// <summary>
    /// 检查是否启用指定日志级别
    /// </summary>
    /// <param name="logLevel">日志级别</param>
    /// <returns></returns>
    public bool IsEnabled(LogLevel logLevel)
    {
        return _options.IsEnabled && _logger.IsEnabled(logLevel);
    }

    /// <summary>
    /// 开始日志作用域
    /// </summary>
    /// <typeparam name="TState">状态类型</typeparam>
    /// <param name="state">状态</param>
    /// <returns></returns>
    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        return _logger.BeginScope(state);
    }
}
