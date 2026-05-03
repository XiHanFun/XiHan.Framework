using Microsoft.Extensions.DependencyInjection;

namespace XiHan.Framework.Bootstrap.Abstractions.Application;

/// <summary>
/// 表示曦寒应用抽象。
/// </summary>
public interface IXiHanApplication : IApplicationInfoAccessor, IDisposable
{
    /// <summary>
    /// 获取启动模块类型。
    /// </summary>
    Type StartupModuleType { get; }

    /// <summary>
    /// 获取服务集合。
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// 获取根服务提供器。
    /// </summary>
    IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// 配置服务。
    /// </summary>
    Task ConfigureServicesAsync();

    /// <summary>
    /// 关闭应用。
    /// </summary>
    Task ShutdownAsync();

    /// <summary>
    /// 同步关闭应用。
    /// </summary>
    void Shutdown();
}
