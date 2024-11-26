#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PlugInSourceList
// Guid:e097e9be-3b02-4937-9258-b2670365fdb2
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/26 8:23:04
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;

namespace XiHan.Framework.Core.Modularity.PlugIns;

/// <summary>
/// 插件源列表
/// </summary>
public class PlugInSourceList : List<IPlugInSource>
{
    /// <summary>
    /// 获取所有模块
    /// </summary>
    /// <param name="logger"></param>
    /// <returns></returns>
    internal Type[] GetAllModules(ILogger logger)
    {
        return this
            .SelectMany(pluginSource => pluginSource.GetModulesWithAllDependencies(logger))
            .Distinct()
            .ToArray();
    }
}