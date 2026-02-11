#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanInitLogEntry
// Guid:ac3c5972-0eb0-457a-8f99-92f283f2b10c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/26 19:17:54
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;

namespace XiHan.Framework.Core.Logging;

/// <summary>
/// 曦寒初始化日志入口
/// </summary>
public class XiHanInitLogEntry
{
    /// <summary>
    /// 日志等级
    /// </summary>
    public LogLevel LogLevel { get; init; }

    /// <summary>
    /// 事件标识符
    /// </summary>
    public EventId EventId { get; init; }

    /// <summary>
    /// 状态
    /// </summary>
    public object State { get; init; } = null!;

    /// <summary>
    /// 异常
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// 格式化器
    /// </summary>
    public Func<object, Exception?, string> Formatter { get; init; } = null!;

    /// <summary>
    /// 异常消息
    /// </summary>
    public string Message => Formatter(State, Exception);
}
