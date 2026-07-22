// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
