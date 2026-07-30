// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Auditing.Writers;

/// <summary>
/// 空异常日志写入器
/// </summary>
public class NullExceptionLogWriter : IExceptionLogWriter
{
    /// <inheritdoc />
    public Task WriteAsync(ExceptionLogRecord record, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
