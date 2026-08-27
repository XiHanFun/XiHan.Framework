// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace XiHan.Framework.Core.Tests.Fakes;

/// <summary>
/// 记录日志级别与渲染文本的手写日志替身
/// </summary>
/// <remarks>
/// 本仓测试栈只有 xunit.v3，不允许引入 mock 框架，替身一律手写。
/// <see cref="IsEnabled"/> 恒为 true，避免级别过滤把待断言的日志吞掉；
/// 只保留断言真正需要的「级别 + 渲染后文本 + 关联异常」三项，不去还原结构化参数。
/// </remarks>
public sealed class CoreRecordingLogger : ILogger
{
    private readonly List<CoreRecordedLogEntry> _entries = [];

    /// <summary>
    /// 按写入顺序保存的日志条目
    /// </summary>
    public IReadOnlyList<CoreRecordedLogEntry> Entries => _entries;

    /// <summary>
    /// 开始日志作用域，替身不需要作用域语义，始终返回 null
    /// </summary>
    /// <typeparam name="TState">作用域状态类型</typeparam>
    /// <param name="state">作用域状态</param>
    /// <returns>始终为 null</returns>
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return null;
    }

    /// <summary>
    /// 判断指定级别是否启用，替身对所有级别都启用
    /// </summary>
    /// <param name="logLevel">日志级别</param>
    /// <returns>始终为 true</returns>
    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    /// <summary>
    /// 记录一条日志
    /// </summary>
    /// <typeparam name="TState">日志状态类型</typeparam>
    /// <param name="logLevel">日志级别</param>
    /// <param name="eventId">事件标识</param>
    /// <param name="state">日志状态</param>
    /// <param name="exception">关联异常</param>
    /// <param name="formatter">文本渲染委托</param>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _entries.Add(new CoreRecordedLogEntry(logLevel, formatter(state, exception), exception));
    }
}

/// <summary>
/// 一条被记录下来的日志
/// </summary>
/// <param name="Level">实际写入的日志级别</param>
/// <param name="Message">渲染后的日志文本</param>
/// <param name="Exception">关联的异常，未关联时为空</param>
public sealed record CoreRecordedLogEntry(LogLevel Level, string Message, Exception? Exception);
