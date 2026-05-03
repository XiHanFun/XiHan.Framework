using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Bootstrap.Abstractions.Application;
using XiHan.Framework.Kernel.Modularity;

namespace XiHan.Framework.Bootstrap.Application;

/// <summary>
/// 提供应用工厂默认实现。
/// </summary>
public static class XiHanApplicationFactory
{
    /// <summary>
    /// 创建内部服务提供器模式的应用。
    /// </summary>
    public static IXiHanApplicationWithInternalServiceProvider Create<TStartupModule>(
        Action<XiHanApplicationCreationOptions>? optionsAction = null)
        where TStartupModule : IXiHanModule
    {
        return Create(typeof(TStartupModule), optionsAction);
    }

    /// <summary>
    /// 创建内部服务提供器模式的应用。
    /// </summary>
    public static IXiHanApplicationWithInternalServiceProvider Create(
        Type startupModuleType,
        Action<XiHanApplicationCreationOptions>? optionsAction = null)
    {
        return new XiHanApplicationWithInternalServiceProvider(startupModuleType, optionsAction);
    }

    /// <summary>
    /// 创建外部服务提供器模式的应用。
    /// </summary>
    public static IXiHanApplicationWithExternalServiceProvider Create<TStartupModule>(
        IServiceCollection services,
        Action<XiHanApplicationCreationOptions>? optionsAction = null)
        where TStartupModule : IXiHanModule
    {
        return Create(typeof(TStartupModule), services, optionsAction);
    }

    /// <summary>
    /// 创建外部服务提供器模式的应用。
    /// </summary>
    public static IXiHanApplicationWithExternalServiceProvider Create(
        Type startupModuleType,
        IServiceCollection services,
        Action<XiHanApplicationCreationOptions>? optionsAction = null)
    {
        return new XiHanApplicationWithExternalServiceProvider(startupModuleType, services, optionsAction);
    }

    /// <summary>
    /// 异步创建内部服务提供器模式的应用。
    /// </summary>
    public static async Task<IXiHanApplicationWithInternalServiceProvider> CreateAsync<TStartupModule>(
        Action<XiHanApplicationCreationOptions>? optionsAction = null)
        where TStartupModule : IXiHanModule
    {
        return await CreateAsync(typeof(TStartupModule), optionsAction);
    }

    /// <summary>
    /// 异步创建内部服务提供器模式的应用。
    /// </summary>
    public static async Task<IXiHanApplicationWithInternalServiceProvider> CreateAsync(
        Type startupModuleType,
        Action<XiHanApplicationCreationOptions>? optionsAction = null)
    {
        var application = new XiHanApplicationWithInternalServiceProvider(startupModuleType, options =>
        {
            options.SkipConfigureServices = true;
            optionsAction?.Invoke(options);
        });

        await application.ConfigureServicesAsync();
        return application;
    }

    /// <summary>
    /// 异步创建外部服务提供器模式的应用。
    /// </summary>
    public static async Task<IXiHanApplicationWithExternalServiceProvider> CreateAsync<TStartupModule>(
        IServiceCollection services,
        Action<XiHanApplicationCreationOptions>? optionsAction = null)
        where TStartupModule : IXiHanModule
    {
        return await CreateAsync(typeof(TStartupModule), services, optionsAction);
    }

    /// <summary>
    /// 异步创建外部服务提供器模式的应用。
    /// </summary>
    public static async Task<IXiHanApplicationWithExternalServiceProvider> CreateAsync(
        Type startupModuleType,
        IServiceCollection services,
        Action<XiHanApplicationCreationOptions>? optionsAction = null)
    {
        var application = new XiHanApplicationWithExternalServiceProvider(startupModuleType, services, options =>
        {
            options.SkipConfigureServices = true;
            optionsAction?.Invoke(options);
        });

        await application.ConfigureServicesAsync();
        return application;
    }
}
