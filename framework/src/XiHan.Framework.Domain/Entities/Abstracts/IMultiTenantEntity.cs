// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Domain.Entities.Abstracts;

/// <summary>
/// 多租户实体接口
/// </summary>
/// <remarks>
/// 约定：TenantId 非空（long），默认值 0。
/// - 0 代表平台租户（全局实体统一使用），业务租户从 1 开始分配
/// - 非空约束确保 UNIQUE(TenantId, XxCode) 等复合唯一索引在全局记录上正常工作（NULL 在 MySQL/PG 的 UNIQUE 约束中不视为相等）
/// - 合并查询全局 + 私有：WHERE TenantId IN (0, {currentTenantId})
/// </remarks>
public interface IMultiTenantEntity
{
    /// <summary>
    /// 租户ID（0=平台租户；业务租户从 1 开始）
    /// </summary>
    long TenantId { get; set; }
}
