#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SettingDefinitionContext
// Guid:ab40e1e9-77a3-4f4e-b5e3-87177e1dc272
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/02/27 14:35:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Core.Exceptions;

namespace XiHan.Framework.Settings.Definitions;

/// <summary>
/// 设置定义上下文
/// </summary>
public class SettingDefinitionContext : ISettingDefinitionContext, ISingletonDependency
{
    private readonly Dictionary<string, SettingDefinition> _settings;

    /// <summary>
    /// 构造函数
    /// </summary>
    public SettingDefinitionContext()
    {
        _settings = [];
    }

    /// <summary>
    /// 添加设置定义
    /// </summary>
    /// <param name="definition">设置定义</param>
    /// <exception cref="XiHanException">设置已存在时抛出</exception>
    public void Add(SettingDefinition definition)
    {
        if (!_settings.TryAdd(definition.Name, definition))
        {
            throw new XiHanException($"设置 '{definition.Name}' 已存在!");
        }
    }

    /// <summary>
    /// 获取设置定义
    /// </summary>
    /// <param name="name">设置名称</param>
    /// <returns>设置定义，不存在时返回null</returns>
    public SettingDefinition? GetOrNull(string name)
    {
        return _settings.GetValueOrDefault(name);
    }

    /// <summary>
    /// 获取所有设置定义
    /// </summary>
    /// <returns>设置定义字典</returns>
    public Dictionary<string, SettingDefinition> GetAll()
    {
        return new Dictionary<string, SettingDefinition>(_settings);
    }
}
