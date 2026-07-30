// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Auditing;

/// <summary>
/// 实体差异日志写入器
/// </summary>
public interface IEntityDiffLogWriter
{
    /// <summary>
    /// 写入差异日志
    /// </summary>
    /// <param name="record">审计记录</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task WriteAsync(EntityDiffLogRecord record, CancellationToken cancellationToken = default);
}
