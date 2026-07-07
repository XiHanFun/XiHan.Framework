#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ILoginLogPipeline
// Guid:02ac02fe-1a6d-4ad6-afa2-b580a31ee8ae
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/05/03 23:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Auditing.Pipelines;

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
