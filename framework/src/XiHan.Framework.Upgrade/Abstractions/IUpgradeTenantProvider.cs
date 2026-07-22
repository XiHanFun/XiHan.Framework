// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.Upgrade.Abstractions;

/// <summary>
/// 升级租户提供者接口
/// </summary>
public interface IUpgradeTenantProvider
{
    /// <summary>
    /// 获取租户列表
    /// </summary>
    IReadOnlyList<BasicTenantInfo> GetTenants();
}
