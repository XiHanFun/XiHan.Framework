// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Auditing;

/// <summary>
/// 空实体差异日志写入器
/// </summary>
public class NullEntityDiffLogWriter : IEntityDiffLogWriter
{
    /// <inheritdoc />
    public Task WriteAsync(EntityDiffLogRecord record, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
