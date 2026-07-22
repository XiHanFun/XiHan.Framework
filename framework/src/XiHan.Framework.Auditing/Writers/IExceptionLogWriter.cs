// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Auditing.Writers;

/// <summary>
/// 异常日志写入器
/// </summary>
public interface IExceptionLogWriter
{
    /// <summary>
    /// 写入异常日志
    /// </summary>
    /// <param name="record">异常日志记录</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task WriteAsync(ExceptionLogRecord record, CancellationToken cancellationToken = default);
}
