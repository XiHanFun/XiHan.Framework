// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Upgrade.Abstractions;

namespace XiHan.Framework.Upgrade.Services;

/// <summary>
/// 默认升级租户提供者
/// </summary>
public class DefaultUpgradeTenantProvider : IUpgradeTenantProvider
{
    private readonly ICurrentTenant? _currentTenant;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="currentTenant">当前租户（可选）</param>
    public DefaultUpgradeTenantProvider(ICurrentTenant? currentTenant = null)
    {
        _currentTenant = currentTenant;
    }

    /// <summary>
    /// 获取租户列表（默认返回当前租户或主租户）
    /// </summary>
    /// <returns>租户列表</returns>
    public IReadOnlyList<BasicTenantInfo> GetTenants()
    {
        return
        [
            new BasicTenantInfo(_currentTenant?.Id, _currentTenant?.Name)
        ];
    }
}
