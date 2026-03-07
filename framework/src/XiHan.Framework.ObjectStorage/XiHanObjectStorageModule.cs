#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanObjectStorageModule
// Guid:6d0ea7cb-9f7e-4cf6-a01b-6b6c3e11ea74
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/26 03:37:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.ObjectStorage.Constants;
using XiHan.Framework.ObjectStorage.Extensions.DependencyInjection;
using XiHan.Framework.ObjectStorage.Options;

namespace XiHan.Framework.ObjectStorage;

/// <summary>
/// 曦寒对象存储模块
/// </summary>
public class XiHanObjectStorageModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var config = services.GetConfiguration();
        services.AddXiHanObjectStorage();

        Configure<XiHanObjectStorageOptions>(config.GetSection(XiHanObjectStorageOptions.SectionName));
        Configure<LocalStorageOptions>(config.GetSection(LocalStorageOptions.SectionName));
        Configure<MinioStorageOptions>(config.GetSection(MinioStorageOptions.SectionName));
        Configure<AliyunOssStorageOptions>(config.GetSection(AliyunOssStorageOptions.SectionName));
        Configure<TencentCosStorageOptions>(config.GetSection(TencentCosStorageOptions.SectionName));

        RegisterConfiguredProviders(services, config);
    }

    /// <summary>
    /// 按配置注册提供程序
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="config">应用配置</param>
    private static void RegisterConfiguredProviders(IServiceCollection services, IConfiguration config)
    {
        var storageOptions = new XiHanObjectStorageOptions();
        config.GetSection(XiHanObjectStorageOptions.SectionName).Bind(storageOptions);

        var providerNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var providerName in storageOptions.EnabledProviders ?? [])
        {
            if (!string.IsNullOrWhiteSpace(providerName))
            {
                providerNames.Add(providerName.Trim());
            }
        }

        if (!string.IsNullOrWhiteSpace(storageOptions.DefaultProvider))
        {
            providerNames.Add(storageOptions.DefaultProvider.Trim());
        }

        if (providerNames.Count == 0)
        {
            providerNames.Add(ObjectStorageProviderNames.Local);
        }

        foreach (var providerName in providerNames)
        {
            RegisterProvider(services, providerName);
        }
    }

    /// <summary>
    /// 注册单个提供程序
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="providerName">提供程序名称</param>
    /// <exception cref="InvalidOperationException">未知提供程序名称</exception>
    private static void RegisterProvider(IServiceCollection services, string providerName)
    {
        switch (providerName.Trim().ToUpperInvariant())
        {
            case "LOCAL":
                services.AddLocalFileStorageProvider();
                break;

            case "MINIO":
                services.AddMinioFileStorageProvider();
                break;

            case "ALIYUNOSS":
                services.AddAliyunOssFileStorageProvider();
                break;

            case "TENCENTCOS":
                services.AddTencentCosFileStorageProvider();
                break;

            default:
                throw new InvalidOperationException($"不支持的对象存储提供程序：{providerName}");
        }
    }
}
