#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanApplicationFactory
// Guid:f93fe1e2-70c3-45a8-a53e-1a3c7318c6c0
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/28 4:10:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Core.Application;

/// <summary>
/// 曦寒应用工厂
/// </summary>
public static class XiHanApplicationFactory
{
    #region 创建集成服务应用

    /// <summary>
    /// 创建集成服务应用，异步
    /// </summary>
    /// <typeparam name="TStartupModule"></typeparam>
    /// <param name="optionsAction"></param>
    /// <returns></returns>
    public static async Task<IXiHanApplicationWithInternalServiceProvider> CreateAsync<TStartupModule>(
        Action<XiHanApplicationCreationOptions>? optionsAction = null)
        where TStartupModule : IXiHanModule
    {
        var app = Create<TStartupModule>(options =>
        {
            options.SkipConfigureServices = true;
            optionsAction?.Invoke(options);
        });
        await app.ConfigureServicesAsync();
        return app;
    }

    /// <summary>
    /// 创建集成服务应用，异步
    /// </summary>
    /// <param name="startupModuleType"></param>
    /// <param name="optionsAction"></param>
    /// <returns></returns>
    public static async Task<IXiHanApplicationWithInternalServiceProvider> CreateAsync(
        [NotNull] Type startupModuleType,
        Action<XiHanApplicationCreationOptions>? optionsAction = null)
    {
        XiHanApplicationWithInternalServiceProvider? app = new(startupModuleType, options =>
        {
            options.SkipConfigureServices = true;
            optionsAction?.Invoke(options);
        });
        await app.ConfigureServicesAsync();
        return app;
    }

    /// <summary>
    /// 创建集成服务应用
    /// </summary>
    /// <typeparam name="TStartupModule"></typeparam>
    /// <param name="optionsAction"></param>
    /// <returns></returns>
    public static IXiHanApplicationWithInternalServiceProvider Create<TStartupModule>(
        Action<XiHanApplicationCreationOptions>? optionsAction = null)
        where TStartupModule : IXiHanModule
    {
        return Create(typeof(TStartupModule), optionsAction);
    }

    /// <summary>
    /// 创建集成服务应用
    /// </summary>
    /// <param name="startupModuleType"></param>
    /// <param name="optionsAction"></param>
    /// <returns></returns>
    public static IXiHanApplicationWithInternalServiceProvider Create(
        [NotNull] Type startupModuleType,
        Action<XiHanApplicationCreationOptions>? optionsAction = null)
    {
        return new XiHanApplicationWithInternalServiceProvider(startupModuleType, optionsAction);
    }

    #endregion

    #region 创建外部服务应用

    /// <summary>
    /// 创建外部服务应用，异步
    /// </summary>
    /// <typeparam name="TStartupModule"></typeparam>
    /// <param name="services"></param>
    /// <param name="optionsAction"></param>
    /// <returns></returns>
    public static async Task<IXiHanApplicationWithExternalServiceProvider> CreateAsync<TStartupModule>(
        [NotNull] IServiceCollection services,
        Action<XiHanApplicationCreationOptions>? optionsAction = null)
        where TStartupModule : IXiHanModule
    {
        var app = Create<TStartupModule>(services, options =>
        {
            options.SkipConfigureServices = true;
            optionsAction?.Invoke(options);
        });
        await app.ConfigureServicesAsync();
        return app;
    }

    /// <summary>
    /// 创建外部服务应用，异步
    /// </summary>
    /// <param name="startupModuleType"></param>
    /// <param name="services"></param>
    /// <param name="optionsAction"></param>
    /// <returns></returns>
    public static async Task<IXiHanApplicationWithExternalServiceProvider> CreateAsync(
        [NotNull] Type startupModuleType,
        [NotNull] IServiceCollection services,
        Action<XiHanApplicationCreationOptions>? optionsAction = null)
    {
        XiHanApplicationWithExternalServiceProvider? app = new(startupModuleType, services, options =>
        {
            options.SkipConfigureServices = true;
            optionsAction?.Invoke(options);
        });
        await app.ConfigureServicesAsync();
        return app;
    }

    /// <summary>
    /// 创建外部服务应用
    /// </summary>
    /// <typeparam name="TStartupModule"></typeparam>
    /// <param name="services"></param>
    /// <param name="optionsAction"></param>
    /// <returns></returns>
    public static IXiHanApplicationWithExternalServiceProvider Create<TStartupModule>(
        [NotNull] IServiceCollection services,
        Action<XiHanApplicationCreationOptions>? optionsAction = null)
        where TStartupModule : IXiHanModule
    {
        return Create(typeof(TStartupModule), services, optionsAction);
    }

    /// <summary>
    /// 创建外部服务应用
    /// </summary>
    /// <param name="startupModuleType"></param>
    /// <param name="services"></param>
    /// <param name="optionsAction"></param>
    /// <returns></returns>
    public static IXiHanApplicationWithExternalServiceProvider Create(
        [NotNull] Type startupModuleType,
        [NotNull] IServiceCollection services,
        Action<XiHanApplicationCreationOptions>? optionsAction = null)
    {
        return new XiHanApplicationWithExternalServiceProvider(startupModuleType, services, optionsAction);
    }

    #endregion
}
