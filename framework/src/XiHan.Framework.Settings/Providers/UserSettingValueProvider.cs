#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:UserSettingValueProvider
// Guid:d1fe1bbc-bb4a-4c89-9ff7-aa4e01032662
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/25 4:58:22
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Security.Users;
using XiHan.Framework.Settings.Definitions;
using XiHan.Framework.Settings.Stores;

namespace XiHan.Framework.Settings.Providers;

/// <summary>
/// 用户设置值提供者
/// </summary>
public class UserSettingValueProvider : SettingValueProvider
{
    /// <summary>
    /// 提供者名称
    /// </summary>
    public const string ProviderName = "U";

    /// <summary>
    /// 名称
    /// </summary>
    public override string Name => ProviderName;

    /// <summary>
    /// 当前用户
    /// </summary>
    protected ICurrentUser CurrentUser { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="settingStore"></param>
    /// <param name="currentUser"></param>
    public UserSettingValueProvider(ISettingStore settingStore, ICurrentUser currentUser)
        : base(settingStore)
    {
        CurrentUser = currentUser;
    }

    /// <summary>
    /// 获取设置值
    /// </summary>
    /// <param name="setting"></param>
    /// <returns></returns>
    public override async Task<string?> GetOrNullAsync(SettingDefinition setting)
    {
        return CurrentUser.Id == null ? null : await SettingStore.GetOrNullAsync(setting.Name, Name, CurrentUser.Id.ToString());
    }

    /// <summary>
    /// 获取所有设置值
    /// </summary>
    /// <param name="settings"></param>
    /// <returns></returns>
    public override async Task<List<SettingValue>> GetAllAsync(SettingDefinition[] settings)
    {
        return CurrentUser.Id == null
            ? settings.Select(x => new SettingValue(x.Name, null)).ToList()
            : await SettingStore.GetAllAsync(settings.Select(x => x.Name).ToArray(), Name, CurrentUser.Id.ToString());
    }
}
