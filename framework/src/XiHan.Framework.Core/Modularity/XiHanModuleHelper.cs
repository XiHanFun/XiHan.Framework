#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanModuleHelper
// Guid:f32042c8-be86-4a61-9e4a-cad19d38bcba
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/26 8:26:28
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using System.Reflection;
using XiHan.Framework.Utils.Extensions.System.Collections.Generic;

namespace XiHan.Framework.Core.Modularity;

/// <summary>
/// 曦寒模块帮助类
/// </summary>
public static class XiHanModuleHelper
{
    /// <summary>
    /// 查找所有模块类型
    /// </summary>
    /// <param name="startupModuleType"></param>
    /// <param name="logger"></param>
    /// <returns></returns>
    public static List<Type> FindAllModuleTypes(Type startupModuleType, ILogger? logger)
    {
        List<Type>? moduleTypes = [];
        logger?.Log(LogLevel.Information, "加载曦寒模块:");
        AddModuleAndDependenciesRecursively(moduleTypes, startupModuleType, logger);
        return moduleTypes;
    }

    /// <summary>
    /// 查找依赖模块类型
    /// </summary>
    /// <param name="moduleType"></param>
    /// <returns></returns>
    public static List<Type> FindDependedModuleTypes(Type moduleType)
    {
        CheckXiHanModuleType(moduleType);

        List<Type>? dependencies = [];

        var dependencyDescriptors = moduleType.GetCustomAttributes()
            .OfType<IDependedTypesProvider>();

        foreach (var descriptor in dependencyDescriptors)
        {
            foreach (var dependedModuleType in descriptor.GetDependedTypes())
            {
                _ = dependencies.AddIfNotContains(dependedModuleType);
            }
        }

        return dependencies;
    }

    /// <summary>
    /// 获取所有程序集
    /// </summary>
    /// <param name="moduleType"></param>
    /// <returns></returns>
    public static Assembly[] GetAllAssemblies(Type moduleType)
    {
        List<Assembly>? assemblies = [];

        var additionalAssemblyDescriptors = moduleType.GetCustomAttributes()
            .OfType<IAdditionalModuleAssemblyProvider>();

        foreach (var descriptor in additionalAssemblyDescriptors)
        {
            foreach (var assembly in descriptor.GetAssemblies())
            {
                _ = assemblies.AddIfNotContains(assembly);
            }
        }

        assemblies.Add(moduleType.Assembly);

        return [.. assemblies];
    }

    /// <summary>
    /// 递归添加模块和依赖项
    /// </summary>
    /// <param name="moduleTypes"></param>
    /// <param name="moduleType"></param>
    /// <param name="logger"></param>
    /// <param name="depth"></param>
    private static void AddModuleAndDependenciesRecursively(List<Type> moduleTypes, Type moduleType, ILogger? logger, int depth = 0)
    {
        CheckXiHanModuleType(moduleType);

        if (moduleTypes.Contains(moduleType))
        {
            return;
        }

        moduleTypes.Add(moduleType);
        logger?.Log(LogLevel.Information, $"{new string(' ', depth * 2)}-{moduleType.FullName}");

        foreach (var dependedModuleType in FindDependedModuleTypes(moduleType))
        {
            AddModuleAndDependenciesRecursively(moduleTypes, dependedModuleType, logger, depth + 1);
        }
    }

    /// <summary>
    /// 是否为曦寒模块
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static bool IsXiHanModule(Type type)
    {
        var typeInfo = type.GetTypeInfo();

        return typeInfo.IsClass && !typeInfo.IsAbstract && !typeInfo.IsGenericType && typeof(IXiHanModule).GetTypeInfo().IsAssignableFrom(type);
    }

    /// <summary>
    /// 检测曦寒模块类
    /// </summary>
    /// <param name="moduleType"></param>
    /// <exception cref="ArgumentException"></exception>
    internal static void CheckXiHanModuleType(Type moduleType)
    {
        if (!IsXiHanModule(moduleType))
        {
            throw new ArgumentException("给定的类型不是曦寒模块:" + moduleType.AssemblyQualifiedName);
        }
    }
}
