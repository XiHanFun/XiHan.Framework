#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IEntityAuditLogWriter
// Guid:685f589c-dac2-4e48-95f3-d6d938cc4f24
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/12 16:27:45
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Data.Auditing;

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
