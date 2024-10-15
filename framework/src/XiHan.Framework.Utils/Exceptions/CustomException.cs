#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2023 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CustomException
// Guid:661fd4f6-f39d-4e1c-8132-43b39be4a6ce
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2023-06-25 上午 10:16:41
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.Utils.Exceptions;

/// <summary>
/// 自定义异常
/// </summary>
/// <remarks>
/// 优先级低于 <see cref="XiHanException"></see>
/// </remarks>
public class CustomException : Exception
{
    private const string DefaultMessage = "服务器端程序自定义异常。";

    /// <summary>
    /// 构造函数
    /// </summary>
    public CustomException() : base(DefaultMessage)
    {
        DefaultMessage.WriteLineError();
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message"></param>
    public CustomException(string? message) : base(DefaultMessage + message)
    {
        (DefaultMessage + message).WriteLineError();
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message"></param>
    /// <param name="exception"></param>
    public CustomException(string? message, Exception? exception) : base(DefaultMessage + message, exception)
    {
        (DefaultMessage + message).WriteLineError();
    }

    /// <summary>
    /// 抛出异常
    /// </summary>
    public static void Throw()
    {
        throw new CustomException();
    }

    /// <summary>
    /// 抛出异常
    /// </summary>
    public static void Throw(string? message)
    {
        throw new CustomException(message);
    }

    /// <summary>
    /// 抛出异常
    /// </summary>
    public static void Throw(string? message, Exception? exception)
    {
        throw new CustomException(message, exception);
    }
}