// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XiHan.Framework.Localization.Abstractions.Enums;
using XiHan.Framework.Localization.Options;
using XiHan.Framework.Localization.Services;

namespace XiHan.Framework.Localization.Extensions.DependencyInjection;

/// <summary>
/// 本地化服务集合扩展
/// </summary>
public static class XiHanLocalizationServiceCollectionExtensions
{
    /// <summary>
    /// 添加 XiHan 本地化服务
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configuration"></param>
    /// <returns></returns>
    public static IServiceCollection AddXiHanLocalization(this IServiceCollection services, IConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddLocalization();
        services.AddOptions<XiHanLocalizationOptions>();

        if (configuration != null)
        {
            services.Configure<XiHanLocalizationOptions>(configuration.GetSection(XiHanLocalizationOptions.SectionName));
        }

        // 保留 ResourceManager 工厂作为兜底
        services.TryAddSingleton<ResourceManagerStringLocalizerFactory>(sp => new ResourceManagerStringLocalizerFactory(
            sp.GetRequiredService<IOptions<LocalizationOptions>>(),
            sp.GetRequiredService<ILoggerFactory>()));

        services.TryAddSingleton<JsonLocalizationResourceStore>();
        services.TryAddSingleton<IEnumLocalizationService, EnumLocalizationService>();
        services.Replace(ServiceDescriptor.Singleton<IStringLocalizerFactory, XiHanStringLocalizerFactory>());

        return services;
    }
}
