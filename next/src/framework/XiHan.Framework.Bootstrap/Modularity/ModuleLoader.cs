using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Bootstrap.Abstractions.Modularity;
using XiHan.Framework.Kernel.Modularity;

namespace XiHan.Framework.Bootstrap.Modularity;

/// <summary>
/// 提供默认模块加载器实现。
/// </summary>
public sealed class ModuleLoader : IModuleLoader
{
    /// <inheritdoc />
    public IReadOnlyList<IModuleDescriptor> LoadModules(IServiceCollection services, Type startupModuleType)
    {
        ArgumentNullException.ThrowIfNull(services);

        var moduleTypes = XiHanModuleHelper.FindAllModuleTypes(startupModuleType);
        var descriptors = moduleTypes
            .Select(moduleType => CreateDescriptor(services, moduleType))
            .ToDictionary(static descriptor => descriptor.Type);

        foreach (var descriptor in descriptors.Values)
        {
            foreach (var dependedModuleType in XiHanModuleHelper.FindDependedModuleTypes(descriptor.Type))
            {
                if (!descriptors.TryGetValue(dependedModuleType, out var dependency))
                {
                    throw new InvalidOperationException($"在模块 {descriptor.Type.AssemblyQualifiedName} 上找不到依赖模块 {dependedModuleType.AssemblyQualifiedName}。");
                }

                descriptor.AddDependency(dependency);
            }
        }

        return SortByDependencies(descriptors.Values.Cast<IModuleDescriptor>().ToArray());
    }

    private static XiHanModuleDescriptor CreateDescriptor(IServiceCollection services, Type moduleType)
    {
        var module = (IXiHanModule)Activator.CreateInstance(moduleType)!;
        services.AddSingleton(moduleType, module);
        return new XiHanModuleDescriptor(moduleType, module);
    }

    private static IReadOnlyList<IModuleDescriptor> SortByDependencies(IReadOnlyCollection<IModuleDescriptor> modules)
    {
        var ordered = new List<IModuleDescriptor>(modules.Count);
        var states = new Dictionary<Type, VisitState>();

        foreach (var module in modules)
        {
            Visit(module, states, ordered);
        }

        return ordered;
    }

    private static void Visit(IModuleDescriptor module, IDictionary<Type, VisitState> states, ICollection<IModuleDescriptor> ordered)
    {
        if (states.TryGetValue(module.Type, out var state))
        {
            if (state == VisitState.Visited)
            {
                return;
            }

            if (state == VisitState.Visiting)
            {
                throw new InvalidOperationException($"检测到模块循环依赖：{module.Type.AssemblyQualifiedName}。");
            }
        }

        states[module.Type] = VisitState.Visiting;

        foreach (var dependency in module.Dependencies)
        {
            Visit(dependency, states, ordered);
        }

        states[module.Type] = VisitState.Visited;

        if (!ordered.Contains(module))
        {
            ordered.Add(module);
        }
    }

    private enum VisitState
    {
        Visiting = 1,
        Visited = 2
    }
}
