// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
