#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ISettingDefinitionContext
// Guid:3e2d1f0a-9b8c-7d6e-5f4a-3e2d-1f0a-9b8c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/27 14:32:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Settings.Definitions;

/// <summary>
/// 设置定义上下文接口
/// </summary>
public interface ISettingDefinitionContext
{
    /// <summary>
    /// 添加设置定义
    /// </summary>
    /// <param name="definition">设置定义</param>
    void Add(SettingDefinition definition);

    /// <summary>
    /// 获取设置定义
    /// </summary>
    /// <param name="name">设置名称</param>
    /// <returns>设置定义</returns>
    SettingDefinition? GetOrNull(string name);

    /// <summary>
    /// 获取所有设置定义
    /// </summary>
    /// <returns>设置定义字典</returns>
    Dictionary<string, SettingDefinition> GetAll();
}
