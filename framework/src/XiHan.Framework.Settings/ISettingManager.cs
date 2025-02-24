#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ISettingManager
// Guid:df6b00a0-33e4-44de-be1f-4d370abe01f2
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/25 3:34:38
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
