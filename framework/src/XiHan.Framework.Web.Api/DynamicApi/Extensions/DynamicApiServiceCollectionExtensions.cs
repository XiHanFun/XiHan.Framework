#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DynamicApiServiceCollectionExtensions
// Guid:p6q7r8s9-t0u1-4v2w-3x4y-5z6a7b8c9d0e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/24 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Web.Api.DynamicApi.Configuration;
using XiHan.Framework.Web.Api.DynamicApi.Controllers;
using XiHan.Framework.Web.Api.DynamicApi.Conventions;

namespace XiHan.Framework.Web.Api.DynamicApi.Extensions;

/// <summary>
/// 动态 API 服务集合扩展
/// </summary>
public static class DynamicApiServiceCollectionExtensions
{
    /// <summary>
    /// 添加动态 API
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configureOptions">配置选项</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddDynamicApi(
        this IServiceCollection services,
        Action<DynamicApiOptions>? configureOptions = null)
    {
        // 配置选项
        var options = new DynamicApiOptions();
        configureOptions?.Invoke(options);
        services.AddSingleton(options);

        // 注册约定
        services.AddSingleton<IDynamicApiConvention>(sp => new DefaultDynamicApiConvention(options));

        // 添加动态控制器特性提供者
        services.AddControllers()
            .ConfigureApplicationPartManager((manager) =>
            {
                // 这里我们需要在构建服务提供者后才能传递，所以使用延迟初始化的方式
                var provider = services.BuildServiceProvider();
                manager.FeatureProviders.Add(new DynamicApiControllerFeatureProvider(provider));
            });

        return services;
    }

    /// <summary>
    /// 配置动态 API 约定
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configurator">约定配置器</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection ConfigureDynamicApiConventions(
        this IServiceCollection services,
        Action<DynamicApiConventionOptions> configurator)
    {
        services.Configure<DynamicApiOptions>(options =>
        {
            configurator(options.Conventions);
        });

        return services;
    }

    /// <summary>
    /// 配置动态 API 路由
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configurator">路由配置器</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection ConfigureDynamicApiRoutes(
        this IServiceCollection services,
        Action<DynamicApiRouteOptions> configurator)
    {
        services.Configure<DynamicApiOptions>(options =>
        {
            configurator(options.Routes);
        });

        return services;
    }
}
