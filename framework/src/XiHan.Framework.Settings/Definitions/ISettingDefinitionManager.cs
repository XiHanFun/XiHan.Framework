#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ISettingDefinitionManager
// Guid:2b5c8d1e-3f4a-4b6c-8d9e-1a2b3c4d5e6f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/14 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
