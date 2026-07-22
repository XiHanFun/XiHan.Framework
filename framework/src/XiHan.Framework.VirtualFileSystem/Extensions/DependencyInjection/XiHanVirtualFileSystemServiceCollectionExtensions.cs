// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.VirtualFileSystem.Options;
using XiHan.Framework.VirtualFileSystem.Services;

namespace XiHan.Framework.VirtualFileSystem.Extensions.DependencyInjection;

/// <summary>
/// 服务集合扩展方法
/// </summary>
public static class XiHanVirtualFileSystemServiceCollectionExtensions
{
    /// <summary>
    /// 添加 XiHan 虚拟文件服务
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configuration"></param>
    /// <returns></returns>
    public static IServiceCollection AddXiHanVirtualFileSystem(this IServiceCollection services, IConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<VirtualFileSystemOptions>();
        if (configuration != null)
        {
            services.Configure<VirtualFileSystemOptions>(configuration.GetSection(VirtualFileSystemOptions.SectionName));
        }

        // 注册核心服务
        services.TryAddSingleton<IVirtualFileSystem, VirtualFileSystem>();
        // 注册附加服务
        services.TryAddSingleton<IFileVersioningService, FileVersioningService>();

        return services;
    }
}
