#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanException
// Guid:c3257c2c-f925-47b1-9cac-d0814fb4293c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/11 6:58:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Exceptions;

/// <summary>
/// 曦寒特定异常
/// 为特定异常抛出的基本异常类型
/// </summary>
public class XiHanException : Exception
{
    private const string DefaultMessage = "曦寒特定异常。";

    /// <summary>
    /// 构造函数
    /// </summary>
    public XiHanException() : base(DefaultMessage)
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message"></param>
    public XiHanException(string? message) : base(DefaultMessage + message)
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message"></param>
    /// <param name="innerException"></param>
    public XiHanException(string? message, Exception? innerException) : base(DefaultMessage + message, innerException)
    {
    }
}