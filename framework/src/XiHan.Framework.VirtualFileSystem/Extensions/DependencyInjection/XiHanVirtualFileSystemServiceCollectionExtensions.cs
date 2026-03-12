#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanVirtualFileSystemServiceCollectionExtensions
// Guid:f46852b8-fe45-4a82-b45f-e2f475747f64
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/14 11:10:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.VirtualFileSystem.Options;
using XiHan.Framework.VirtualFileSystem.Processing;
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

        // 注册媒体处理服务（默认实现会返回失败结果，不会中断主流程）
        services.TryAddTransient<IImageProcessingService, ImageSharpProcessingService>();
        services.TryAddTransient<IVideoProcessingService, FFmpegVideoProcessingService>();

        return services;
    }
}
