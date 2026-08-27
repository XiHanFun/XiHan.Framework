// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace XiHan.Framework.Logging.Tests.Fakes;

/// <summary>
/// 单条日志记录快照
/// </summary>
/// <param name="Level">日志级别</param>
/// <param name="Message">格式化后的消息</param>
/// <param name="Exception">关联异常</param>
/// <param name="Properties">结构化属性，来自消息模板的具名占位符</param>
/// <param name="Scopes">写入时处于生效状态的作用域，由外向内排列</param>
internal sealed record RecordedLogEntry(
    LogLevel Level,
    string Message,
    Exception? Exception,
    IReadOnlyList<KeyValuePair<string, object?>> Properties,
    IReadOnlyList<object?> Scopes)
{
    /// <summary>
    /// 按名称取结构化属性值
    /// </summary>
    /// <param name="name">属性名</param>
    /// <returns>属性值；名称不存在时返回 null</returns>
    public object? GetProperty(string name)
    {
        foreach (var pair in Properties)
        {
            if (string.Equals(pair.Key, name, StringComparison.Ordinal))
            {
                return pair.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// 是否存在指定名称的结构化属性
    /// </summary>
    /// <param name="name">属性名</param>
    /// <returns>存在返回 true</returns>
    public bool HasProperty(string name)
    {
        foreach (var pair in Properties)
        {
            if (string.Equals(pair.Key, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// 手写的日志记录器替身
/// </summary>
/// <remarks>
/// 被测的 XiHanLogger / StructuredLogger / PerformanceLogger 都把「日志级别、消息模板、结构化属性、异常对象」
/// 一次性交给下游 ILogger，除此之外没有任何返回值可断言；因此需要一个能完整回放这四者的记录器，
/// 否则「参数被丢弃」与「参数被正确透传」在测试里无法区分。
/// </remarks>
internal sealed class RecordingLogger : ILogger
{
    private readonly LoggerExternalScopeProvider _scopeProvider = new();

    /// <summary>
    /// 已记录的日志条目，按写入顺序排列
    /// </summary>
    public List<RecordedLogEntry> Entries { get; } = [];

    /// <summary>
    /// 生效的最小级别，用于验证上游是否遵守级别过滤
    /// </summary>
    public LogLevel MinLevel { get; set; } = LogLevel.Trace;

    /// <summary>
    /// 开始日志作用域
    /// </summary>
    /// <typeparam name="TState">状态类型</typeparam>
    /// <param name="state">状态</param>
    /// <returns>作用域句柄</returns>
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return _scopeProvider.Push(state);
    }

    /// <summary>
    /// 是否启用指定级别
    /// </summary>
    /// <param name="logLevel">日志级别</param>
    /// <returns>启用返回 true</returns>
    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel != LogLevel.None && logLevel >= MinLevel;
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
        if (!IsEnabled(logLevel))
        {
            return;
        }

        List<KeyValuePair<string, object?>> properties = [];
        if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
        {
            properties.AddRange(pairs);
        }

        List<object?> scopes = [];
        _scopeProvider.ForEachScope((scope, target) => target.Add(scope), scopes);

        Entries.Add(new RecordedLogEntry(logLevel, formatter(state, exception), exception, properties, scopes));
    }
}

/// <summary>
/// 泛型日志记录器替身
/// </summary>
/// <typeparam name="TCategoryName">日志分类</typeparam>
internal sealed class RecordingLogger<TCategoryName> : ILogger<TCategoryName>
{
    /// <summary>
    /// 内部实际记录器
    /// </summary>
    public RecordingLogger Inner { get; } = new();

    /// <summary>
    /// 已记录的日志条目，按写入顺序排列
    /// </summary>
    public List<RecordedLogEntry> Entries => Inner.Entries;

    /// <summary>
    /// 生效的最小级别
    /// </summary>
    public LogLevel MinLevel
    {
        get => Inner.MinLevel;
        set => Inner.MinLevel = value;
    }

    /// <summary>
    /// 开始日志作用域
    /// </summary>
    /// <typeparam name="TState">状态类型</typeparam>
    /// <param name="state">状态</param>
    /// <returns>作用域句柄</returns>
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return Inner.BeginScope(state);
    }

    /// <summary>
    /// 是否启用指定级别
    /// </summary>
    /// <param name="logLevel">日志级别</param>
    /// <returns>启用返回 true</returns>
    public bool IsEnabled(LogLevel logLevel)
    {
        return Inner.IsEnabled(logLevel);
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
        Inner.Log(logLevel, eventId, state, exception, formatter);
    }
}
