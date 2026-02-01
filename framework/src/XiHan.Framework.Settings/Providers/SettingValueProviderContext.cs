#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SettingValueProviderContext
// Guid:8bc9ee81-3380-4909-9fe1-ac5165175c89
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/02/25 04:19:03
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Settings.Definitions;
using XiHan.Framework.Settings.Stores;

namespace XiHan.Framework.Settings.Providers;

/// <summary>
/// 设置值提供者上下文
/// </summary>
public class SettingValueProviderContext
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="setting"></param>
    /// <param name="scope"></param>
    /// <param name="serviceProvider"></param>
    public SettingValueProviderContext(
        SettingDefinition setting,
        SettingScope scope,
        IServiceProvider serviceProvider)
    {
        Setting = setting;
        Scope = scope;
        ServiceProvider = serviceProvider;
    }

    /// <summary>
    /// 设置定义
    /// </summary>
    public SettingDefinition Setting { get; }

    /// <summary>
    /// 作用域
    /// </summary>
    public SettingScope Scope { get; }

    /// <summary>
    /// 服务提供者
    /// </summary>
    public IServiceProvider ServiceProvider { get; }
}
