// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
