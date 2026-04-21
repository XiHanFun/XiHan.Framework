using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Bootstrap.Abstractions.Application;
using XiHan.Framework.Bootstrap.Application;
using XiHan.Framework.Kernel.Modularity;

namespace XiHan.Framework.Bootstrap.Extensions.DependencyInjection;

/// <summary>
/// 提供应用接入相关的服务集合扩展。
/// </summary>
public static class ServiceCollectionApplicationExtensions
{
    /// <summary>
    /// 添加应用并返回外部服务提供器模式的应用对象。
    /// </summary>
    public static IXiHanApplicationWithExternalServiceProvider AddApplication<TStartupModule>(
        this IServiceCollection services,
        Action<XiHanApplicationCreationOptions>? optionsAction = null)
        where TStartupModule : IXiHanModule
    {
        return XiHanApplicationFactory.Create<TStartupModule>(services, optionsAction);
    }

    /// <summary>
    /// 添加应用并返回外部服务提供器模式的应用对象。
    /// </summary>
    public static IXiHanApplicationWithExternalServiceProvider AddApplication(
        this IServiceCollection services,
        Type startupModuleType,
        Action<XiHanApplicationCreationOptions>? optionsAction = null)
    {
        return XiHanApplicationFactory.Create(startupModuleType, services, optionsAction);
    }

    /// <summary>
    /// 异步添加应用并返回外部服务提供器模式的应用对象。
    /// </summary>
    public static Task<IXiHanApplicationWithExternalServiceProvider> AddApplicationAsync<TStartupModule>(
        this IServiceCollection services,
        Action<XiHanApplicationCreationOptions>? optionsAction = null)
        where TStartupModule : IXiHanModule
    {
        return XiHanApplicationFactory.CreateAsync<TStartupModule>(services, optionsAction);
    }

    /// <summary>
    /// 异步添加应用并返回外部服务提供器模式的应用对象。
    /// </summary>
    public static Task<IXiHanApplicationWithExternalServiceProvider> AddApplicationAsync(
        this IServiceCollection services,
        Type startupModuleType,
        Action<XiHanApplicationCreationOptions>? optionsAction = null)
    {
        return XiHanApplicationFactory.CreateAsync(startupModuleType, services, optionsAction);
    }
}
