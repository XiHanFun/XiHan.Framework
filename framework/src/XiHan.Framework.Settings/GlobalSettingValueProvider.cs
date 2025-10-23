#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:GlobalSettingValueProvider
// Guid:1812be3c-4ec0-4c5d-8267-8ad7068ed68b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/19 6:56:34
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Settings.Definitions;
using XiHan.Framework.Settings.Providers;
using XiHan.Framework.Settings.Stores;

namespace XiHan.Framework.Settings;

/// <summary>
/// 全局设置值提供器
/// </summary>
public class GlobalSettingValueProvider : SettingValueProvider
{
    /// <summary>
    /// 提供器名称
    /// </summary>
    public const string ProviderName = "G";

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="settingStore">设置存储</param>
    public GlobalSettingValueProvider(ISettingStore settingStore)
        : base(settingStore)
    {
    }

    /// <summary>
    /// 获取设置值
    /// </summary>
    public override string Name => ProviderName;

    /// <summary>
    /// 获取设置值或空
    /// </summary>
    /// <param name="setting">设置定义</param>
    /// <returns>设置值</returns>
    public override Task<string?> GetOrNullAsync(SettingDefinition setting)
    {
        return SettingStore.GetOrNullAsync(setting.Name, Name, null);
    }

    /// <summary>
    /// 获取所有设置值
    /// </summary>
    /// <param name="settings">设置定义</param>
    /// <returns>设置值</returns>
    public override Task<List<SettingValue>> GetAllAsync(SettingDefinition[] settings)
    {
        return SettingStore.GetAllAsync([.. settings.Select(x => x.Name)], Name, null);
    }
}
