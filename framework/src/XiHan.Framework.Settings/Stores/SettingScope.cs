// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
