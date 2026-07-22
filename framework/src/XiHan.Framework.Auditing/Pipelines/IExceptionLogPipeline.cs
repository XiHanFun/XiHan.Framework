// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Auditing.Pipelines;

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
