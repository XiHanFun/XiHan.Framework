// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Logging;

namespace XiHan.Framework.Core.Exceptions;

/// <summary>
/// 初始化异常
/// </summary>
public class InitializationException : Exception
{
    private const string DefaultMessage = "程序初始化异常。";

    /// <summary>
    /// 构造函数
    /// </summary>
    public InitializationException() : base(DefaultMessage)
    {
        LogHelper.Error(DefaultMessage);
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message"></param>
    public InitializationException(string? message) : base(DefaultMessage + message)
    {
        LogHelper.Error(DefaultMessage + Environment.NewLine + message);
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message"></param>
    /// <param name="innerException"></param>
    public InitializationException(string? message, Exception? innerException) : base(DefaultMessage + message, innerException)
    {
        LogHelper.Error(DefaultMessage + Environment.NewLine + message + Environment.NewLine + innerException?.StackTrace);
    }
}
