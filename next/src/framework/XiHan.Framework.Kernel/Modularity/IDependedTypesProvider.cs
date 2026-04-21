namespace XiHan.Framework.Kernel.Modularity;

/// <summary>
/// 表示模块依赖类型提供器。
/// </summary>
public interface IDependedTypesProvider
{
    /// <summary>
    /// 获取依赖的模块类型集合。
    /// </summary>
    /// <returns>依赖模块类型集合。</returns>
    Type[] GetDependedTypes();
}
