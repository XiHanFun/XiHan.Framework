#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanAspNetCoreModule
// Guid:e188b74b-8725-46e3-b520-c6757cfe0f6f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 2:41:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.AspNetCore;

/// <summary>
/// 曦寒框架 Web 核心模块
/// </summary>
public class XiHanAspNetCoreModule : XiHanModule
{
    /// <summary>
    /// 服务配置前
    /// </summary>
    /// <param name="context"></param>
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        _ = services.GetSingletonInstance<IXiHanHostEnvironment>();
        //if (xihanHostEnvironment.EnvironmentName.IsNullOrWhiteSpace())
        //{
        //    xihanHostEnvironment.EnvironmentName = services.GetHostingEnvironment().EnvironmentName;
        //}
    }

    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        //Configure<XiHanAuditingOptions>(options =>
        //{
        //    options.Contributors.Add(new AspNetCoreAuditLogContributor());
        //});

        //Configure<StaticFileOptions>(options =>
        //{
        //    options.ContentTypeProvider = context.Services.GetRequiredService<XiHanFileExtensionContentTypeProvider>();
        //});

        //AddAspNetServices(context.Services);
        _ = services.AddObjectAccessor<IApplicationBuilder>();
        //context.Services.AddXiHanDynamicOptions<RequestLocalizationOptions, XiHanRequestLocalizationOptionsManager>();
    }
}
