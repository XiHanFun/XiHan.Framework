// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace XiHan.Framework.Authentication.Tests.OAuth.Infrastructure;

/// <summary>
/// 把日志文本收集起来供断言的日志提供程序
/// </summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _messages = new();

    /// <summary>
    /// 已捕获的日志文本
    /// </summary>
    public IReadOnlyCollection<string> Messages => _messages;

    /// <summary>
    /// 判断捕获到的日志里是否出现过指定文本
    /// </summary>
    /// <param name="text">要查找的文本</param>
    /// <returns>是否出现过</returns>
    public bool Contains(string text)
    {
        return _messages.Any(message => message.Contains(text, StringComparison.Ordinal));
    }

    /// <summary>
    /// 创建日志记录器
    /// </summary>
    /// <param name="categoryName">日志分类名</param>
    /// <returns>日志记录器</returns>
    public ILogger CreateLogger(string categoryName)
    {
        return new CapturingLogger(_messages);
    }

    /// <summary>
    /// 释放
    /// </summary>
    public void Dispose()
    {
    }

    private sealed class CapturingLogger : ILogger
    {
        private readonly ConcurrentQueue<string> _messages;

        public CapturingLogger(ConcurrentQueue<string> messages)
        {
            _messages = messages;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _messages.Enqueue(formatter(state, exception));
        }
    }
}
