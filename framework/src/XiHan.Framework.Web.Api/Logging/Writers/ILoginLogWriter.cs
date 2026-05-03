#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ILoginLogWriter
// Guid:b8d3e629-4e1f-4c7b-af96-2g4c7b9d3e5f
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
