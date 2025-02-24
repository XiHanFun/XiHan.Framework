#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DefaultValueSettingValueProvider
// Guid:21fe3485-b1aa-477f-b04f-9143096a9f90
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/25 4:20:07
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Settings.Definitions;

namespace XiHan.Framework.Settings.Providers;

/// <summary>
/// 默认值设置值提供者
/// </summary>
public class DefaultValueSettingValueProvider : ISettingValueProvider
{
    /// <summary>
    /// 名称
    /// </summary>
    public string Name => "Default";

    /// <summary>
    /// 获取设置值
    /// </summary>
    /// <param name="setting"></param>
    /// <returns></returns>
    public Task<string?> GetOrNullAsync(SettingDefinition setting)
    {
        return Task.FromResult(setting.DefaultValue);
    }

    /// <summary>
    /// 获取所有设置值
    /// </summary>
    /// <param name="settings"></param>
    /// <returns></returns>
    public Task<List<SettingValue>> GetAllAsync(SettingDefinition[] settings)
    {
        return Task.FromResult(settings.Select(s => new SettingValue(s.Name, s.DefaultValue)).ToList());
    }
}
