namespace XiHan.Framework.Kernel.Modularity;

/// <summary>
/// 用于声明模块依赖关系。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute : Attribute, IDependedTypesProvider
{
    /// <summary>
    /// 使用指定依赖模块类型初始化特性。
    /// </summary>
    /// <param name="dependedTypes">依赖模块类型集合。</param>
    public DependsOnAttribute(params Type[]? dependedTypes)
    {
        DependedTypes = dependedTypes ?? Type.EmptyTypes;
    }

    /// <summary>
    /// 获取依赖模块类型集合。
    /// </summary>
    public Type[] DependedTypes { get; }

    /// <inheritdoc />
    public Type[] GetDependedTypes()
    {
        return DependedTypes;
    }
}
