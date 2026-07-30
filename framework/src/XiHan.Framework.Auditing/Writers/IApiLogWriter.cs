// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Auditing.Writers;

/// <summary>
/// 接口日志写入器
/// </summary>
public interface IApiLogWriter
{
    /// <summary>
    /// 写入接口日志
    /// </summary>
    /// <param name="record">接口日志记录</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task WriteAsync(ApiLogRecord record, CancellationToken cancellationToken = default);
}
