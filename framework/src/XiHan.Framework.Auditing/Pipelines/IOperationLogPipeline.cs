// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Auditing.Pipelines;

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
