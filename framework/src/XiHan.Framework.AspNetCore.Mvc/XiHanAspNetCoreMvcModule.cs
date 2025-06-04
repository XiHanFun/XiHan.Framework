#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanAspNetCoreMvcModule
// Guid:3373b5b4-d47d-4691-8604-08247351259f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 3:00:45
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.AspNetCore.Extensions;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.AspNetCore.Mvc;

/// <summary>
/// 曦寒框架 Web 核心 Mvc 模块
/// </summary>
[DependsOn(
    typeof(XiHanAspNetCoreModule)
)]
public class XiHanAspNetCoreMvcModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var aspNetCoreMvcOptions = new XiHanAspNetCoreMvcOptions();

        _ = services.AddControllers(options =>
        {
            options = aspNetCoreMvcOptions.MvcOptions;
        }).ConfigureApiBehaviorOptions(options =>
        {
            options = aspNetCoreMvcOptions.ApiBehaviorOptions;
        }).AddJsonOptions(options =>
        {
            options = aspNetCoreMvcOptions.JsonOptions;
        }).AddFormatterMappings(options =>
        {
            options = aspNetCoreMvcOptions.FormatterOptions;
        });

        _ = services.AddCors(options =>
        {
            options = aspNetCoreMvcOptions.CorsOptions;
        });

        _ = services.AddOpenApi();
    }

    /// <summary>
    /// 应用初始化
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        _ = context.ServiceProvider;
        _ = context.GetEnvironment();
        var app = context.GetApplicationBuilder();

        _ = app.UseRouting();
        _ = app.UseCors();
        _ = app.UseEndpoints(endpoints =>
        {
            // 不对约定路由做任何假设，也就是不使用约定路由，依赖用户的特性路由
            _ = endpoints.MapControllers();
            _ = endpoints.MapOpenApi();
        });
    }
}
