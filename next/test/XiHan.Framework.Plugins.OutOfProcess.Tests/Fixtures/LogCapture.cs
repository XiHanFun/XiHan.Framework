// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests.Fixtures;

/// <summary>
/// 把日志收进内存的日志提供程序
/// </summary>
internal sealed class LogCapture : ILoggerProvider
{
    /// <summary>
    /// 收到的日志
    /// </summary>
    private readonly ConcurrentQueue<(LogLevel Level, string Category, string Message)> _entries = new();

    /// <summary>
    /// 收到的日志（级别、类别、格式化后的消息）
    /// </summary>
    public IReadOnlyList<(LogLevel Level, string Category, string Message)> Entries => _entries.ToList();

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new CapturedLogger(categoryName, _entries);

    /// <inheritdoc />
    public void Dispose()
    {
    }

    /// <summary>
    /// 一个类别的日志
    /// </summary>
    private sealed class CapturedLogger(string category, ConcurrentQueue<(LogLevel Level, string Category, string Message)> entries) : ILogger
    {
        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => entries.Enqueue((logLevel, category, formatter(state, exception)));
    }
}
