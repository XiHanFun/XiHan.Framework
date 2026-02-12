#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:NullEntityAuditContextProvider
// Guid:55d8a5e5-ebf8-43fa-9b9f-0e3f0904f6ef
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/12 16:28:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Data.Auditing;

/// <summary>
/// 空实体审计上下文提供器
/// </summary>
public class NullEntityAuditContextProvider : IEntityAuditContextProvider
{
    /// <inheritdoc />
    public EntityAuditLogRecord CreateBaseRecord()
    {
        return new EntityAuditLogRecord();
    }

    /// <inheritdoc />
    public bool ShouldAudit(Type entityType)
    {
        return false;
    }
}
