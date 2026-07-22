// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.VirtualFileSystem.Extensions.DependencyInjection;

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
        var services = context.Services;
        var config = services.GetConfiguration();

        services.AddXiHanVirtualFileSystem(config);
    }
}
