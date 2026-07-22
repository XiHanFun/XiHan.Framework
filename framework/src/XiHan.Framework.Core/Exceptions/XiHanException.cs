// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Logging;

namespace XiHan.Framework.Core.Exceptions;

/// <summary>
/// 曦寒框架异常，为特定异常抛出的基本异常类型
/// </summary>
public class XiHanException : Exception
{
    private const string DefaultMessage = "曦寒框架异常。";

    /// <summary>
    /// 构造函数
    /// </summary>
    public XiHanException() : base(DefaultMessage)
    {
        LogHelper.Error(DefaultMessage);
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message"></param>
    public XiHanException(string? message) : base(DefaultMessage + message)
    {
        LogHelper.Error(DefaultMessage + message);
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message"></param>
    /// <param name="exception"></param>
    public XiHanException(string? message, Exception? exception) : base(DefaultMessage + message, exception)
    {
        LogHelper.Error(DefaultMessage + message);
    }
}
