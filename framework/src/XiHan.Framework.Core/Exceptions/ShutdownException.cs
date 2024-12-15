﻿#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ShutdownException
// Guid:bacd3ec2-7edc-4e4c-a9ef-4960dd18508e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/26 21:20:20
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.System;

namespace XiHan.Framework.Core.Exceptions;

/// <summary>
/// 关闭过程异常
/// </summary>
public class ShutdownException : Exception
{
    private const string DefaultMessage = "程序关闭过程异常。";

    /// <summary>
    /// 构造函数
    /// </summary>
    public ShutdownException() : base(DefaultMessage)
    {
        ConsoleHelper.Danger(DefaultMessage);
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message"></param>
    public ShutdownException(string? message) : base(DefaultMessage + message)
    {
        ConsoleHelper.Danger(DefaultMessage + message);
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message"></param>
    /// <param name="innerException"></param>
    public ShutdownException(string? message, Exception? innerException) : base(DefaultMessage + message, innerException)
    {
        ConsoleHelper.Danger(DefaultMessage + message);
    }

    /// <summary>
    /// 抛出异常
    /// </summary>
    public static void Throw()
    {
        throw new ShutdownException();
    }

    /// <summary>
    /// 抛出异常
    /// </summary>
    public static void Throw(string? message)
    {
        throw new ShutdownException(message);
    }

    /// <summary>
    /// 抛出异常
    /// </summary>
    public static void Throw(string? message, Exception? exception)
    {
        throw new ShutdownException(message, exception);
    }
}
