using XiHan.Framework.Bootstrap.Abstractions.Application;

namespace XiHan.Framework.Bootstrap.Abstractions.Modularity;

/// <summary>
/// 表示模块管理器抽象。
/// </summary>
public interface IModuleManager
{
    /// <summary>
    /// 初始化模块，异步。
    /// </summary>
    Task InitializeModulesAsync(ApplicationInitializationContext context);

    /// <summary>
    /// 初始化模块。
    /// </summary>
    void InitializeModules(ApplicationInitializationContext context);

    /// <summary>
    /// 关闭模块，异步。
    /// </summary>
    Task ShutdownModulesAsync(ApplicationShutdownContext context);

    /// <summary>
    /// 关闭模块。
    /// </summary>
    void ShutdownModules(ApplicationShutdownContext context);
}
