#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanWebApiModule
// Guid:7b69fc24-fbf3-4e1b-8175-eed3f45a7c76
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/12 0:38:39
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Serialization;
using XiHan.Framework.Web.Api.DynamicApi.Extensions;
using XiHan.Framework.Web.Core;
using XiHan.Framework.Web.Core.Extensions;

namespace XiHan.Framework.Web.Api;

/// <summary>
/// 曦寒框架 Web REST API 接口模块
/// </summary>
[DependsOn(
    typeof(XiHanWebCoreModule),
    typeof(XiHanSerializationModule)
)]
public class XiHanWebApiModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        // 添加动态 API
        services.AddDynamicApi(options =>
        {
            // 默认配置
            options.IsEnabled = true;
            options.DefaultRoutePrefix = "api";
            options.EnableApiVersioning = true;
            options.EnableBatchOperations = true;
            options.MaxBatchSize = 100;
            options.RemoveServiceSuffix = true;

            // 约定配置
            options.Conventions.UseLowercaseRoutes = true;
            options.Conventions.UsePascalCaseRoutes = true;
            options.Conventions.RouteSeparator = "";

            // 路由配置
            options.Routes.UseModuleNameAsRoute = false;
            options.Routes.UseNamespaceAsRoute = false;
        });

        var aspNetCoreMvcOptions = new XiHanWebCoreMvcOptions();

        services.AddControllers(options =>
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

        services.AddCors(options =>
        {
            options = aspNetCoreMvcOptions.CorsOptions;
        });

        services.AddOpenApi();
    }

    /// <summary>
    /// 应用初始化
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();

        app.UseRouting();
        app.UseCors();
        app.UseEndpoints(endpoints =>
        {
            // 不对约定路由做任何假设，也就是不使用约定路由，依赖用户的特性路由
            endpoints.MapControllers();
            endpoints.MapOpenApi();
        });
    }
}
