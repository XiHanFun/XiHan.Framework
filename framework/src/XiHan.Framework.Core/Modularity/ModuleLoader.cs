﻿#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ModuleLoader
// Guid:047944ae-8b28-465c-a4e5-2247329b629b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/26 8:04:27
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity.PlugIns;
using XiHan.Framework.Utils.Collections;
using XiHan.Framework.Utils.System;

namespace XiHan.Framework.Core.Modularity;

/// <summary>
/// 模块加载器
/// </summary>
public class ModuleLoader : IModuleLoader
{
    /// <summary>
    /// 加载模块
    /// </summary>
    /// <param name="services"></param>
    /// <param name="startupModuleType"></param>
    /// <param name="plugInSources"></param>
    /// <returns></returns>
    public IModuleDescriptor[] LoadModules(IServiceCollection services, Type startupModuleType, PlugInSourceList plugInSources)
    {
        _ = Guard.NotNull(services, nameof(services));
        _ = Guard.NotNull(startupModuleType, nameof(startupModuleType));
        _ = Guard.NotNull(plugInSources, nameof(plugInSources));

        var modules = GetDescriptors(services, startupModuleType, plugInSources);

        modules = SortByDependency(modules, startupModuleType);

        return [.. modules];
    }

    /// <summary>
    /// 填充模块
    /// </summary>
    /// <param name="modules"></param>
    /// <param name="services"></param>
    /// <param name="startupModuleType"></param>
    /// <param name="plugInSources"></param>
    protected virtual void FillModules(List<XiHanModuleDescriptor> modules, IServiceCollection services, Type startupModuleType, PlugInSourceList plugInSources)
    {
        var logger = services.GetInitLogger<XiHanApplicationBase>();

        // 所有从启动模块开始的模块
        modules.AddRange(XiHanModuleHelper.FindAllModuleTypes(startupModuleType, logger).Select(moduleType => CreateModuleDescriptor(services, moduleType)));

        // 插件模块
        foreach (var moduleType in plugInSources.GetAllModules(logger))
        {
            if (modules.Any(m => m.Type == moduleType))
            {
                continue;
            }

            modules.Add(CreateModuleDescriptor(services, moduleType, true));
        }
    }

    /// <summary>
    /// 设置依赖项
    /// </summary>
    /// <param name="modules"></param>
    protected virtual void SetDependencies(List<XiHanModuleDescriptor> modules)
    {
        foreach (var module in modules)
        {
            SetDependencies(modules, module);
        }
    }

    /// <summary>
    /// 按依赖项排序
    /// </summary>
    /// <param name="modules"></param>
    /// <param name="startupModuleType"></param>
    /// <returns></returns>
    protected virtual List<IModuleDescriptor> SortByDependency(List<IModuleDescriptor> modules, Type startupModuleType)
    {
        var sortedModules = modules.SortByDependencies(m => m.Dependencies);
        sortedModules.MoveItem(m => m.Type == startupModuleType, modules.Count - 1);
        return sortedModules;
    }

    /// <summary>
    /// 创建模块描述器
    /// </summary>
    /// <param name="services"></param>
    /// <param name="moduleType"></param>
    /// <param name="isLoadedAsPlugIn"></param>
    /// <returns></returns>
    protected virtual XiHanModuleDescriptor CreateModuleDescriptor(IServiceCollection services, Type moduleType, bool isLoadedAsPlugIn = false)
    {
        return new XiHanModuleDescriptor(moduleType, CreateAndRegisterModule(services, moduleType), isLoadedAsPlugIn);
    }

    /// <summary>
    /// 创建并注册模块
    /// </summary>
    /// <param name="services"></param>
    /// <param name="moduleType"></param>
    /// <returns></returns>
    protected virtual IXiHanModule CreateAndRegisterModule(IServiceCollection services, Type moduleType)
    {
        var module = (IXiHanModule)Activator.CreateInstance(moduleType)!;
        _ = services.AddSingleton(moduleType, module);
        return module;
    }

    /// <summary>
    /// 设置依赖项
    /// </summary>
    /// <param name="modules"></param>
    /// <param name="module"></param>
    /// <exception cref="Exception"></exception>
    protected virtual void SetDependencies(List<XiHanModuleDescriptor> modules, XiHanModuleDescriptor module)
    {
        foreach (var dependedModule in XiHanModuleHelper.FindDependedModuleTypes(module.Type).Select(dependedModuleType => modules.FirstOrDefault(m => m.Type == dependedModuleType) ??
            throw new Exception($"在 {module.Type.AssemblyQualifiedName} 无法找到依赖的模块 {dependedModuleType.AssemblyQualifiedName}！")))
        {
            module.AddDependency(dependedModule);
        }
    }

    /// <summary>
    /// 获取模块描述器列表
    /// </summary>
    /// <param name="services"></param>
    /// <param name="startupModuleType"></param>
    /// <param name="plugInSources"></param>
    /// <returns></returns>
    private List<IModuleDescriptor> GetDescriptors(IServiceCollection services, Type startupModuleType, PlugInSourceList plugInSources)
    {
        List<XiHanModuleDescriptor> modules = [];

        FillModules(modules, services, startupModuleType, plugInSources);
        SetDependencies(modules);

        return [.. modules.Cast<IModuleDescriptor>()];
    }
}
