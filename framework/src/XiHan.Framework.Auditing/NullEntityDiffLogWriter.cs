// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Auditing;

/// <summary>
/// 空实体差异日志写入器
/// </summary>
public class NullEntityDiffLogWriter : IEntityDiffLogWriter
{
    /// <summary>
    /// 写入实体差异日志，直接丢弃不做任何处理
    /// </summary>
    /// <param name="record">实体差异日志记录</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task WriteAsync(EntityDiffLogRecord record, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
