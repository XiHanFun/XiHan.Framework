#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanLocalizationModule
// Guid:3e2d1f0a-9b8c-7d6e-5f4a-3e2d-1f0a-9b8c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/27 13:40:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Localization.Core;
using XiHan.Framework.Localization.Extensions;
using XiHan.Framework.Localization.JsonLocalization;
using XiHan.Framework.Localization.Resources;
using XiHan.Framework.Localization.VirtualFileSystem;
using XiHan.Framework.Settings;
using XiHan.Framework.Threading;
using XiHan.Framework.VirtualFileSystem;

namespace XiHan.Framework.Localization;

/// <summary>
/// 曦寒框架本地化模块
/// </summary>
[DependsOn(
    typeof(XiHanVirtualFileSystemModule),
    typeof(XiHanThreadingModule),
    typeof(XiHanSettingsModule)
)]
public class XiHanLocalizationModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context">服务配置上下文</param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        // 注册本地化服务
        _ = services.AddXiHanLocalization();

        // 注册接口实现
        _ = services.AddSingleton<ILocalizationResourceManager, LocalizationResourceManager>();
        _ = services.AddSingleton<IVirtualFileResourceFactory, VirtualFileResourceFactory>();
        _ = services.AddSingleton<IResourceStringProvider, JsonLocalizationResourceProvider>();
    }

    /// <summary>
    /// 应用程序初始化
    /// </summary>
    /// <param name="context">应用初始化上下文</param>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var serviceProvider = context.ServiceProvider;

        // 从虚拟文件系统注册本地化资源
        RegisterLocalizationResourcesFromVirtualFileSystem(serviceProvider);
    }

    /// <summary>
    /// 从虚拟文件系统注册本地化资源
    /// </summary>
    /// <param name="serviceProvider">服务提供者</param>
    private static void RegisterLocalizationResourcesFromVirtualFileSystem(IServiceProvider serviceProvider)
    {
        var resourceManager = serviceProvider.GetRequiredService<ILocalizationResourceManager>();
        var virtualFileSystem = serviceProvider.GetRequiredService<IVirtualFileSystem>();

        // 查找 Localization 目录下的所有JSON文件
        var directoryContents = virtualFileSystem.GetDirectoryContents("Localization");
        if (!directoryContents.Exists)
        {
            return;
        }

        foreach (var file in directoryContents)
        {
            if (file.IsDirectory)
            {
                var resourcePath = $"Localization/{file.Name}";
                _ = resourceManager.AddVirtualFileResource(resourcePath);
            }
        }
    }
}
