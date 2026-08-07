// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Auditing;

/// <summary>
/// 空实体审计上下文提供器
/// </summary>
public class NullEntityAuditContextProvider : IEntityAuditContextProvider
{
    /// <summary>
    /// 创建审计记录基础对象，不填充任何字段
    /// </summary>
    /// <returns>空的审计记录</returns>
    public EntityDiffLogRecord CreateBaseRecord()
    {
        return new EntityDiffLogRecord();
    }

    /// <summary>
    /// 判断指定实体类型是否需要审计，始终不审计
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <returns>是否审计，始终为 false</returns>
    public bool ShouldAudit(Type entityType)
    {
        return false;
    }
}
