#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LocalizationExtensions
// Guid:9b8c7d6e-5f4a-3e2d-1f0a-9b8c7d6e5f4a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/27 13:25:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using XiHan.Framework.Localization.Core;
using XiHan.Framework.Localization.JsonLocalization;
using XiHan.Framework.Localization.Resources;

namespace XiHan.Framework.Localization.Extensions;

/// <summary>
/// 本地化扩展方法
/// </summary>
public static class LocalizationExtensions
{
    /// <summary>
    /// 注册虚拟文件JSON本地化系统
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanLocalization(this IServiceCollection services)
    {
        // 添加标准化本地化服务
        _ = services.AddLocalization();

        // 添加JSON资源提供程序
        _ = services.AddSingleton<IResourceStringProvider, JsonLocalizationResourceProvider>();

        // 注册资源管理器和工厂
        _ = services.AddSingleton<IStringLocalizerFactory, XiHanStringLocalizerFactory>();

        return services;
    }

    /// <summary>
    /// 获取特定资源类型的本地化器
    /// </summary>
    /// <typeparam name="T">资源类型</typeparam>
    /// <param name="provider">服务提供者</param>
    /// <returns>字符串本地化器</returns>
    public static IXiHanStringLocalizer GetXiHanLocalizer<T>(this IServiceProvider provider)
    {
        var factory = provider.GetRequiredService<IStringLocalizerFactory>();
        return (IXiHanStringLocalizer)factory.Create(typeof(T));
    }

    /// <summary>
    /// 获取特定名称的本地化器
    /// </summary>
    /// <param name="provider">服务提供者</param>
    /// <param name="resourceName">资源名称</param>
    /// <returns>字符串本地化器</returns>
    public static IXiHanStringLocalizer GetXiHanLocalizer(this IServiceProvider provider, string resourceName)
    {
        var resourceManager = provider.GetRequiredService<ILocalizationResourceManager>();
        var factory = provider.GetRequiredService<IStringLocalizerFactory>();

        var resource = resourceManager.GetResource(resourceName);
        var resourceStringProvider = provider.GetRequiredService<IResourceStringProvider>();

        return new XiHanStringLocalizer(resource, factory, resourceStringProvider);
    }
}
