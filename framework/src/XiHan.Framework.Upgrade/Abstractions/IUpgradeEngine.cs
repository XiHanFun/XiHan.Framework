#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IUpgradeEngine
// Guid:26e8f9b1-9614-4e0b-8f15-7f8f1f5a8f8e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/01 16:25:40
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Upgrade.Models;

namespace XiHan.Framework.Upgrade.Abstractions;

/// <summary>
/// 升级引擎接口
/// </summary>
public interface IUpgradeEngine
{
    /// <summary>
    /// 执行升级
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<UpgradeStartResult> ExecuteAsync(CancellationToken cancellationToken = default);
}
