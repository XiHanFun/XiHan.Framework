using System.Reflection;
using XiHan.Framework.Bootstrap.Abstractions.Reflections;

namespace XiHan.Framework.Bootstrap.Reflections;

/// <summary>
/// 提供默认类型查找器实现。
/// </summary>
public sealed class TypeFinder : ITypeFinder
{
    private readonly Lazy<IReadOnlyList<Type>> _types;
    private readonly IAssemblyFinder _assemblyFinder;

    /// <summary>
    /// 初始化类型查找器实例。
    /// </summary>
    /// <param name="assemblyFinder">程序集查找器。</param>
    public TypeFinder(IAssemblyFinder assemblyFinder)
    {
        _assemblyFinder = assemblyFinder;
        _types = new Lazy<IReadOnlyList<Type>>(FindAll, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <inheritdoc />
    public IReadOnlyList<Type> Types => _types.Value;

    private IReadOnlyList<Type> FindAll()
    {
        var allTypes = new List<Type>();

        foreach (var assembly in _assemblyFinder.Assemblies)
        {
            try
            {
                allTypes.AddRange(assembly.GetTypes());
            }
            catch (ReflectionTypeLoadException exception)
            {
                allTypes.AddRange(exception.Types.Where(static type => type is not null)!);
            }
        }

        return allTypes.Distinct().ToArray();
    }
}
