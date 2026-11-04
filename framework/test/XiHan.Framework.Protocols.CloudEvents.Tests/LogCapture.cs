// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace XiHan.Framework.Protocols.CloudEvents.Tests;

/// <summary>
/// 把日志连同异常收进内存的日志工厂
/// </summary>
/// <remarks>
/// 全部级别都启用；登记为 <see cref="ILoggerFactory"/> 后宿主里的组件都写到这里
/// </remarks>
internal sealed class LogCapture : ILoggerFactory
{
    /// <summary>
    /// 收到的日志
    /// </summary>
    private readonly ConcurrentQueue<(LogLevel Level, string Category, string Message, Exception? Exception)> _entries = new();

    /// <summary>
    /// 收到的日志（级别、类别、格式化后的消息、异常），按写入顺序
    /// </summary>
    public IReadOnlyList<(LogLevel Level, string Category, string Message, Exception? Exception)> Entries => [.. _entries];

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new CapturedLogger(categoryName, _entries);

    /// <inheritdoc />
    public void AddProvider(ILoggerProvider provider) => throw new NotSupportedException("The log capture does not take providers.");

    /// <inheritdoc />
    public void Dispose()
    {
    }

    /// <summary>
    /// 一个类别的日志
    /// </summary>
    private sealed class CapturedLogger(string category, ConcurrentQueue<(LogLevel Level, string Category, string Message, Exception? Exception)> entries) : ILogger
    {
        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => entries.Enqueue((logLevel, category, formatter(state, exception), exception));
    }
}
