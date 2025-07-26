#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:InternalServiceCollectionExtensions
// Guid:be714c28-5f84-4057-bb41-36f075962e28
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/26 19:32:35
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
        _ = services.AddOptions();
        _ = services.AddLogging();
        _ = services.AddLocalization();
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
            _ = services.ReplaceConfiguration(ConfigurationHelper.BuildConfiguration(applicationCreationOptions.Configuration));
        }

        services.TryAddSingleton<IAssemblyFinder>(assemblyFinder);
        services.TryAddSingleton<ITypeFinder>(typeFinder);
        services.TryAddSingleton<IInitLoggerFactory>(new DefaultInitLoggerFactory());
        services.TryAddSingleton<IModuleLoader>(moduleLoader);

        // 属性或字段自动注入服务
        _ = services.AddSingleton<AutowiredServiceHandler>();

        _ = services.AddAssemblyOf<IXiHanApplication>();

        _ = services.AddTransient(typeof(ISimpleStateCheckerManager<>), typeof(SimpleStateCheckerManager<>));

        _ = services.Configure<XiHanModuleLifecycleOptions>(options =>
        {
            options.Contributors.Add<OnPreApplicationInitializationModuleLifecycleContributor>();
            options.Contributors.Add<OnApplicationInitializationModuleLifecycleContributor>();
            options.Contributors.Add<OnPostApplicationInitializationModuleLifecycleContributor>();
            options.Contributors.Add<OnApplicationShutdownModuleLifecycleContributor>();
        });
    }
}
