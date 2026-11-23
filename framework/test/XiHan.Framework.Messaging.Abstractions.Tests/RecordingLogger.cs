// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace XiHan.Framework.Messaging.Abstractions.Tests;

/// <summary>
/// 把日志收进内存的日志器
/// </summary>
/// <remarks>
/// 全部级别都启用；记下级别、格式化后的消息与随日志带上的异常
/// </remarks>
internal sealed class RecordingLogger : ILogger
{
    /// <summary>
    /// 收到的日志
    /// </summary>
    private readonly ConcurrentQueue<(LogLevel Level, string Message, Exception? Exception)> _entries = new();

    /// <summary>
    /// 收到的日志（级别、格式化后的消息、异常），按写入顺序
    /// </summary>
    public IReadOnlyList<(LogLevel Level, string Message, Exception? Exception)> Entries => [.. _entries];

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        _entries.Enqueue((logLevel, formatter(state, exception), exception));
    }
}
