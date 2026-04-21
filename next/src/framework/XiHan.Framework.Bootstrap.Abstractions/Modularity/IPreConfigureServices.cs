using XiHan.Framework.Kernel.Modularity;

namespace XiHan.Framework.Bootstrap.Abstractions.Modularity;

/// <summary>
/// 表示模块预配置服务扩展点。
/// </summary>
public interface IPreConfigureServices
{
    /// <summary>
    /// 预配置服务。
    /// </summary>
    void PreConfigureServices(ServiceConfigurationContext context);

    /// <summary>
    /// 异步预配置服务。
    /// </summary>
    Task PreConfigureServicesAsync(ServiceConfigurationContext context);
}
