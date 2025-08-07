#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanApplicationBase
// Guid:6d8c9d68-9cd7-45b3-a8de-dcb04e7a5c7e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/26 19:09:17
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reflection;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Extensions.Internal;
using XiHan.Framework.Core.Logging;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Utils.Extensions;
using XiHan.Framework.Utils.Logging;
using XiHan.Framework.Utils.System;

namespace XiHan.Framework.Core.Application;

/// <summary>
/// 曦寒应用基类
/// </summary>
public class XiHanApplicationBase : IXiHanApplication
{
    private bool _configuredServices;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="startupModuleType"></param>
    /// <param name="services"></param>
    /// <param name="optionsAction"></param>
    internal XiHanApplicationBase(Type startupModuleType, IServiceCollection services, Action<XiHanApplicationCreationOptions>? optionsAction)
    {
        _ = Guard.NotNull(startupModuleType, nameof(startupModuleType));
        _ = Guard.NotNull(services, nameof(services));
        ConsoleLogger.SetIsDisplayHeader(false);
        ConsoleLogger.Rainbow(XiHan.Logo);
        ConsoleLogger.Info(XiHan.GetSummary());

        // 设置启动模块
        StartupModuleType = startupModuleType;
        Services = services;

        // 添加一个空的对象访问器，该访问器的值会在初始化的时候被赋值
        _ = services.TryAddObjectAccessor<IServiceProvider>();

        // 调用用户传入的配置委托
        XiHanApplicationCreationOptions options = new(services);
        optionsAction?.Invoke(options);

        ApplicationName = GetApplicationName(options);

        // 注册自己
        _ = services.AddSingleton<IXiHanApplication>(this);
        _ = services.AddSingleton<IApplicationInfoAccessor>(this);
        _ = services.AddSingleton<IModuleContainer>(this);
        _ = services.AddSingleton<IXiHanHostEnvironment>(new XiHanHostEnvironment
        {
            EnvironmentName = options.Environment
        });

        // 添加日志等基础设施组件
        services.AddCoreServices();
        // 添加核心的 XiHan 服务，主要是模块系统相关组件
        services.AddCoreServices(this, options);

        // 加载模块，并按照依赖关系排序，依次执行他们的生命周期方法
        Modules = LoadModules(services, options);

        if (!options.SkipConfigureServices)
        {
            ConfigureServices();
        }
        ConsoleLogger.SetIsDisplayHeader(true);
    }

    /// <summary>
    /// 应用程序启动(入口)模块的类型
    /// </summary>
    public Type StartupModuleType { get; }

    /// <summary>
    /// 所有服务注册的列表，应用程序初始化后，不能向这个集合添加新的服务
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// 应用程序根服务提供器，在初始化应用程序之前不能使用
    /// </summary>
    public IServiceProvider ServiceProvider { get; private set; } = default!;

    /// <summary>
    /// 模块
    /// </summary>
    public IReadOnlyList<IModuleDescriptor> Modules { get; }

    /// <summary>
    /// 应用名称
    /// </summary>
    public string? ApplicationName { get; }

    /// <summary>
    /// 实例 ID
    /// </summary>
    public string InstanceId { get; } = Guid.NewGuid().ToString();

    #region 初始化模块

    /// <summary>
    /// 记录初始化日志
    /// </summary>
    /// <param name="serviceProvider"></param>
    protected virtual void WriteInitLogs(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetService<ILogger<XiHanApplicationBase>>();
        if (logger is null)
        {
            return;
        }

        var initLogger = serviceProvider.GetRequiredService<IInitLoggerFactory>().Create<XiHanApplicationBase>();

        foreach (var entry in initLogger.Entries)
        {
            logger.Log(entry.LogLevel, entry.EventId, entry.State, entry.Exception, entry.Formatter);
        }

        initLogger.Entries.Clear();
    }

    /// <summary>
    /// 加载模块
    /// </summary>
    /// <param name="services"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    protected virtual IReadOnlyList<IModuleDescriptor> LoadModules(IServiceCollection services, XiHanApplicationCreationOptions options)
    {
        return services.GetSingletonInstance<IModuleLoader>().LoadModules(services, StartupModuleType, options.PlugInSources);
    }

    /// <summary>
    /// 设置服务提供器
    /// </summary>
    /// <param name="serviceProvider"></param>
    protected virtual void SetServiceProvider(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
        ServiceProvider.GetRequiredService<ObjectAccessor<IServiceProvider>>().Value = ServiceProvider;
    }

    /// <summary>
    /// 初始化模块，异步
    /// </summary>
    /// <returns></returns>
    protected virtual async Task InitializeModulesAsync()
    {
        using var scope = ServiceProvider.CreateScope();
        WriteInitLogs(scope.ServiceProvider);
        await scope.ServiceProvider.GetRequiredService<IModuleManager>().InitializeModulesAsync(new ApplicationInitializationContext(scope.ServiceProvider));
    }

    /// <summary>
    /// 初始化模块
    /// </summary>
    protected virtual void InitializeModules()
    {
        using var scope = ServiceProvider.CreateScope();
        WriteInitLogs(scope.ServiceProvider);
        scope.ServiceProvider.GetRequiredService<IModuleManager>().InitializeModules(new ApplicationInitializationContext(scope.ServiceProvider));
    }

    /// <summary>
    /// 获取应用程序名称
    /// </summary>
    /// <param name="options"></param>
    /// <returns></returns>
    private static string? GetApplicationName(XiHanApplicationCreationOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ApplicationName))
        {
            return options.ApplicationName!;
        }

        var configuration = options.Services.GetConfigurationOrNull();
        if (configuration is not null)
        {
            var appNameConfig = configuration["ApplicationName"];
            if (!string.IsNullOrWhiteSpace(appNameConfig))
            {
                return appNameConfig;
            }
        }

        var entryAssembly = Assembly.GetEntryAssembly();
        return entryAssembly?.GetName().Name;
    }

    /// <summary>
    /// 尝试设置环境
    /// </summary>
    /// <param name="services"></param>
    private static void TryToSetEnvironment(IServiceCollection services)
    {
        var hostEnvironment = services.GetSingletonInstance<IXiHanHostEnvironment>();
        if (hostEnvironment.EnvironmentName.IsNullOrWhiteSpace())
        {
            hostEnvironment.EnvironmentName = Environments.Production;
        }
    }

    #endregion 初始化模块

    #region 配置服务

    /// <summary>
    /// 配置服务
    /// </summary>
    public virtual async Task ConfigureServicesAsync()
    {
        CheckMultipleConfigureServices();

        ServiceConfigurationContext context = new(Services);
        _ = Services.AddSingleton(context);

        foreach (var module in Modules)
        {
            if (module.Instance is XiHanModule xihanModule)
            {
                xihanModule.ServiceConfigurationContext = context;
            }
        }

        // PreConfigureServices
        foreach (var module in Modules.Where(m => m.Instance is IPreConfigureServices))
        {
            try
            {
                await ((IPreConfigureServices)module.Instance).PreConfigureServicesAsync(context);
            }
            catch (Exception ex)
            {
                throw new InitializationException($"在模块 {module.Type.AssemblyQualifiedName} 的 {nameof(IPreConfigureServices.PreConfigureServicesAsync)} 阶段发生错误。查看集成异常以获取详细信息。", ex);
            }
        }

        HashSet<Assembly> assemblies = [];

        // ConfigureServices
        foreach (var module in Modules)
        {
            if (module.Instance is XiHanModule { SkipAutoServiceRegistration: false })
            {
                foreach (var assembly in module.AllAssemblies)
                {
                    if (assemblies.Contains(assembly))
                    {
                        continue;
                    }

                    _ = Services.AddAssembly(assembly);
                    _ = assemblies.Add(assembly);
                }
            }

            try
            {
                await module.Instance.ConfigureServicesAsync(context);
            }
            catch (Exception ex)
            {
                throw new InitializationException($"在模块 {module.Type.AssemblyQualifiedName} 的 {nameof(IXiHanModule.ConfigureServicesAsync)} 阶段发生了一个错误。查看集成异常以获取详细信息。", ex);
            }
        }

        // PostConfigureServices
        foreach (var module in Modules.Where(m => m.Instance is IPostConfigureServices))
        {
            try
            {
                await ((IPostConfigureServices)module.Instance).PostConfigureServicesAsync(context);
            }
            catch (Exception ex)
            {
                throw new InitializationException($"在模块 {module.Type.AssemblyQualifiedName} 的 {nameof(IPostConfigureServices.PostConfigureServicesAsync)} 阶段发生了一个错误。查看集成异常以了解详细信息。", ex);
            }
        }

        foreach (var module in Modules)
        {
            if (module.Instance is XiHanModule xihanModule)
            {
                xihanModule.ServiceConfigurationContext = null!;
            }
        }

        _configuredServices = true;

        TryToSetEnvironment(Services);
    }

    /// <summary>
    /// 配置服务
    /// </summary>
    /// <exception cref="InitializationException"></exception>
    public virtual void ConfigureServices()
    {
        CheckMultipleConfigureServices();

        ServiceConfigurationContext context = new(Services);
        _ = Services.AddSingleton(context);

        foreach (var module in Modules)
        {
            if (module.Instance is XiHanModule xihanModule)
            {
                xihanModule.ServiceConfigurationContext = context;
            }
        }

        // PreConfigureServices
        foreach (var module in Modules.Where(m => m.Instance is IPreConfigureServices))
        {
            try
            {
                ((IPreConfigureServices)module.Instance).PreConfigureServices(context);
            }
            catch (Exception ex)
            {
                throw new InitializationException($"在模块 {module.Type.AssemblyQualifiedName} 的 {nameof(IPreConfigureServices.PreConfigureServices)} 阶段发生了一个错误。查看集成异常以获取详细信息。", ex);
            }
        }

        HashSet<Assembly> assemblies = [];

        // ConfigureServices
        foreach (var module in Modules)
        {
            if (module.Instance is XiHanModule { SkipAutoServiceRegistration: false })
            {
                foreach (var assembly in module.AllAssemblies)
                {
                    if (assemblies.Contains(assembly))
                    {
                        continue;
                    }

                    _ = Services.AddAssembly(assembly);
                    _ = assemblies.Add(assembly);
                }
            }

            try
            {
                module.Instance.ConfigureServices(context);
            }
            catch (Exception ex)
            {
                throw new InitializationException($"在模块 {module.Type.AssemblyQualifiedName} 的 {nameof(IXiHanModule.ConfigureServices)} 阶段发生了一个错误。查看集成异常以获取详细信息。", ex);
            }
        }

        // PostConfigureServices
        foreach (var module in Modules.Where(m => m.Instance is IPostConfigureServices))
        {
            try
            {
                ((IPostConfigureServices)module.Instance).PostConfigureServices(context);
            }
            catch (Exception ex)
            {
                throw new InitializationException($"在模块 {module.Type.AssemblyQualifiedName} 的 {nameof(IPostConfigureServices.PostConfigureServices)} 阶段发生了一个错误。查看集成异常以获取详细信息。", ex);
            }
        }

        foreach (var module in Modules)
        {
            if (module.Instance is XiHanModule xihanModule)
            {
                xihanModule.ServiceConfigurationContext = null!;
            }
        }

        _configuredServices = true;

        TryToSetEnvironment(Services);
    }

    /// <summary>
    /// 检查多个配置服务
    /// </summary>
    /// <exception cref="InitializationException"></exception>
    private void CheckMultipleConfigureServices()
    {
        if (_configuredServices)
        {
            throw new InitializationException("服务已被配置！如果调用 ConfigureServicesAsync 方法，必须在此之前将 ApplicationCreationOptions.SkipConfigureServices 设置为 true");
        }
    }

    #endregion 配置服务

    #region 关闭应用

    /// <summary>
    /// 关闭应用
    /// </summary>
    public virtual async Task ShutdownAsync()
    {
        using var scope = ServiceProvider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IModuleManager>().ShutdownModulesAsync(new ApplicationShutdownContext(scope.ServiceProvider));
    }

    /// <summary>
    /// 关闭应用
    /// </summary>
    public virtual void Shutdown()
    {
        using var scope = ServiceProvider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IModuleManager>().ShutdownModules(new ApplicationShutdownContext(scope.ServiceProvider));
    }

    /// <summary>
    /// 释放
    /// </summary>
    public virtual void Dispose()
    {
        //TODO: 如果之前没有完成，就进行关闭?

        GC.SuppressFinalize(this);
    }

    #endregion 关闭应用
}
