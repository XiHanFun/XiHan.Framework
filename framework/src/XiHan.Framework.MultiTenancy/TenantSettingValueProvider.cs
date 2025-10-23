#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TenantSettingValueProvider
// Guid:1349a3d7-92c6-4119-a624-4263791e02b1
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/19 6:58:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Settings.Definitions;
using XiHan.Framework.Settings.Providers;
using XiHan.Framework.Settings.Stores;

namespace XiHan.Framework.MultiTenancy;

/// <summary>
/// 租户设置值提供器
/// </summary>
public class TenantSettingValueProvider : SettingValueProvider
{
    /// <summary>
    /// 提供器名称
    /// </summary>
    public const string ProviderName = "T";

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="settingStore">设置存储</param>
    /// <param name="currentTenant">当前租户</param>
    public TenantSettingValueProvider(ISettingStore settingStore, ICurrentTenant currentTenant)
        : base(settingStore)
    {
        CurrentTenant = currentTenant;
    }

    /// <summary>
    /// 获取设置值
    /// </summary>
    public override string Name => ProviderName;

    /// <summary>
    /// 获取所有设置值
    /// </summary>
    protected ICurrentTenant CurrentTenant { get; }

    /// <summary>
    /// 获取设置值或空
    /// </summary>
    /// <param name="setting">设置定义</param>
    /// <returns>设置值</returns>
    public override async Task<string?> GetOrNullAsync(SettingDefinition setting)
    {
        return await SettingStore.GetOrNullAsync(setting.Name, Name, CurrentTenant.Id?.ToString());
    }

    /// <summary>
    /// 获取所有设置值
    /// </summary>
    /// <param name="settings">设置定义</param>
    /// <returns>设置值</returns>
    public override async Task<List<SettingValue>> GetAllAsync(SettingDefinition[] settings)
    {
        return await SettingStore.GetAllAsync([.. settings.Select(x => x.Name)], Name, CurrentTenant.Id?.ToString());
    }
}
