using Microsoft.Extensions.DependencyInjection;

namespace XiHan.Framework.Kernel.Modularity;

/// <summary>
/// 表示模块服务配置上下文。
/// </summary>
public sealed class ServiceConfigurationContext
{
    /// <summary>
    /// 初始化上下文实例。
    /// </summary>
    /// <param name="services">服务集合。</param>
    public ServiceConfigurationContext(IServiceCollection services)
    {
        Services = services;
    }

    /// <summary>
    /// 获取服务集合。
    /// </summary>
    public IServiceCollection Services { get; }
}
