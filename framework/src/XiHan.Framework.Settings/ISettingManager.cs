// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Settings.Definitions;
using XiHan.Framework.Settings.Stores;

namespace XiHan.Framework.Settings;

/// <summary>
/// 设置管理器接口
/// </summary>
public interface ISettingManager
{
    /// <summary>
    /// 添加设置定义
    /// </summary>
    /// <param name="definition"></param>
    /// <exception cref="XiHanException"></exception>
    void AddDefinition(SettingDefinition definition);

    /// <summary>
    /// 获取设置值
    /// </summary>
    /// <param name="name"></param>
    /// <param name="scope"></param>
    /// <returns></returns>
    /// <exception cref="XiHanException"></exception>
    Task<string?> GetOrNullAsync(string name, SettingScope scope = SettingScope.Application);

    /// <summary>
    /// 设置值
    /// </summary>
    /// <param name="name"></param>
    /// <param name="value"></param>
    /// <param name="scope"></param>
    /// <returns></returns>
    /// <exception cref="XiHanException"></exception>
    Task SetValueAsync(string name, string? value, SettingScope scope = SettingScope.Application);
}
