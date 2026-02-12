#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:NullEntityAuditLogWriter
// Guid:6484e4d4-bf14-4f10-9f99-2e0673fba826
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/12 16:28:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Data.Auditing;

/// <summary>
/// 空实体审计日志写入器
/// </summary>
public class NullEntityAuditLogWriter : IEntityAuditLogWriter
{
    /// <inheritdoc />
    public Task WriteAsync(EntityAuditLogRecord record, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
