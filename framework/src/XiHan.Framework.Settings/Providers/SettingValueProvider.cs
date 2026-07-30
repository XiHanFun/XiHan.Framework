// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Settings.Definitions;
using XiHan.Framework.Settings.Stores;

namespace XiHan.Framework.Settings.Providers;

/// <summary>
/// 设置值提供者
/// </summary>
public abstract class SettingValueProvider : ISettingValueProvider, ITransientDependency
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="settingStore"></param>
    protected SettingValueProvider(ISettingStore settingStore)
    {
        SettingStore = settingStore;
    }

    /// <summary>
    /// 名称
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// 设置存储
    /// </summary>
    protected ISettingStore SettingStore { get; }

    /// <summary>
    /// 获取设置值
    /// </summary>
    /// <param name="setting"></param>
    /// <returns></returns>
    public abstract Task<string?> GetOrNullAsync(SettingDefinition setting);

    /// <summary>
    /// 获取所有设置值
    /// </summary>
    /// <param name="settings"></param>
    /// <returns></returns>
    public abstract Task<List<SettingValue>> GetAllAsync(SettingDefinition[] settings);
}
