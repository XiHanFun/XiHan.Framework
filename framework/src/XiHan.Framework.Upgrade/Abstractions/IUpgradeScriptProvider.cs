#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IUpgradeScriptProvider
// Guid:4a314a4c-2e8c-4f2b-9e2c-0f5e0b2a851a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/01 16:26:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Upgrade.Models;

namespace XiHan.Framework.Upgrade.Abstractions;

/// <summary>
/// 升级脚本提供者接口
/// </summary>
public interface IUpgradeScriptProvider
{
    /// <summary>
    /// 获取升级脚本列表
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IReadOnlyList<UpgradeScript>> GetScriptsAsync(CancellationToken cancellationToken = default);
}
