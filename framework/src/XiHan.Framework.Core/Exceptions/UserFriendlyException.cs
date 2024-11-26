#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:UserFriendlyException
// Guid:871472de-7ccb-421e-b2ff-1c64883d78be
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/26 21:21:47
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;

namespace XiHan.Framework.Core.Exceptions;

/// <summary>
/// 用户友好异常
/// </summary>
public class UserFriendlyException : BusinessException, IUserFriendlyException
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message"></param>
    /// <param name="code"></param>
    /// <param name="details"></param>
    /// <param name="innerException"></param>
    /// <param name="logLevel"></param>
    public UserFriendlyException(string message, string? code = null, string? details = null, Exception? innerException = null, LogLevel logLevel = LogLevel.Warning)
        : base(code, message, details, innerException, logLevel)
    {
        Details = details;
    }
}