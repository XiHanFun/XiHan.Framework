#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:GlobalSettingValueProvider
// Guid:5c09f57c-fd37-4cc0-a1c2-f80fb312d8e9
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/02/25 04:45:28
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
