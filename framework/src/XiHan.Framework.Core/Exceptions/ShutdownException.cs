#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ShutdownException
// Guid:bacd3ec2-7edc-4e4c-a9ef-4960dd18508e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/26 21:20:20
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.Logging;

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
        LogHelper.Error(DefaultMessage);
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message"></param>
    public ShutdownException(string? message) : base(DefaultMessage + message)
    {
        LogHelper.Error(DefaultMessage + message);
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message"></param>
    /// <param name="innerException"></param>
    public ShutdownException(string? message, Exception? innerException) : base(DefaultMessage + message, innerException)
    {
        LogHelper.Error(DefaultMessage + message);
    }
}
