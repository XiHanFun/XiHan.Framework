// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Core.Reflections;

/// <summary>
/// 程序集查找器
/// </summary>
public class AssemblyFinder : IAssemblyFinder
{
    private readonly IModuleContainer _moduleContainer;
    private readonly Lazy<IReadOnlyList<Assembly>> _assemblies;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="moduleContainer"></param>
    public AssemblyFinder(IModuleContainer moduleContainer)
    {
        _moduleContainer = moduleContainer;
        _assemblies = new Lazy<IReadOnlyList<Assembly>>(FindAll, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    /// 程序集
    /// </summary>
    public IReadOnlyList<Assembly> Assemblies => _assemblies.Value;

    /// <summary>
    /// 查找所有程序集
    /// </summary>
    /// <returns></returns>
    public IReadOnlyList<Assembly> FindAll()
    {
        List<Assembly> assemblies = [];

        foreach (var module in _moduleContainer.Modules)
        {
            assemblies.AddRange(module.AllAssemblies);
        }

        return [.. assemblies.Distinct()];
    }
}
