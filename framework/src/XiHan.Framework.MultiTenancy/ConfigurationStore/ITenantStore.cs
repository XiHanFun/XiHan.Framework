// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.MultiTenancy.ConfigurationStore;

/// <summary>
/// 租户存储接口
/// </summary>
public interface ITenantStore
{
    /// <summary>
    /// 根据租户 Id 查询租户配置
    /// </summary>
    /// <param name="id">租户 Id</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>租户配置</returns>
    Task<TenantConfiguration?> FindAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据租户名称查询租户配置
    /// </summary>
    /// <param name="name">租户名称（Name 或 NormalizedName）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>租户配置</returns>
    Task<TenantConfiguration?> FindAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取租户配置列表
    /// </summary>
    /// <param name="includeInactive">是否包含非激活租户</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>租户配置列表</returns>
    Task<IReadOnlyList<TenantConfiguration>> GetListAsync(bool includeInactive = true, CancellationToken cancellationToken = default);
}
