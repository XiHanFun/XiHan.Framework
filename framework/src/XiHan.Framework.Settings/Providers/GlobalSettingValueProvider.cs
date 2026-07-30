// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Settings.Definitions;
using XiHan.Framework.Settings.Stores;

namespace XiHan.Framework.Settings.Providers;

/// <summary>
/// 全局设置值提供者
/// </summary>
public class GlobalSettingValueProvider : SettingValueProvider
{
    /// <summary>
    /// 提供者名称
    /// </summary>
    public const string ProviderName = "G";

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="settingStore"></param>
    public GlobalSettingValueProvider(ISettingStore settingStore)
        : base(settingStore)
    {
    }

    /// <summary>
    /// 名称
    /// </summary>
    public override string Name => ProviderName;

    /// <summary>
    /// 获取设置值
    /// </summary>
    /// <param name="setting"></param>
    /// <returns></returns>
    public override Task<string?> GetOrNullAsync(SettingDefinition setting)
    {
        return SettingStore.GetOrNullAsync(setting.Name, Name, null);
    }

    /// <summary>
    /// 获取所有设置值
    /// </summary>
    /// <param name="settings"></param>
    /// <returns></returns>
    public override Task<List<SettingValue>> GetAllAsync(SettingDefinition[] settings)
    {
        return SettingStore.GetAllAsync([.. settings.Select(x => x.Name)], Name, null);
    }
}
