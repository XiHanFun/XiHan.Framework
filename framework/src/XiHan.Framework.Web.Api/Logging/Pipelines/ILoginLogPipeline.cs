#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ILoginLogPipeline
// Guid:d0f5g84b-6g3h-4e9d-ch18-4i6e9d1f5g7h
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/05/03 23:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Api.Logging.Pipelines;

/// <summary>
/// 登录日志管道
/// </summary>
public interface ILoginLogPipeline
{
    /// <summary>
    /// 写入登录日志
    /// </summary>
    /// <param name="record">登录日志记录</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task WriteAsync(LoginLogRecord record, CancellationToken cancellationToken = default);
}
