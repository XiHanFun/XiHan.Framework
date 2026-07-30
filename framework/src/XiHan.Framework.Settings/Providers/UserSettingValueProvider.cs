// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
    /// 名称
    /// </summary>
    public override string Name => ProviderName;

    /// <summary>
    /// 当前用户
    /// </summary>
    protected ICurrentUser CurrentUser { get; }

    /// <summary>
    /// 获取设置值
    /// </summary>
    /// <param name="setting"></param>
    /// <returns></returns>
    public override async Task<string?> GetOrNullAsync(SettingDefinition setting)
    {
        return CurrentUser.UserId is null ? null : await SettingStore.GetOrNullAsync(setting.Name, Name, CurrentUser.UserId.ToString());
    }

    /// <summary>
    /// 获取所有设置值
    /// </summary>
    /// <param name="settings"></param>
    /// <returns></returns>
    public override async Task<List<SettingValue>> GetAllAsync(SettingDefinition[] settings)
    {
        return CurrentUser.UserId is null
            ? [.. settings.Select(x => new SettingValue(x.Name, null))]
            : await SettingStore.GetAllAsync([.. settings.Select(x => x.Name)], Name, CurrentUser.UserId.ToString());
    }
}
