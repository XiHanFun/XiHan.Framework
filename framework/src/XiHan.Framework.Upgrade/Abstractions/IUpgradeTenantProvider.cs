#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IUpgradeTenantProvider
// Guid:e95a2da0-7c69-4e3e-90be-73d2b91a2be2
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/01 16:27:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
