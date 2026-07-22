// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Auditing.Writers;

/// <summary>
/// 访问日志写入器
/// </summary>
public interface IAccessLogWriter
{
    /// <summary>
    /// 写入访问日志
    /// </summary>
    /// <param name="record">访问日志记录</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task WriteAsync(AccessLogRecord record, CancellationToken cancellationToken = default);
}
