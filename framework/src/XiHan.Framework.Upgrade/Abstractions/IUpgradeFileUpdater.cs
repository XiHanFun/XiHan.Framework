#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IUpgradeFileUpdater
// Guid:ca39d9d2-8f1e-4f3e-8fe4-d92f5b9b50a4
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/01 16:26:30
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Upgrade.Abstractions;

/// <summary>
/// 程序文件更新器接口
/// </summary>
public interface IUpgradeFileUpdater
{
    /// <summary>
    /// 替换程序文件
    /// </summary>
    Task ApplyAsync(CancellationToken cancellationToken = default);
}
