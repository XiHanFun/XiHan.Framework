#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AssemblyFinder
// Guid:f4f61066-31b7-4ef6-acc3-f59ec25af356
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/4/24 23:02:32
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Immutable;
using System.Reflection;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Core.Reflection;

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
        List<Assembly>? assemblies = new();

        foreach (IModuleDescriptor? module in _moduleContainer.Modules)
        {
            assemblies.AddRange(module.AllAssemblies);
        }

        return assemblies.Distinct().ToImmutableList();
    }
}
