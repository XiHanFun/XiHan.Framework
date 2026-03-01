#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:UpgradeStatus
// Guid:0f74b2f3-6a72-4c14-9d3f-9b42d7a6f7cc
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/01 16:24:20
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Upgrade.Enums;

/// <summary>
/// 升级状态
/// </summary>
public enum UpgradeStatus
{
    /// <summary>
    /// 正常
    /// </summary>
    Normal = 0,

    /// <summary>
    /// 升级中
    /// </summary>
    Upgrading = 1,

    /// <summary>
    /// 完成
    /// </summary>
    Completed = 2,

    /// <summary>
    /// 失败
    /// </summary>
    Failed = 3
}
