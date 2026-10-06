// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace XiHan.Framework.Web.Mcp.Tests;

/// <summary>
/// 手写的日志器替身，按写入顺序记下级别与格式化后的消息
/// </summary>
/// <typeparam name="TCategoryName">日志分类</typeparam>
internal sealed class RecordingLogger<TCategoryName> : ILogger<TCategoryName>
{
    /// <summary>
    /// 已记录的日志条目，按写入顺序排列
    /// </summary>
    public List<LogRecord> Records { get; } = [];

    /// <summary>
    /// 开始日志作用域
    /// </summary>
    /// <typeparam name="TState">状态类型</typeparam>
    /// <param name="state">状态</param>
    /// <returns>始终返回 null，测试不关心作用域</returns>
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return null;
    }

    /// <summary>
    /// 是否启用指定级别
    /// </summary>
    /// <param name="logLevel">日志级别</param>
    /// <returns>始终启用，避免被级别过滤掩盖调用</returns>
    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    /// <summary>
    /// 写入日志
    /// </summary>
    /// <typeparam name="TState">状态类型</typeparam>
    /// <param name="logLevel">日志级别</param>
    /// <param name="eventId">事件标识</param>
    /// <param name="state">状态</param>
    /// <param name="exception">异常</param>
    /// <param name="formatter">格式化委托</param>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Records.Add(new LogRecord(logLevel, formatter(state, exception)));
    }
}

/// <summary>
/// 单条日志记录
/// </summary>
/// <param name="Level">日志级别</param>
/// <param name="Message">格式化后的消息</param>
internal sealed record LogRecord(LogLevel Level, string Message);
