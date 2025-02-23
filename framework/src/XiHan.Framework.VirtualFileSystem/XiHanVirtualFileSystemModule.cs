#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanVirtualFileSystemModule
// Guid:8543c204-0497-4b7a-be6b-6d06c7c3053b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 3:42:33
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.VirtualFileSystem.Extensions;

namespace XiHan.Framework.VirtualFileSystem;

/// <summary>
/// 曦寒虚拟文件系统模块
/// </summary>
public class XiHanVirtualFileSystemModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        _ = context.Services.AddXihanVirtualFileSystem(config =>
        {
            // 默认配置
            _ = config
                .AddPhysical("wwwroot")      // 默认物理文件目录
                                             //.AddEmbedded<Program>()      // 主程序集嵌入资源
                .AddMemory()              // 启用文件缓存
                                          //.EnableOperationLogging() // 启用操作日志
                ;

            // 开发环境特殊配置
            //if (context.Services.IsDevelopment())
            //{
            //    _ = config
            //        .AddMemoryStorage(mem => // 内存文件系统
            //        {
            //            mem.Priority = 200;
            //        })
            //        .AddRemoteFileProvider("https://cdn.xihan.com"); // 开发环境CDN
        });
    }

    /// <summary>
    /// 应用初始化
    /// </summary>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        // 初始化文件系统监控等
        var fileSystem = context.ServiceProvider.GetRequiredService<IVirtualFileSystem>();
        _ = fileSystem.Watch("**/*").RegisterChangeCallback(_ =>
        {
            // 文件变化处理逻辑
        }, null);
    }
}
