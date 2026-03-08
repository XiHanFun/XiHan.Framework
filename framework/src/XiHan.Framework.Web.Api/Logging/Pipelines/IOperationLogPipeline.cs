#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IOperationLogPipeline
// Guid:99d3a840-6b2b-4b6c-9e19-0f3c0b5c3171
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/08 22:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Api.Logging.Pipelines;

/// <summary>
/// 操作日志管道
/// </summary>
public interface IOperationLogPipeline
{
    /// <summary>
    /// 写入操作日志
    /// </summary>
    /// <param name="record"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task WriteAsync(OperationLogRecord record, CancellationToken cancellationToken = default);
}
