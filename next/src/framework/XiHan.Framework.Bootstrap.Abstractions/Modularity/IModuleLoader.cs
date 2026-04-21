using Microsoft.Extensions.DependencyInjection;

namespace XiHan.Framework.Bootstrap.Abstractions.Modularity;

/// <summary>
/// 表示模块加载器抽象。
/// </summary>
public interface IModuleLoader
{
    /// <summary>
    /// 从启动模块加载完整模块图。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="startupModuleType">启动模块类型。</param>
    /// <returns>已排序的模块描述集合。</returns>
    IReadOnlyList<IModuleDescriptor> LoadModules(IServiceCollection services, Type startupModuleType);
}
