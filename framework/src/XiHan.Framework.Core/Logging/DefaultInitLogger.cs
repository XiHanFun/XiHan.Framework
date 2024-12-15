#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DefaultInitLogger
// Guid:cc4b2cc3-48df-4558-88d0-d07d522613e0
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/27 2:12:49
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using XiHan.Framework.Utils.System;

namespace XiHan.Framework.Core.Logging;

/// <summary>
/// 默认初始化日志
/// </summary>
public class DefaultInitLogger<T> : IInitLogger<T>
{
    /// <summary>
    /// 日志记录器
    /// </summary>
    public List<XiHanInitLogEntry> Entries { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    public DefaultInitLogger()
    {
        Entries = [];
    }

    /// <summary>
    /// 记录日志
    /// </summary>
    /// <typeparam name="TState"></typeparam>
    /// <param name="logLevel"></param>
    /// <param name="eventId"></param>
    /// <param name="state"></param>
    /// <param name="exception"></param>
    /// <param name="formatter"></param>
    public virtual void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Entries.Add(new XiHanInitLogEntry
        {
            LogLevel = logLevel,
            EventId = eventId,
            State = state!,
            Exception = exception,
            Formatter = (s, e) => formatter((TState)s, e)
        });
    }

    /// <summary>
    /// 是否启用
    /// </summary>
    /// <param name="logLevel"></param>
    /// <returns></returns>
    public virtual bool IsEnabled(LogLevel logLevel)
    {
        return logLevel != LogLevel.None;
    }

    /// <summary>
    /// 开始范围
    /// </summary>
    /// <typeparam name="TState"></typeparam>
    /// <param name="state"></param>
    /// <returns></returns>
    public virtual IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        return NullDisposable.Instance;
    }
}
