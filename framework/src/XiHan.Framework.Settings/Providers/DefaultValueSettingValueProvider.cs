// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Settings.Definitions;
using XiHan.Framework.Settings.Stores;

namespace XiHan.Framework.Settings.Providers;

/// <summary>
/// 默认值设置值提供者
/// </summary>
public class DefaultValueSettingValueProvider : SettingValueProvider
{
    /// <summary>
    /// 提供者名称
    /// </summary>
    public const string ProviderName = "D";

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="settingStore"></param>
    public DefaultValueSettingValueProvider(ISettingStore settingStore)
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
        return Task.FromResult(setting.DefaultValue);
    }

    /// <summary>
    /// 获取所有设置值
    /// </summary>
    /// <param name="settings"></param>
    /// <returns></returns>
    public override Task<List<SettingValue>> GetAllAsync(SettingDefinition[] settings)
    {
        return Task.FromResult(settings.Select(x => new SettingValue(x.Name, x.DefaultValue)).ToList());
    }
}
