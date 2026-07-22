// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Auditing.Writers;

/// <summary>
/// 空访问日志写入器
/// </summary>
public class NullAccessLogWriter : IAccessLogWriter
{
    /// <inheritdoc />
    public Task WriteAsync(AccessLogRecord record, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
