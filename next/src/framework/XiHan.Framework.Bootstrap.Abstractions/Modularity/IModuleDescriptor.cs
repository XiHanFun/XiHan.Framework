using System.Reflection;
using XiHan.Framework.Kernel.Modularity;

namespace XiHan.Framework.Bootstrap.Abstractions.Modularity;

/// <summary>
/// 表示模块描述信息。
/// </summary>
public interface IModuleDescriptor
{
    /// <summary>
    /// 获取模块类型。
    /// </summary>
    Type Type { get; }

    /// <summary>
    /// 获取模块主程序集。
    /// </summary>
    Assembly Assembly { get; }

    /// <summary>
    /// 获取模块实例。
    /// </summary>
    IXiHanModule Instance { get; }

    /// <summary>
    /// 获取依赖模块集合。
    /// </summary>
    IReadOnlyList<IModuleDescriptor> Dependencies { get; }
}
