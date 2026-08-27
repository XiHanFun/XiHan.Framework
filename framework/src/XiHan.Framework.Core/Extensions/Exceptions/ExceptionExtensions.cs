// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using System.Runtime.ExceptionServices;
using XiHan.Framework.Core.Exceptions.Abstracts;

namespace XiHan.Framework.Core.Extensions.Exceptions;

/// <summary>
/// 异常扩展方法
/// </summary>
public static class ExceptionExtensions
{
    /// <summary>
    /// 获取异常的消息，包括内部异常
    /// </summary>
    /// <remarks>
    /// 该开关原名 <c>isHideStackTrace</c>、文档写作"是否隐藏异常规模信息"，但它从头到尾没碰过堆栈：
    /// 本方法只拼消息文本，开关控制的是"还要不要沿 InnerException 链继续往下拼"。
    /// 按原名调用的人会以为传 true 只是少打堆栈，实际却把内部异常这条真正的根因线索整条丢掉，
    /// 因此按实际语义改名，行为保持不变。
    /// </remarks>
    /// <param name="exception">异常</param>
    /// <param name="isHideInnerException">是否隐藏内部异常（为 true 时只返回最外层异常的消息，不再沿内部异常链展开）</param>
    /// <returns></returns>
    public static string FormatMessage(this Exception? exception, bool isHideInnerException = false)
    {
        if (exception is null)
        {
            return string.Empty;
        }

        var message = exception.Message;
        if (isHideInnerException)
        {
            return message;
        }

        if (exception.InnerException is not null)
        {
            message += " --> " + exception.InnerException.FormatMessage();
        }

        return message;
    }

    /// <summary>
    /// 尝试从给定的<paramref name="exception"/>获取日志级别，如果它实现了<see cref="IHasLogLevel"/>接口
    /// 否则，返回<paramref name="defaultLevel"/>
    /// </summary>
    /// <param name="exception">异常</param>
    /// <param name="defaultLevel">默认日志级别</param>
    /// <returns></returns>
    public static LogLevel GetLogLevel(this Exception exception, LogLevel defaultLevel = LogLevel.Error)
    {
        return (exception as IHasLogLevel)?.LogLevel ?? defaultLevel;
    }

    /// <summary>
    /// 使用<see cref="ExceptionDispatchInfo.Capture"/>方法以重新抛出异常，同时保留堆栈跟踪
    /// </summary>
    /// <param name="exception">异常将被重新抛出</param>
    public static void ReThrow(this Exception exception)
    {
        ExceptionDispatchInfo.Capture(exception).Throw();
    }

    /// <summary>
    /// 如果<paramref name="isThrow"/>为 true，则抛出异常
    /// </summary>
    /// <param name="exception">异常</param>
    /// <param name="isThrow">是否抛出异常</param>
    public static void ThrowIf(this Exception exception, bool isThrow)
    {
        if (isThrow)
        {
            throw exception;
        }
    }

    /// <summary>
    /// 如果<paramref name="isThrowFunc"/>返回 true，则抛出异常
    /// </summary>
    /// <param name="exception">异常</param>
    /// <param name="isThrowFunc">是否抛出异常</param>
    public static void ThrowIf(this Exception exception, Func<bool> isThrowFunc)
    {
        if (isThrowFunc())
        {
            throw exception;
        }
    }
}
