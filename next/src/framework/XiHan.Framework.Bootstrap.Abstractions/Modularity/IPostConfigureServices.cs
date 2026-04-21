using XiHan.Framework.Kernel.Modularity;

namespace XiHan.Framework.Bootstrap.Abstractions.Modularity;

/// <summary>
/// 表示模块后置服务配置扩展点。
/// </summary>
public interface IPostConfigureServices
{
    /// <summary>
    /// 后置配置服务。
    /// </summary>
    void PostConfigureServices(ServiceConfigurationContext context);

    /// <summary>
    /// 异步后置配置服务。
    /// </summary>
    Task PostConfigureServicesAsync(ServiceConfigurationContext context);
}
