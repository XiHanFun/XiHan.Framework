// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace XiHan.Framework.Upgrade.Tests;

/// <summary>
/// 记录日志文本的日志器替身
/// </summary>
/// <remarks>
/// 升级模块中的若干「空实现」服务除了写一条日志之外没有其它可观察行为，
/// 用它把这条日志变成可断言的契约，避免退化成只判断不抛异常的冒烟测试。
/// </remarks>
/// <typeparam name="T">日志分类类型</typeparam>
public sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly List<string> _messages = [];

    /// <summary>
    /// 已记录的日志文本
    /// </summary>
    public IReadOnlyList<string> Messages
    {
        get
        {
            lock (_messages)
            {
                return _messages.ToArray();
            }
        }
    }

    /// <summary>
    /// 开始日志作用域（测试中不需要作用域语义）
    /// </summary>
    /// <typeparam name="TState">状态类型</typeparam>
    /// <param name="state">状态</param>
    /// <returns>始终返回 null</returns>
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return null;
    }

    /// <summary>
    /// 是否启用指定级别
    /// </summary>
    /// <param name="logLevel">日志级别</param>
    /// <returns>始终启用</returns>
    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    /// <summary>
    /// 写日志
    /// </summary>
    /// <typeparam name="TState">状态类型</typeparam>
    /// <param name="logLevel">日志级别</param>
    /// <param name="eventId">事件标识</param>
    /// <param name="state">状态</param>
    /// <param name="exception">异常</param>
    /// <param name="formatter">格式化器</param>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (_messages)
        {
            _messages.Add(formatter(state, exception));
        }
    }
}
