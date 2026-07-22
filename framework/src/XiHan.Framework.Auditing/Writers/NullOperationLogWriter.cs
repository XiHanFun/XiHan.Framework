// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Auditing.Writers;

/// <summary>
/// 空操作日志写入器
/// </summary>
public class NullOperationLogWriter : IOperationLogWriter
{
    /// <inheritdoc />
    public Task WriteAsync(OperationLogRecord record, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
