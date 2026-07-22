// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Auditing.Writers;

/// <summary>
/// 操作日志写入器
/// </summary>
public interface IOperationLogWriter
{
    /// <summary>
    /// 写入操作日志
    /// </summary>
    /// <param name="record">操作日志记录</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task WriteAsync(OperationLogRecord record, CancellationToken cancellationToken = default);
}
