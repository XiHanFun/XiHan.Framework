#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ExceptionExtensions
// Guid:bc13f023-00e7-4016-af09-44c7c910ccd8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/26 21:24:58
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using System.Runtime.ExceptionServices;
using XiHan.Framework.Core.Exceptions.Handling.Abstracts;

namespace XiHan.Framework.Core.Extensions.Exceptions;

/// <summary>
/// 异常扩展方法
/// </summary>
public static class ExceptionExtensions
{
    /// <summary>
    /// 使用<see cref="ExceptionDispatchInfo.Capture"/>方法以重新抛出异常，同时保留堆栈跟踪
    /// </summary>
    /// <param name="exception">异常将被重新抛出</param>
    public static void ReThrow(this Exception exception)
    {
        ExceptionDispatchInfo.Capture(exception).Throw();
    }

    /// <summary>
    /// 尝试从给定的<paramref name="exception"/>获取日志级别，如果它实现了<see cref="IHasLogLevel"/>接口
    /// 否则，返回<paramref name="defaultLevel"/>
    /// </summary>
    /// <param name="exception"></param>
    /// <param name="defaultLevel"></param>
    /// <returns></returns>
    public static LogLevel GetLogLevel(this Exception exception, LogLevel defaultLevel = LogLevel.Error)
    {
        return (exception as IHasLogLevel)?.LogLevel ?? defaultLevel;
    }
}