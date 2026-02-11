#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SettingScope
// Guid:52f4831d-b684-49eb-9fb9-2ddbb853ef73
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/02/25 03:30:38
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Settings.Stores;

/// <summary>
/// 设置作用域
/// </summary>
public enum SettingScope
{
    /// <summary>
    /// 全局应用级别
    /// </summary>
    Application,

    /// <summary>
    /// 租户级别
    /// </summary>
    Tenant,

    /// <summary>
    /// 用户级别
    /// </summary>
    User,

    /// <summary>
    /// 会话级别
    /// </summary>
    Session
}
