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

using Microsoft.AspNetCore.Mvc.ApplicationParts;

using System.Reflection;
using XiHan.Framework.Application.Services.Abstracts;
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

        // 自动发现并注册包含应用服务的程序集
        services.AddControllers()
            .ConfigureApplicationPartManager((manager) =>
            {
                // 扫描所有已加载的程序集，查找包含 IApplicationService 的程序集
                var assemblies = DiscoverApplicationServiceAssemblies();

                var addedCount = 0;
                foreach (var assembly in assemblies)
                {
                    // 检查是否已添加
                    var assemblyName = assembly.GetName().Name;
                    if (!manager.ApplicationParts.Any(p => p.Name == assemblyName))
                    {
                        manager.ApplicationParts.Add(new AssemblyPart(assembly));
                        addedCount++;
                    }
                }

                // 输出发现的程序集信息（开发时可以取消注释查看）
                // Console.WriteLine($"[动态 API] 自动发现并注册了 {addedCount} 个包含应用服务的程序集");

                // 添加动态 API 控制器特性提供者
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

    /// <summary>
    /// 发现包含应用服务的程序集
    /// </summary>
    /// <returns>包含应用服务的程序集列表</returns>
    private static List<Assembly> DiscoverApplicationServiceAssemblies()
    {
        var assemblies = new List<Assembly>();
        var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (var assembly in loadedAssemblies)
        {
            // 跳过系统程序集
            if (IsSystemAssembly(assembly))
            {
                continue;
            }

            try
            {
                // 检查程序集中是否有实现 IApplicationService 的类型
                var hasApplicationService = assembly.GetTypes()
                    .Any(type => type.IsClass &&
                                !type.IsAbstract &&
                                typeof(IApplicationService).IsAssignableFrom(type));

                if (hasApplicationService)
                {
                    assemblies.Add(assembly);
                }
            }
            catch (ReflectionTypeLoadException)
            {
                // 忽略无法加载的程序集
            }
            catch (Exception)
            {
                // 忽略其他异常
            }
        }

        return assemblies;
    }

    /// <summary>
    /// 判断是否是系统程序集
    /// </summary>
    /// <param name="assembly">程序集</param>
    /// <returns>是否是系统程序集</returns>
    private static bool IsSystemAssembly(Assembly assembly)
    {
        var name = assembly.GetName().Name ?? string.Empty;

        // 跳过 Microsoft、System 等系统程序集
        return name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("Serilog", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("Newtonsoft", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("System", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("mscorlib", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("netstandard", StringComparison.OrdinalIgnoreCase);
    }
}
