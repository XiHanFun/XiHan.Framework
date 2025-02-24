#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ISettingValueProvider
// Guid:9b147675-c26c-4f24-9b04-4bf939cf0a41
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/25 4:18:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Settings.Definitions;

namespace XiHan.Framework.Settings.Providers;

/// <summary>
/// 设置值提供者
/// </summary>
public interface ISettingValueProvider
{
    /// <summary>
    /// 名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 获取设置值
    /// </summary>
    /// <param name="setting"></param>
    /// <returns></returns>
    Task<string?> GetOrNullAsync(SettingDefinition setting);

    /// <summary>
    /// 获取所有设置值
    /// </summary>
    /// <param name="settings"></param>
    /// <returns></returns>
    Task<List<SettingValue>> GetAllAsync(SettingDefinition[] settings);
}
