// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity.PlugIns;

namespace XiHan.Framework.Core.Modularity;

/// <summary>
/// 模块加载器接口
/// </summary>
public interface IModuleLoader
{
    /// <summary>
    /// 加载模块
    /// </summary>
    /// <param name="services"></param>
    /// <param name="startupModuleType"></param>
    /// <param name="plugInSources"></param>
    /// <returns></returns>
    IModuleDescriptor[] LoadModules(IServiceCollection services, Type startupModuleType, PlugInSourceList plugInSources);
}
