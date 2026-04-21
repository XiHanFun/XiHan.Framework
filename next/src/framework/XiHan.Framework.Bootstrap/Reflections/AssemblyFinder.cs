using System.Reflection;
using XiHan.Framework.Bootstrap.Abstractions.Reflections;

namespace XiHan.Framework.Bootstrap.Reflections;

/// <summary>
/// 提供默认程序集查找器实现。
/// </summary>
public sealed class AssemblyFinder : IAssemblyFinder
{
    private readonly IReadOnlyList<Assembly> _assemblies;

    /// <summary>
    /// 使用指定程序集集合初始化查找器。
    /// </summary>
    /// <param name="assemblies">程序集集合。</param>
    public AssemblyFinder(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        _assemblies = assemblies.Distinct().ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyList<Assembly> Assemblies => _assemblies;
}
