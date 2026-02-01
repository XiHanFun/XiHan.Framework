#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PlugInSourceExtensions
// Guid:5d2d3037-ab3c-4c72-b825-dec4e03bb03e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/26 08:25:03
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using XiHan.Framework.Utils.Diagnostics;

namespace XiHan.Framework.Core.Modularity.PlugIns;

/// <summary>
/// 插件源扩展方法
/// </summary>
public static class PlugInSourceExtensions
{
    /// <summary>
    /// 获取所有模块和依赖项
    /// </summary>
    /// <param name="plugInSource"></param>
    /// <param name="logger"></param>
    /// <returns></returns>
    public static Type[] GetModulesWithAllDependencies(this IPlugInSource plugInSource, ILogger logger)
    {
        Guard.NotNull(plugInSource, nameof(plugInSource));

        return [.. plugInSource.GetModules()
            .SelectMany(type => XiHanModuleHelper.FindAllModuleTypes(type, logger))
            .Distinct()];
    }
}
