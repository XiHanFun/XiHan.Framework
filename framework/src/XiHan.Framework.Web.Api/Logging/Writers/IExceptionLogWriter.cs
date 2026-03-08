#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IExceptionLogWriter
// Guid:3a706af8-ff70-4328-b55d-75a98648be8b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/12 16:22:50
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Api.Logging;

/// <summary>
/// 异常日志写入器
/// </summary>
public interface IExceptionLogWriter
{
    /// <summary>
    /// 写入异常日志
    /// </summary>
    /// <param name="record">异常日志记录</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task WriteAsync(ExceptionLogRecord record, CancellationToken cancellationToken = default);
}
