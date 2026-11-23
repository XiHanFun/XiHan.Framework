// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Testing;

/// <summary>
/// 把日志收进内存的日志工厂
/// </summary>
/// <remarks>
/// 全部级别都启用。登记为 <see cref="ILoggerFactory"/>，或作为 <see cref="ILoggerProvider"/> 加进日志构建器之后，组件写的日志都到这里，测试按级别、类别、消息与异常断言。最多留 <see cref="Capacity"/> 条，满了丢掉最早的；可以在多个线程上同时写入与读取
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class LogCapture : ILoggerFactory, ILoggerProvider
{
    /// <summary>
    /// 缺省的条数上限
    /// </summary>
    public const int DefaultCapacity = 10_000;

    /// <summary>
    /// 保护 <see cref="_entries"/> 的锁
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// 收到的日志，按写入顺序
    /// </summary>
    private readonly Queue<LogCaptureEntry> _entries = new();

    /// <summary>
    /// 以缺省的条数上限创建
    /// </summary>
    public LogCapture()
        : this(DefaultCapacity)
    {
    }

    /// <summary>
    /// 以给定的条数上限创建
    /// </summary>
    /// <param name="capacity">最多留多少条，必须为正</param>
    public LogCapture(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        Capacity = capacity;
    }

    /// <summary>
    /// 条数上限
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// 收到的日志的快照，按写入顺序
    /// </summary>
    /// <remarks>
    /// 每次读取都是一份新的副本，读到之后再写入的日志不在里面
    /// </remarks>
    public IReadOnlyList<LogCaptureEntry> Entries
    {
        get
        {
            lock (_lock)
            {
                return [.. _entries];
            }
        }
    }

    /// <inheritdoc cref="ILoggerFactory.CreateLogger(string)" />
    public ILogger CreateLogger(string categoryName)
    {
        ArgumentNullException.ThrowIfNull(categoryName);
        return new CapturedLogger(this, categoryName);
    }

    /// <inheritdoc />
    /// <remarks>
    /// 不接受别的提供程序：日志只收在这里，不转发
    /// </remarks>
    public void AddProvider(ILoggerProvider provider) => throw new NotSupportedException("The log capture does not take providers.");

    /// <inheritdoc />
    /// <remarks>
    /// 没有要释放的资源：释放之后写入的日志照样记下，已经收到的仍可读取
    /// </remarks>
    public void Dispose()
    {
    }

    /// <summary>
    /// 记下一条
    /// </summary>
    private void Add(LogCaptureEntry entry)
    {
        lock (_lock)
        {
            // 先腾位置再放：条数任何时刻都不超过上限，丢的是最早的
            while (_entries.Count >= Capacity)
            {
                _entries.Dequeue();
            }

            _entries.Enqueue(entry);
        }
    }

    /// <summary>
    /// 把写入转成条目的日志器
    /// </summary>
    /// <param name="owner">收条目的工厂</param>
    /// <param name="category">类别</param>
    private sealed class CapturedLogger(LogCapture owner, string category) : ILogger
    {
        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            owner.Add(new LogCaptureEntry { Level = logLevel, Category = category, Message = formatter(state, exception), Exception = exception });
        }
    }
}
