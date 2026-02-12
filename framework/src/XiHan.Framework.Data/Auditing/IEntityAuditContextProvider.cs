#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IEntityAuditContextProvider
// Guid:c8f577f7-a0eb-4f28-b312-ec3ac2d35d12
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/12 16:27:30
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Data.Auditing;

/// <summary>
/// 实体审计上下文提供器
/// </summary>
public interface IEntityAuditContextProvider
{
    /// <summary>
    /// 获取当前审计上下文
    /// </summary>
    /// <returns>上下文记录</returns>
    EntityAuditLogRecord CreateBaseRecord();

    /// <summary>
    /// 是否应审计指定实体类型
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <returns>是否审计</returns>
    bool ShouldAudit(Type entityType);
}
