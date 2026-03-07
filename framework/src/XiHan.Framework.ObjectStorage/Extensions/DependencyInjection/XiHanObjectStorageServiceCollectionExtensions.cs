#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanObjectStorageServiceCollectionExtensions
// Guid:8ec10f1b-5b4c-413e-b63f-c47f704e1728
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/08 01:56:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.ObjectStorage.Constants;
using XiHan.Framework.ObjectStorage.Options;
using XiHan.Framework.ObjectStorage.Providers;
using XiHan.Framework.ObjectStorage.Services;

namespace XiHan.Framework.ObjectStorage.Extensions.DependencyInjection;

/// <summary>
/// 对象存储服务注册扩展
/// </summary>
public static class XiHanObjectStorageServiceCollectionExtensions
{
    /// <summary>
    /// 添加对象存储核心服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">对象存储配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanObjectStorage(
        this IServiceCollection services,
        Action<XiHanObjectStorageOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.Configure<XiHanObjectStorageOptions>(_ => { });
        }

        services.Configure<XiHanObjectStorageProviderOptions>(_ => { });
        services.TryAddSingleton<IFileStorageProviderManager, DefaultFileStorageProviderManager>();
        services.TryAddSingleton<IFileStorageRouter, DefaultFileStorageRouter>();

        return services;
    }

    /// <summary>
    /// 注册自定义文件存储提供程序
    /// </summary>
    /// <typeparam name="TProvider">提供程序类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="providerName">提供程序名称</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddFileStorageProvider<TProvider>(
        this IServiceCollection services,
        string providerName)
        where TProvider : class, IFileStorageProvider
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

        var normalizedProviderName = providerName.Trim();

        services.TryAddSingleton<TProvider>();
        services.Configure<XiHanObjectStorageProviderOptions>(options =>
        {
            options.AddProvider(normalizedProviderName, typeof(TProvider));
        });

        return services;
    }

    /// <summary>
    /// 注册本地文件存储提供程序
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">本地存储配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddLocalFileStorageProvider(
        this IServiceCollection services,
        Action<LocalStorageOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.Configure<LocalStorageOptions>(_ => { });
        }

        return services.AddFileStorageProvider<LocalFileStorageProvider>(ObjectStorageProviderNames.Local);
    }

    /// <summary>
    /// 注册 MinIO 文件存储提供程序
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">MinIO 配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddMinioFileStorageProvider(
        this IServiceCollection services,
        Action<MinioStorageOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.Configure<MinioStorageOptions>(_ => { });
        }

        return services.AddFileStorageProvider<MinioFileStorageProvider>(ObjectStorageProviderNames.Minio);
    }

    /// <summary>
    /// 注册阿里云 OSS 文件存储提供程序
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">阿里云 OSS 配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddAliyunOssFileStorageProvider(
        this IServiceCollection services,
        Action<AliyunOssStorageOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.Configure<AliyunOssStorageOptions>(_ => { });
        }

        return services.AddFileStorageProvider<AliyunOssStorageProvider>(ObjectStorageProviderNames.AliyunOss);
    }

    /// <summary>
    /// 注册腾讯云 COS 文件存储提供程序
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">腾讯云 COS 配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddTencentCosFileStorageProvider(
        this IServiceCollection services,
        Action<TencentCosStorageOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.Configure<TencentCosStorageOptions>(_ => { });
        }

        return services.AddFileStorageProvider<TencentCosStorageProvider>(ObjectStorageProviderNames.TencentCos);
    }
}
