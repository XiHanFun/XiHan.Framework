// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Auditing.Writers;

/// <summary>
/// 空异常日志写入器
/// </summary>
public class NullExceptionLogWriter : IExceptionLogWriter
{
    /// <summary>
    /// 写入异常日志，直接丢弃不做任何处理
    /// </summary>
    /// <param name="record">异常日志记录</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task WriteAsync(ExceptionLogRecord record, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
