// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
