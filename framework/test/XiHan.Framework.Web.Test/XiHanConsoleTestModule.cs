#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanConsoleTestModule
// Guid:c9bf348b-8c2f-4e2a-9f36-cc2edafe551e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/10 5:34:12
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.AI;
using XiHan.Framework.AspNetCore.Authentication.JwtBearer;
using XiHan.Framework.AspNetCore.Authentication.OAuth;
using XiHan.Framework.AspNetCore.Scalar;
using XiHan.Framework.AspNetCore.Serilog;
using XiHan.Framework.AspNetCore.SignalR;
using XiHan.Framework.AspNetCore.Swagger;
using XiHan.Framework.BlobStoring;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Ddd.Application;
using XiHan.Framework.Localization;
using XiHan.Framework.Settings;
using XiHan.Framework.Utils.IO;
using XiHan.Framework.VirtualFileSystem;
using XiHan.Framework.VirtualFileSystem.Options;

namespace XiHan.Framework.Web.Test;

/// <summary>
/// 曦寒测试应用 Web 主机
/// </summary>
[DependsOn(
    typeof(XiHanAIModule),
    typeof(XiHanBlobStoringModule),
    typeof(XiHanSettingsModule),
    typeof(XiHanLocalizationModule),
    typeof(XiHanDddApplicationModule),
    typeof(XiHanAspNetCoreSerilogModule),
    typeof(XiHanAspNetCoreSignalRModule),
    typeof(XiHanAspNetCoreAuthenticationJwtBearerModule),
    typeof(XiHanAspNetCoreAuthenticationOAuthModule),
    typeof(XiHanAspNetCoreScalarModule),
    typeof(XiHanAspNetCoreSwaggerModule)
)]
public class XiHanConsoleTestModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        // 配置虚拟文件系统的本地化资源目录
        Configure<VirtualFileSystemOptions>(config =>
        {
            _ = config
                .AddPhysical(DirectoryHelper.GetBaseDirectory())
                .AddPhysical("Localization/Resources");
        });
    }

    /// <summary>
    /// 应用初始化
    /// </summary>
    /// <param name="context"></param>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var serviceProvider = context.ServiceProvider;
        var virtualFileSystem = serviceProvider.GetRequiredService<IVirtualFileSystem>();

        // 订阅文件变化事件
        virtualFileSystem.OnFileChanged += (sender, args) =>
        {
            // 处理文件变化逻辑
            Console.WriteLine($"文件发生变化: {args.FilePath} {args.ChangeType}");
        };
        _ = virtualFileSystem.Watch("*.*");
    }
}
