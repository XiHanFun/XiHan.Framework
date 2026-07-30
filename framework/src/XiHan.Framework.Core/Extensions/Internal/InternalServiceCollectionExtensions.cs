// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.Extensions.Configuration;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Logging;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Core.Reflections;
using XiHan.Framework.Core.SimpleStateChecking;

namespace XiHan.Framework.Core.Extensions.Internal;

/// <summary>
/// 集成服务集合扩展方法
/// </summary>
internal static class InternalServiceCollectionExtensions
{
    /// <summary>
    /// 添加核心服务
    /// </summary>
    /// <param name="services"></param>
    internal static void AddCoreServices(this IServiceCollection services)
    {
        services.AddOptions();
        services.AddLogging();
        services.AddLocalization();
    }

    /// <summary>
    /// 添加核心服务
    /// </summary>
    /// <param name="services"></param>
    /// <param name="application"></param>
    /// <param name="applicationCreationOptions"></param>
    internal static void AddCoreServices(this IServiceCollection services, IXiHanApplication application, XiHanApplicationCreationOptions applicationCreationOptions)
    {
        var moduleLoader = new ModuleLoader();
        var assemblyFinder = new AssemblyFinder(application);
        var typeFinder = new TypeFinder(assemblyFinder);

        if (!services.IsAdded<IConfiguration>())
        {
            services.ReplaceConfiguration(ConfigurationHelper.BuildConfiguration(applicationCreationOptions.Configuration));
        }

        // 模块基础服务
        services.TryAddSingleton<IAssemblyFinder>(assemblyFinder);
        services.TryAddSingleton<ITypeFinder>(typeFinder);
        services.TryAddSingleton<IInitLoggerFactory>(new DefaultInitLoggerFactory());
        services.TryAddSingleton<IModuleLoader>(moduleLoader);

        // 属性或字段自动注入服务
        services.AddSingleton<AutowiredServiceHandler>();

        // 核心应用服务
        services.AddAssemblyOf<IXiHanApplication>();

        // 状态检查服务
        services.AddTransient(typeof(ISimpleStateCheckerManager<>), typeof(SimpleStateCheckerManager<>));

        // 模块生命周期
        services.Configure<XiHanModuleLifecycleOptions>(options =>
        {
            options.Contributors.Add<OnPreApplicationInitializationModuleLifecycleContributor>();
            options.Contributors.Add<OnApplicationInitializationModuleLifecycleContributor>();
            options.Contributors.Add<OnPostApplicationInitializationModuleLifecycleContributor>();
            options.Contributors.Add<OnApplicationShutdownModuleLifecycleContributor>();
        });
    }
}
