// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
