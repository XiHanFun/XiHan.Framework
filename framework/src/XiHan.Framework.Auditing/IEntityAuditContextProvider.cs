// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Auditing;

/// <summary>
/// 实体审计上下文提供器
/// </summary>
public interface IEntityAuditContextProvider
{
    /// <summary>
    /// 获取当前审计上下文
    /// </summary>
    /// <returns>上下文记录</returns>
    EntityDiffLogRecord CreateBaseRecord();

    /// <summary>
    /// 是否应审计指定实体类型
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <returns>是否审计</returns>
    bool ShouldAudit(Type entityType);
}
