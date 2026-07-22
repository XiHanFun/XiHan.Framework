// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Auditing.Pipelines;

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
