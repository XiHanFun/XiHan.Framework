// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Settings.Definitions;

namespace XiHan.Framework.Settings.Tests.Fakes;

/// <summary>
/// 设置定义管理器替身
/// </summary>
/// <remarks>
/// 用固定的定义表替代真实的懒加载汇总流程，让设置管理器的测试只聚焦编排逻辑本身。
/// </remarks>
public sealed class FakeSettingDefinitionManager : ISettingDefinitionManager
{
    private readonly Dictionary<string, SettingDefinition> _definitions = [];

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="definitions">预置的设置定义</param>
    public FakeSettingDefinitionManager(params SettingDefinition[] definitions)
    {
        foreach (var definition in definitions)
        {
            _definitions[definition.Name] = definition;
        }
    }

    /// <summary>
    /// 按名称获取设置定义
    /// </summary>
    /// <param name="name">设置名称</param>
    /// <returns>设置定义，不存在返回 null</returns>
    public SettingDefinition? GetOrNull(string name)
    {
        return _definitions.GetValueOrDefault(name);
    }

    /// <summary>
    /// 获取全部设置定义
    /// </summary>
    /// <returns>设置定义列表</returns>
    public IReadOnlyList<SettingDefinition> GetAll()
    {
        return [.. _definitions.Values];
    }
}
