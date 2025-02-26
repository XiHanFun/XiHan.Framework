#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LocalizationResourceExtensions
// Guid:7f6e5d4c-3b2a-1c0d-9e8f-7f6e5d4c3b2a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/27 14:35:28
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using XiHan.Framework.Localization.Core;
using XiHan.Framework.Localization.Options;
using XiHan.Framework.Localization.Resources;

namespace XiHan.Framework.Localization.Extensions;

/// <summary>
/// 本地化资源扩展方法
/// </summary>
public static class LocalizationResourceExtensions
{
    /// <summary>
    /// 添加虚拟文件路径下的所有本地化资源
    /// </summary>
    /// <param name="resourceManager">本地化资源管理器</param>
    /// <param name="basePath">基础虚拟路径</param>
    /// <returns>添加的资源列表</returns>
    public static IReadOnlyList<ILocalizationResource> AddVirtualFileResources(
        this ILocalizationResourceManager resourceManager,
        string basePath)
    {
        if (string.IsNullOrEmpty(basePath))
        {
            throw new ArgumentException("基础路径不能为空", nameof(basePath));
        }

        var resources = new List<ILocalizationResource>();
        var resource = resourceManager.AddVirtualFileResource(basePath);
        resources.Add(resource);

        return resources.AsReadOnly();
    }

    /// <summary>
    /// 根据资源名称获取本地化资源，如果不存在则创建
    /// </summary>
    /// <param name="resourceManager">本地化资源管理器</param>
    /// <param name="resourceName">资源名称</param>
    /// <param name="virtualPath">虚拟路径，如果资源不存在则使用此路径创建</param>
    /// <returns>本地化资源</returns>
    public static ILocalizationResource GetOrAddResource(
        this ILocalizationResourceManager resourceManager,
        string resourceName,
        string virtualPath)
    {
        var resource = resourceManager.GetResource(resourceName);
        return resource ?? resourceManager.AddVirtualFileResource(virtualPath);
    }

    /// <summary>
    /// 使用指定的选项配置本地化服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configureOptions">配置选项的委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanLocalizationWithOptions(
        this IServiceCollection services,
        Action<XiHanLocalizationOptions> configureOptions)
    {
        _ = services.AddXiHanLocalization();
        _ = services.Configure(configureOptions);
        return services;
    }

    /// <summary>
    /// 添加常用的本地化资源文件夹
    /// </summary>
    /// <param name="resourceManager">本地化资源管理器</param>
    /// <param name="serviceProvider">服务提供者</param>
    /// <returns>本地化资源管理器</returns>
    public static ILocalizationResourceManager AddDefaultResources(
        this ILocalizationResourceManager resourceManager,
        IServiceProvider serviceProvider)
    {
        var options = serviceProvider.GetService<XiHanLocalizationOptions>();
        var defaultPath = options?.ResourcesPath ?? "Localization/Resources";

        _ = resourceManager.AddVirtualFileResource(defaultPath);
        return resourceManager;
    }

    /// <summary>
    /// 获取指定资源的所有可用文化
    /// </summary>
    /// <param name="resource">本地化资源</param>
    /// <returns>可用文化列表</returns>
    public static IReadOnlyList<CultureInfo> GetAvailableCultures(this ILocalizationResource resource)
    {
        var cultures = resource.GetSupportedCultures()
            .Select(c => new CultureInfo(c))
            .ToList();

        return cultures.AsReadOnly();
    }

    /// <summary>
    /// 检查资源是否支持指定的文化
    /// </summary>
    /// <param name="resource">本地化资源</param>
    /// <param name="cultureName">文化名称</param>
    /// <returns>是否支持</returns>
    public static bool SupportsCulture(this ILocalizationResource resource, string cultureName)
    {
        return resource.GetSupportedCultures().Contains(cultureName, StringComparer.OrdinalIgnoreCase);
    }
}
