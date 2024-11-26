﻿#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ExceptionNotificationContext
// Guid:bfd0f066-1730-4c0e-a782-fb6bf0432201
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/4/23 1:03:26
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using XiHan.Framework.Core.Extensions.Exceptions;
using XiHan.Framework.Utils.System;

namespace XiHan.Framework.Core.Exceptions.Handling;

/// <summary>
/// 异常通知上下文
/// </summary>
public class ExceptionNotificationContext
{
    /// <summary>
    /// 异常
    /// </summary>
    [NotNull]
    public Exception Exception { get; }

    /// <summary>
    /// 日志级别
    /// </summary>
    public LogLevel LogLevel { get; }

    /// <summary>
    /// 是否已处理
    /// </summary>
    public bool Handled { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="exception"></param>
    /// <param name="logLevel"></param>
    /// <param name="handled"></param>
    public ExceptionNotificationContext([NotNull] Exception exception, LogLevel? logLevel = null, bool handled = true)
    {
        Exception = CheckHelper.NotNull(exception, nameof(exception));
        LogLevel = logLevel ?? exception.GetLogLevel();
        Handled = handled;
    }
}