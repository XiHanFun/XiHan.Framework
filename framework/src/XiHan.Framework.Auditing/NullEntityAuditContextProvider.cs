// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Auditing;

/// <summary>
/// 空实体审计上下文提供器
/// </summary>
public class NullEntityAuditContextProvider : IEntityAuditContextProvider
{
    /// <inheritdoc />
    public EntityDiffLogRecord CreateBaseRecord()
    {
        return new EntityDiffLogRecord();
    }

    /// <inheritdoc />
    public bool ShouldAudit(Type entityType)
    {
        return false;
    }
}
