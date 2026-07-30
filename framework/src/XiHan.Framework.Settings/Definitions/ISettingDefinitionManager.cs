// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Settings.Definitions;

/// <summary>
/// 设置定义管理器：把所有 <see cref="ISettingDefinitionProvider"/> 的 <c>Define</c> 结果汇总成只读定义表
/// </summary>
public interface ISettingDefinitionManager
{
    /// <summary>
    /// 按名称获取设置定义，不存在返回 null
    /// </summary>
    /// <param name="name">设置名称</param>
    /// <returns>设置定义</returns>
    SettingDefinition? GetOrNull(string name);

    /// <summary>
    /// 获取全部设置定义
    /// </summary>
    /// <returns>设置定义列表</returns>
    IReadOnlyList<SettingDefinition> GetAll();
}
