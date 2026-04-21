using XiHan.Framework.Kernel.Modularity;

namespace XiHan.Framework.Bootstrap.Modularity;

/// <summary>
/// 提供模块类型分析辅助方法。
/// </summary>
public static class XiHanModuleHelper
{
    /// <summary>
    /// 校验模块类型是否合法。
    /// </summary>
    /// <param name="moduleType">模块类型。</param>
    public static void CheckModuleType(Type moduleType)
    {
        ArgumentNullException.ThrowIfNull(moduleType);

        if (!typeof(IXiHanModule).IsAssignableFrom(moduleType))
        {
            throw new ArgumentException($"类型 {moduleType.AssemblyQualifiedName} 必须实现 {typeof(IXiHanModule).AssemblyQualifiedName}。", nameof(moduleType));
        }

        if (moduleType.IsAbstract || moduleType.IsInterface)
        {
            throw new ArgumentException($"模块类型 {moduleType.AssemblyQualifiedName} 必须是可实例化的具体类型。", nameof(moduleType));
        }
    }

    /// <summary>
    /// 查找指定模块类型的所有依赖模块类型。
    /// </summary>
    /// <param name="moduleType">模块类型。</param>
    /// <returns>依赖模块类型集合。</returns>
    public static IReadOnlyList<Type> FindDependedModuleTypes(Type moduleType)
    {
        ArgumentNullException.ThrowIfNull(moduleType);

        return moduleType
            .GetCustomAttributes(inherit: true)
            .OfType<IDependedTypesProvider>()
            .SelectMany(static provider => provider.GetDependedTypes())
            .Distinct()
            .ToArray();
    }

    /// <summary>
    /// 查找从启动模块可达的所有模块类型。
    /// </summary>
    /// <param name="startupModuleType">启动模块类型。</param>
    /// <returns>模块类型集合。</returns>
    public static IReadOnlyList<Type> FindAllModuleTypes(Type startupModuleType)
    {
        CheckModuleType(startupModuleType);

        var modules = new List<Type>();
        var visited = new HashSet<Type>();
        FillModuleTypes(startupModuleType, modules, visited);
        return modules;
    }

    private static void FillModuleTypes(Type moduleType, List<Type> modules, HashSet<Type> visited)
    {
        if (!visited.Add(moduleType))
        {
            return;
        }

        foreach (var dependedModuleType in FindDependedModuleTypes(moduleType))
        {
            CheckModuleType(dependedModuleType);
            FillModuleTypes(dependedModuleType, modules, visited);
        }

        modules.Add(moduleType);
    }
}
