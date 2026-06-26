#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ILoginLogWriter
// Guid:a5324b2d-3c1e-4930-ba02-c422bb887198
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/05/03 23:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Api.Logging.Writers;

/// <summary>
/// 登录日志写入器
/// </summary>
public interface ILoginLogWriter
{
    /// <summary>
    /// 写入登录日志
    /// </summary>
    /// <param name="record">登录日志记录</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task WriteAsync(LoginLogRecord record, CancellationToken cancellationToken = default);
}
