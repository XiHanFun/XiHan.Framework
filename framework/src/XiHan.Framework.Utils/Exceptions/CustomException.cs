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
public class CustomException : Exception
{
    /// <summary>
    /// 提示信息
    /// </summary>
    public new string? Message { get; set; }

    /// <summary>
    /// 异常详情
    /// </summary>
    public new Exception? InnerException { get; set; }

    /// <summary>
    /// 构造函数
    /// </summary>
    public CustomException() : base("服务器端程序错误")
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message"></param>
    public CustomException(string? message) : base(message)
    {
        message?.WriteLineError();
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message"></param>
    /// <param name="innerException"></param>
    public CustomException(string? message, Exception? innerException) : base(message, innerException)
    {
        message?.WriteLineError();
        throw innerException!;
    }
}