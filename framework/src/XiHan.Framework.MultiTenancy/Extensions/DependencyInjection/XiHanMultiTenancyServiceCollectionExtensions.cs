// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.MultiTenancy.ConfigurationStore;
using XiHan.Framework.MultiTenancy.Features;
using XiHan.Framework.Settings.Options;
using XiHan.Framework.Settings.Providers;
using XiHan.Framework.Utils.Collections;

namespace XiHan.Framework.MultiTenancy.Extensions.DependencyInjection;

/// <summary>
/// 曦寒多租户服务集合扩展
/// </summary>
public static class XiHanMultiTenancyServiceCollectionExtensions
{
    /// <summary>
    /// 添加曦寒多租户服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanMultiTenancy(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ICurrentTenantAccessor>(AsyncLocalCurrentTenantAccessor.Instance);

        services.Configure<XiHanDefaultTenantStoreOptions>(configuration.GetSection(XiHanDefaultTenantStoreOptions.SectionName));
        services.TryAddSingleton<ITenantStore, DefaultTenantStore>();

        services.Configure<XiHanSettingOptions>(options =>
        {
            options.ValueProviders.InsertAfter(t => t == typeof(GlobalSettingValueProvider), typeof(TenantSettingValueProvider));
        });

        services.Configure<XiHanTenantResolveOptions>(options =>
        {
            options.TenantResolvers.Insert(0, new CurrentUserTenantResolveContributor());
        });
        services.AddTransient<ITenantFeatureChecker, TenantFeatureChecker>();

        return services;
    }
}
