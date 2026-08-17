// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace XiHan.Framework.Docs.Mcp.Tests;

/// <summary>
/// 把日志条目连同结构化字段一起收下来的 <see cref="ILogger{TCategoryName}"/>
/// </summary>
/// <typeparam name="T">日志类别</typeparam>
/// <remarks>
/// 断言渲染后的文本是不够的：那样把 <c>LogInformation($"命中 {count} 段")</c> 写成插值串也照样能过，
/// 而插值串在结构化日志后端里是一条查不了的整串。这里把 <c>state</c> 里的键值对原样收下，
/// 用例便能断言「{HitCount} 这个字段确实存在且等于 3」——插值串没有字段，那样写会直接变红。
/// </remarks>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    /// <summary>
    /// 已捕获的日志条目，按写入顺序
    /// </summary>
    public List<CapturedLogEntry> Entries { get; } = [];

    /// <summary>
    /// 开始一个日志作用域，本实现不需要
    /// </summary>
    /// <typeparam name="TState">状态类型</typeparam>
    /// <param name="state">状态</param>
    /// <returns>永远为 null</returns>
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return null;
    }

    /// <summary>
    /// 是否启用某个级别，测试里一律启用
    /// </summary>
    /// <param name="logLevel">日志级别</param>
    /// <returns>恒为 true</returns>
    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    /// <summary>
    /// 记录一条日志
    /// </summary>
    /// <typeparam name="TState">状态类型</typeparam>
    /// <param name="logLevel">日志级别</param>
    /// <param name="eventId">事件标识</param>
    /// <param name="state">结构化状态</param>
    /// <param name="exception">异常</param>
    /// <param name="formatter">渲染委托</param>
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        var values = state as IReadOnlyList<KeyValuePair<string, object?>> ?? [];

        Entries.Add(new CapturedLogEntry(
            logLevel,
            formatter(state, exception),
            values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            exception));
    }
}

/// <summary>
/// 一条被捕获的日志
/// </summary>
/// <param name="Level">级别</param>
/// <param name="Message">渲染后的文本</param>
/// <param name="Values">结构化字段，键为消息模板里的占位符名</param>
/// <param name="Exception">附带的异常</param>
internal sealed record CapturedLogEntry(
    LogLevel Level,
    string Message,
    IReadOnlyDictionary<string, object?> Values,
    Exception? Exception)
{
    /// <summary>
    /// 取某个结构化字段的值，不存在时返回 null
    /// </summary>
    /// <param name="name">字段名</param>
    /// <returns>字段值</returns>
    public object? Value(string name)
    {
        return Values.TryGetValue(name, out var value) ? value : null;
    }
}
