#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IOperationLogWriter
// Guid:e19436db-59cc-4128-ba97-277eb33a491f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/12 16:22:40
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Api.Logging;

/// <summary>
/// 操作日志写入器
/// </summary>
public interface IOperationLogWriter
{
    /// <summary>
    /// 写入操作日志
    /// </summary>
    /// <param name="record">操作日志记录</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task WriteAsync(OperationLogRecord record, CancellationToken cancellationToken = default);
}
