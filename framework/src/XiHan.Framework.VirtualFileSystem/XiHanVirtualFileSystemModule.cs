#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanVirtualFileSystemModule
// Guid:8543c204-0497-4b7a-be6b-6d06c7c3053b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 03:42:33
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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

        services.AddXiHanVirtualFileSystem();
    }
}
