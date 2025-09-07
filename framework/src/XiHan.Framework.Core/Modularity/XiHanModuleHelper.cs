#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
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
using System.Text;
using XiHan.Framework.Utils.Collections;
using XiHan.Framework.Utils.Logging;

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
        List<Type> moduleTypes = [];
        ConsoleLogger.Handle("加载曦寒模块:");
        //logger?.LogInformation("加载曦寒模块:");
        AddModuleAndDependenciesRecursively(moduleTypes, startupModuleType, logger);
        ConsoleLogger.Handle("已初始化所有模块。");
        //logger?.LogInformation("已初始化所有模块。");
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

        List<Type> dependencies = [];

        var dependencyDescriptors = moduleType.GetCustomAttributes()
            .OfType<IDependedTypesProvider>();

        foreach (var descriptor in dependencyDescriptors)
        {
            foreach (var dependedModuleType in descriptor.GetDependedTypes())
            {
                dependencies.AddIfNotContains(dependedModuleType);
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
        List<Assembly> assemblies = [];

        var additionalAssemblyDescriptors = moduleType.GetCustomAttributes()
            .OfType<IAdditionalModuleAssemblyProvider>();

        foreach (var descriptor in additionalAssemblyDescriptors)
        {
            foreach (var assembly in descriptor.GetAssemblies())
            {
                assemblies.AddIfNotContains(assembly);
            }
        }

        assemblies.Add(moduleType.Assembly);

        return [.. assemblies];
    }

    /// <summary>
    /// 是否为曦寒模块
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static bool IsXiHanModule(Type type)
    {
        var typeInfo = type.GetTypeInfo();

        return typeInfo is { IsClass: true, IsAbstract: false, IsGenericType: false } && typeof(IXiHanModule).GetTypeInfo().IsAssignableFrom(type);
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

    /// <summary>
    /// 递归添加模块和依赖项，并以目录树形式打印
    /// </summary>
    /// <param name="moduleTypes">已处理的模块列表，避免重复</param>
    /// <param name="moduleType">当前模块类型</param>
    /// <param name="logger">日志记录器(可选)</param>
    /// <param name="prefix">前缀字符串，用于构造目录树分支</param>
    /// <param name="isLast">当前模块是否为同级中的最后一个</param>
    private static void AddModuleAndDependenciesRecursively(List<Type> moduleTypes, Type moduleType, ILogger? logger,
        string prefix = "", bool isLast = true)
    {
        // 检查是否是合法的 XiHan 模块类型
        CheckXiHanModuleType(moduleType);

        var isAlreadyLoaded = moduleTypes.Contains(moduleType);

        // 构造树形结构的节点显示信息
        var nodeLine = BuildModuleNodeLine(moduleType, prefix, isLast, isAlreadyLoaded);

        ConsoleLogger.Handle(nodeLine);
        //logger?.LogInformation(nodeLine);

        if (isAlreadyLoaded)
        {
            return;
        }

        moduleTypes.Add(moduleType);

        // 获取当前模块依赖的模块类型
        var dependedModuleTypes = FindDependedModuleTypes(moduleType);

        // 遍历每个依赖的模块，并递归调用
        for (var i = 0; i < dependedModuleTypes.Count; i++)
        {
            var childIsLast = i == dependedModuleTypes.Count - 1;
            var childPrefix = BuildChildPrefix(prefix, isLast);
            AddModuleAndDependenciesRecursively(moduleTypes, dependedModuleTypes[i], logger, childPrefix, childIsLast);
        }
    }

    /// <summary>
    /// 构造模块节点的显示行
    /// </summary>
    /// <param name="moduleType">模块类型</param>
    /// <param name="prefix">前缀字符串</param>
    /// <param name="isLast">是否为同级最后一个</param>
    /// <param name="isAlreadyLoaded">是否已经加载</param>
    /// <returns>格式化的节点显示行</returns>
    private static string BuildModuleNodeLine(Type moduleType, string prefix, bool isLast, bool isAlreadyLoaded)
    {
        // 构造树形分支符号
        var branchSymbol = GetBranchSymbol(prefix, isLast);

        // 获取模块简短名称（去除命名空间前缀以提高可读性）
        var moduleName = GetModuleDisplayName(moduleType);

        // 构造完整的节点行
        var nodeBuilder = new StringBuilder();
        nodeBuilder.Append(prefix);
        nodeBuilder.Append(branchSymbol);
        nodeBuilder.Append(moduleName);

        if (isAlreadyLoaded)
        {
            nodeBuilder.Append(" [已跳过-重复加载]");
        }

        return nodeBuilder.ToString();
    }

    /// <summary>
    /// 获取树形分支符号
    /// </summary>
    /// <param name="prefix">当前前缀</param>
    /// <param name="isLast">是否为同级最后一个</param>
    /// <returns>分支符号</returns>
    private static string GetBranchSymbol(string prefix, bool isLast)
    {
        return string.IsNullOrEmpty(prefix) ? "" : isLast ? "└─" : "├─";
    }

    /// <summary>
    /// 构造子节点前缀
    /// </summary>
    /// <param name="currentPrefix">当前节点前缀</param>
    /// <param name="isCurrentLast">当前节点是否为同级最后一个</param>
    /// <returns>子节点前缀</returns>
    private static string BuildChildPrefix(string currentPrefix, bool isCurrentLast)
    {
        return currentPrefix + (isCurrentLast ? "  " : "│ ");
    }

    /// <summary>
    /// 获取模块的显示名称
    /// </summary>
    /// <param name="moduleType">模块类型</param>
    /// <returns>模块显示名称</returns>
    private static string GetModuleDisplayName(Type moduleType)
    {
        var fullName = moduleType.Namespace ?? moduleType.Name;

        // 如果是 XiHan.Framework 命名空间，显示简短形式
        return fullName.StartsWith("XiHan.Framework.") ? fullName["XiHan.Framework.".Length..] : fullName;
    }
}
