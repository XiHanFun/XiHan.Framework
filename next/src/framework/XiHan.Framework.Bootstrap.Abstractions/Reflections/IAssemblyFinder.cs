using System.Reflection;

namespace XiHan.Framework.Bootstrap.Abstractions.Reflections;

/// <summary>
/// 表示程序集查找器抽象。
/// </summary>
public interface IAssemblyFinder
{
    /// <summary>
    /// 获取可用于类型发现的程序集集合。
    /// </summary>
    IReadOnlyList<Assembly> Assemblies { get; }
}
