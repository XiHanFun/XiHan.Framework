#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanException
// Guid:c3257c2c-f925-47b1-9cac-d0814fb4293c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/11 6:58:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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

    /// <summary>
    /// 抛出异常
    /// </summary>
    public static void Throw()
    {
        throw new XiHanException();
    }

    /// <summary>
    /// 抛出异常
    /// </summary>
    public static void Throw(string? message)
    {
        throw new XiHanException(message);
    }

    /// <summary>
    /// 抛出异常
    /// </summary>
    public static void Throw(string? message, Exception? exception)
    {
        throw new XiHanException(message, exception);
    }
}
