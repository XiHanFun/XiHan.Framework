// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Logging;

namespace XiHan.Framework.Utils.Exceptions;

/// <summary>
/// 自定义异常
/// </summary>
public class CustomException : Exception
{
    private const string DefaultMessage = "自定义异常。";

    /// <summary>
    /// 构造函数
    /// </summary>
    public CustomException() : base(DefaultMessage)
    {
        LogHelper.Error(DefaultMessage);
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message"></param>
    public CustomException(string? message) : base(DefaultMessage + message)
    {
        LogHelper.Error(DefaultMessage + message);
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message"></param>
    /// <param name="exception"></param>
    public CustomException(string? message, Exception? exception) : base(DefaultMessage + message, exception)
    {
        LogHelper.Error(DefaultMessage + message);
    }
}
