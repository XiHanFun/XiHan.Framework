// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Serilog.Core;
using Serilog.Events;

namespace XiHan.Framework.Logging.Tests.Fakes;

/// <summary>
/// 收集日志事件的 Serilog 接收器替身
/// </summary>
/// <remarks>
/// XiHanLoggerConfigurationBuilder 的最小级别、级别重写与扩充器都只在事件真正流经管道时才生效，
/// 挂一个内存接收器可以在不落盘、不打控制台的前提下拿到完整的 LogEvent 做断言。
/// </remarks>
internal sealed class CollectingSink : ILogEventSink
{
    private readonly Lock _lock = new();
    private readonly List<LogEvent> _events = [];

    /// <summary>
    /// 已收集的日志事件快照，按写入顺序排列
    /// </summary>
    public IReadOnlyList<LogEvent> Events
    {
        get
        {
            lock (_lock)
            {
                return [.. _events];
            }
        }
    }

    /// <summary>
    /// 接收日志事件
    /// </summary>
    /// <param name="logEvent">日志事件</param>
    public void Emit(LogEvent logEvent)
    {
        lock (_lock)
        {
            _events.Add(logEvent);
        }
    }
}
