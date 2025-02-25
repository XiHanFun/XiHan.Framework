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

using Microsoft.AspNetCore.Localization;
using System.Globalization;
using XiHan.Framework.AspNetCore.Authentication.JwtBearer;
using XiHan.Framework.AspNetCore.Authentication.OAuth;
using XiHan.Framework.AspNetCore.Extensions;
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
using XiHan.Framework.VirtualFileSystem;
using XiHan.Framework.VirtualFileSystem.Options;
using XiHan.Framework.Web.Test.Localization;

namespace XiHan.Framework.Web.Test;

/// <summary>
/// 曦寒测试应用 Web 主机
/// </summary>
[DependsOn(
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
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        // 确保Localization目录在物理位置存在并且被虚拟文件系统正确识别
        var localizationDir = Path.Combine(AppContext.BaseDirectory, "Localization");
        if (!Directory.Exists(localizationDir))
        {
            _ = Directory.CreateDirectory(localizationDir);
            System.Console.WriteLine($"创建Localization根目录: {localizationDir}");
        }
    }

    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // 首先，修改虚拟文件系统配置以确保能找到Localization目录
        Configure<VirtualFileSystemOptions>(config =>
        {
            // 先添加基础目录，确保根目录和Localization目录存在
            _ = config
                // 优先使用应用程序基础目录
                .AddPhysical(AppContext.BaseDirectory)
                // 然后添加Localization专用目录
                .AddPhysical(Path.Combine(AppContext.BaseDirectory, "Localization"))
                // 添加当前目录
                .AddPhysical(Environment.CurrentDirectory)
                // 最后添加其他目录
                .AddPhysical("wwwroot")
                .AddEmbedded<XiHanConsoleTestModule>();
        });

        _ = context.Services.AddSingleton<TestResource>();
    }

    /// <summary>
    /// 应用初始化
    /// </summary>
    /// <param name="context"></param>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        // 获取文件系统实例
        var fileSystem = context.ServiceProvider.GetRequiredService<IVirtualFileSystem>();
        // 订阅文件变化事件
        fileSystem.OnFileChanged += (sender, args) =>
        {
            // 处理文件变化逻辑
            System.Console.WriteLine($"文件发生变化: {args.FilePath} {args.ChangeType}");
        };
        _ = fileSystem.Watch("*.*");

        var app = context.GetApplicationBuilder();

        _ = app.UseRequestLocalization(options =>
        {
            var supportedCultures = new[]
            {
                new CultureInfo("en"),
                new CultureInfo("zh-CN")
            };

            options.DefaultRequestCulture = new RequestCulture("en");
            options.SupportedCultures = supportedCultures;
            options.SupportedUICultures = supportedCultures;
        });
    }
}
