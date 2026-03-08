#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IAccessLogPipeline
// Guid:3a2fd451-80b3-4b14-94f7-2e45ad2aa9a7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/08 22:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Api.Logging.Pipelines;

/// <summary>
/// 访问日志管道
/// </summary>
public interface IAccessLogPipeline
{
    /// <summary>
    /// 写入访问日志
    /// </summary>
    /// <param name="record"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task WriteAsync(AccessLogRecord record, CancellationToken cancellationToken = default);
}
