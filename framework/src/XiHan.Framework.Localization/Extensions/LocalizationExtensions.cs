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
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using XiHan.Framework.Localization.Core;
using XiHan.Framework.Localization.Options;
using XiHan.Framework.Localization.Provider;
using XiHan.Framework.Localization.Settings;
using XiHan.Framework.Localization.VirtualFileSystem;
using XiHan.Framework.Settings.Definitions;

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
        return services.AddXiHanLocalization(_ => { });
    }

    /// <summary>
    /// 注册虚拟文件JSON本地化系统
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置选项</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanLocalization(
        this IServiceCollection services,
        Action<XiHanLocalizationOptions> configure)
    {
        // 配置选项
        _ = services.Configure(configure);

        // 添加标准化本地化服务
        _ = services.AddLocalization();

        // 注册核心服务
        services.TryAddSingleton<IVirtualFileResourceFactory, VirtualFileResourceFactory>();
        services.TryAddSingleton<ILocalizationResourceManager, LocalizationResourceManager>();
        services.TryAddSingleton<IResourceStringProvider, JsonLocalizationResourceProvider>();
        services.TryAddSingleton<ISettingDefinitionProvider, LocalizationSettingDefinitionProvider>();

        // 注册资源管理器和工厂
        services.TryAddSingleton<IStringLocalizerFactory, XiHanStringLocalizerFactory>();

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

    /// <summary>
    /// 使用指定文化获取本地化字符串
    /// </summary>
    /// <param name="localizer">本地化器</param>
    /// <param name="name">资源名</param>
    /// <param name="culture">文化</param>
    /// <returns>本地化字符串</returns>
    public static LocalizedString WithCulture(this IStringLocalizer localizer, string name, string culture)
    {
        if (localizer is IXiHanStringLocalizer xiHanLocalizer)
        {
            return xiHanLocalizer.GetWithCulture(name, culture);
        }

        // 如果不是 IXiHanStringLocalizer，回退到标准行为
        return localizer[name];
    }

    /// <summary>
    /// 使用指定文化获取本地化字符串，支持格式化参数
    /// </summary>
    /// <param name="localizer">本地化器</param>
    /// <param name="name">资源名</param>
    /// <param name="culture">文化</param>
    /// <param name="arguments">格式化参数</param>
    /// <returns>本地化字符串</returns>
    public static LocalizedString WithCulture(this IStringLocalizer localizer, string name, string culture, params object[] arguments)
    {
        if (localizer is IXiHanStringLocalizer xiHanLocalizer)
        {
            return xiHanLocalizer.GetWithCulture(name, culture, arguments);
        }

        // 如果不是 IXiHanStringLocalizer，回退到标准行为
        return localizer[name, arguments];
    }
}
