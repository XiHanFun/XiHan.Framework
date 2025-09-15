#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:InitializationException
// Guid:ddcc658e-c5e3-4478-b6b4-7e8cbec828ff
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/26 21:15:43
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
