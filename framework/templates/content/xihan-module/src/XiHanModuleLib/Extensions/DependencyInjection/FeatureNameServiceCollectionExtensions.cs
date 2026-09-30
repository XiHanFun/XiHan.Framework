using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHanModuleLib.Options;
using XiHanModuleLib.Services;

namespace XiHanModuleLib.Extensions.DependencyInjection;

/// <summary>
/// FeatureName 服务集合扩展
/// </summary>
public static class FeatureNameServiceCollectionExtensions
{
    /// <summary>
    /// 添加 FeatureName 服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddFeatureName(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<FeatureNameOptions>(configuration.GetSection(FeatureNameOptions.SectionName));
        services.TryAddSingleton<IFeatureNameService, DefaultFeatureNameService>();

        return services;
    }
}
