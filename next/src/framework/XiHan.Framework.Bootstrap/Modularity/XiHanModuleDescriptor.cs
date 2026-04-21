using System.Reflection;
using XiHan.Framework.Bootstrap.Abstractions.Modularity;
using XiHan.Framework.Kernel.Modularity;

namespace XiHan.Framework.Bootstrap.Modularity;

/// <summary>
/// 提供默认模块描述器实现。
/// </summary>
public sealed class XiHanModuleDescriptor : IModuleDescriptor
{
    private readonly List<IModuleDescriptor> _dependencies = [];

    /// <summary>
    /// 使用指定模块信息初始化描述器。
    /// </summary>
    /// <param name="type">模块类型。</param>
    /// <param name="instance">模块实例。</param>
    public XiHanModuleDescriptor(Type type, IXiHanModule instance)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(instance);

        if (!typeof(IXiHanModule).IsAssignableFrom(type))
        {
            throw new ArgumentException($"类型 {type.AssemblyQualifiedName} 不是有效模块类型。", nameof(type));
        }

        if (!type.IsInstanceOfType(instance))
        {
            throw new ArgumentException($"模块实例 {instance.GetType().AssemblyQualifiedName} 与模块类型 {type.AssemblyQualifiedName} 不匹配。", nameof(instance));
        }

        Type = type;
        Assembly = type.Assembly;
        Instance = instance;
    }

    /// <inheritdoc />
    public Type Type { get; }

    /// <inheritdoc />
    public Assembly Assembly { get; }

    /// <inheritdoc />
    public IXiHanModule Instance { get; }

    /// <inheritdoc />
    public IReadOnlyList<IModuleDescriptor> Dependencies => _dependencies;

    /// <summary>
    /// 添加依赖模块。
    /// </summary>
    /// <param name="dependency">依赖模块描述。</param>
    public void AddDependency(IModuleDescriptor dependency)
    {
        ArgumentNullException.ThrowIfNull(dependency);

        if (_dependencies.Contains(dependency))
        {
            return;
        }

        _dependencies.Add(dependency);
    }
}
