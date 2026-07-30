// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Core.Extensions.DependencyInjection;

/// <summary>
/// 服务容器应用程序扩展
/// </summary>
public static class ServiceCollectionApplicationExtensions
{
    /// <summary>
    /// 添加应用程序
    /// </summary>
    /// <typeparam name="TStartupModule"></typeparam>
    /// <param name="services"></param>
    /// <param name="optionsAction"></param>
    /// <returns></returns>
    public static IXiHanApplicationWithExternalServiceProvider AddApplication<TStartupModule>(
        this IServiceCollection services,
        Action<XiHanApplicationCreationOptions>? optionsAction = null)
        where TStartupModule : IXiHanModule
    {
        return XiHanApplicationFactory.Create<TStartupModule>(services, optionsAction);
    }

    /// <summary>
    /// 添加应用程序
    /// </summary>
    /// <param name="services"></param>
    /// <param name="startupModuleType"></param>
    /// <param name="optionsAction"></param>
    /// <returns></returns>
    public static IXiHanApplicationWithExternalServiceProvider AddApplication(
        this IServiceCollection services,
        Type startupModuleType,
        Action<XiHanApplicationCreationOptions>? optionsAction = null)
    {
        return XiHanApplicationFactory.Create(startupModuleType, services, optionsAction);
    }

    /// <summary>
    /// 添加应用程序
    /// </summary>
    /// <typeparam name="TStartupModule"></typeparam>
    /// <param name="services"></param>
    /// <param name="optionsAction"></param>
    /// <returns></returns>
    public static async Task<IXiHanApplicationWithExternalServiceProvider> AddApplicationAsync<TStartupModule>(
        this IServiceCollection services,
        Action<XiHanApplicationCreationOptions>? optionsAction = null)
        where TStartupModule : IXiHanModule
    {
        return await XiHanApplicationFactory.CreateAsync<TStartupModule>(services, optionsAction);
    }

    /// <summary>
    /// 添加应用程序
    /// </summary>
    /// <param name="services"></param>
    /// <param name="startupModuleType"></param>
    /// <param name="optionsAction"></param>
    /// <returns></returns>
    public static async Task<IXiHanApplicationWithExternalServiceProvider> AddApplicationAsync(
        this IServiceCollection services,
        Type startupModuleType,
        Action<XiHanApplicationCreationOptions>? optionsAction = null)
    {
        return await XiHanApplicationFactory.CreateAsync(startupModuleType, services, optionsAction);
    }

    /// <summary>
    /// 获取应用程序名称
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static string? GetApplicationName(this IServiceCollection services)
    {
        return services.GetSingletonInstance<IApplicationInfoAccessor>().ApplicationName;
    }

    /// <summary>
    /// 获取应用程序实例唯一标识
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static string GetApplicationInstanceId(this IServiceCollection services)
    {
        return services.GetSingletonInstance<IApplicationInfoAccessor>().InstanceId;
    }

    /// <summary>
    /// 获取应用程序环境
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IXiHanHostEnvironment GetXiHanHostEnvironment(this IServiceCollection services)
    {
        return services.GetSingletonInstance<IXiHanHostEnvironment>();
    }
}
