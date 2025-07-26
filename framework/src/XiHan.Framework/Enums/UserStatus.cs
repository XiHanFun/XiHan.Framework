#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:UserStatus
// Guid:0bb4ba7a-182a-4a74-aee2-20eed339b5d6
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/17 13:56:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Enums;

/// <summary>
/// 用户状态
/// </summary>
public enum UserStatus
{
    /// <summary>
    /// 未激活
    /// </summary>
    Inactive = 0,

    /// <summary>
    /// 激活
    /// </summary>
    Active = 1,

    /// <summary>
    /// 锁定
    /// </summary>
    Locked = 2,

    /// <summary>
    /// 禁用
    /// </summary>
    Disabled = 3,

    /// <summary>
    /// 待审核
    /// </summary>
    Pending = 4
}
