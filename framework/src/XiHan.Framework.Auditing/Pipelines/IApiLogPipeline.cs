#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IApiLogPipeline
// Guid:f7081920-1234-5678-9012-cdef12345678
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/08 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Auditing.Pipelines;

/// <summary>
/// 接口日志管道
/// </summary>
public interface IApiLogPipeline
{
    /// <summary>
    /// 写入接口日志
    /// </summary>
    /// <param name="record"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task WriteAsync(ApiLogRecord record, CancellationToken cancellationToken = default);
}
