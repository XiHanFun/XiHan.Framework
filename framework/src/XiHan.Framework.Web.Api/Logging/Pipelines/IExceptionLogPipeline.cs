#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IExceptionLogPipeline
// Guid:21f8f95f-5606-4d28-8bfa-126f1603b877
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/08 22:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Api.Logging.Pipelines;

/// <summary>
/// 异常日志管道
/// </summary>
public interface IExceptionLogPipeline
{
    /// <summary>
    /// 写入异常日志
    /// </summary>
    /// <param name="record"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task WriteAsync(ExceptionLogRecord record, CancellationToken cancellationToken = default);
}
