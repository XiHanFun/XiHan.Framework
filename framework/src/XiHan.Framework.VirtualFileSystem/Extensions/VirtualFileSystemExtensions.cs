#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:VirtualFileSystemExtensions
// Guid:d4762acb-0b33-4edd-b46c-783b01056601
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/23 5:38:01
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.VirtualFileSystem.Options;
using XiHan.Framework.VirtualFileSystem.Services;

namespace XiHan.Framework.VirtualFileSystem.Extensions;

/// <summary>
/// 虚拟文件系统扩展方法
/// </summary>
public static class VirtualFileSystemExtensions
{
    /// <summary>
    /// 添加曦寒虚拟文件系统核心服务
    /// </summary>
    public static IServiceCollection AddXihanVirtualFileSystem(this IServiceCollection services, Action<VirtualFileSystemOptions> configure)
    {
        // 基础配置
        var options = new VirtualFileSystemOptions();
        configure?.Invoke(options);

        // 注册核心服务
        _ = services
            .AddSingleton<IVirtualFileSystem>(provider =>
            {
                var orderedProviders = options.Providers
                    .OrderByDescending(p => p.Priority)
                    .Select(p => p.Provider)
                    .ToList();

                return new VirtualFileSystem(orderedProviders);
            })
            // 注册附加服务
            .AddSingleton<FileCacheService>()
            .AddSingleton<FileVersioningService>()
            //.AddSingleton<FileOperationInterceptor>()
            ;

        return services;
    }
}
