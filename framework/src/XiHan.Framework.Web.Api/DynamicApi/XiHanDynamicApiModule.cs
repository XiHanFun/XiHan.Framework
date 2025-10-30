#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanDynamicApiModule
// Guid:q7r8s9t0-u1v2-4w3x-4y5z-6a7b8c9d0e1f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/24 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Web.Api.DynamicApi.Extensions;

namespace XiHan.Framework.Web.Api.DynamicApi;

/// <summary>
/// 曦寒框架动态 API 模块
/// </summary>
[DependsOn(typeof(XiHanWebApiModule))]
public class XiHanDynamicApiModule : XiHanModule
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
            options.EnableBatchOperations = true;
            options.MaxBatchSize = 100;
            options.RemoveServiceSuffix = true;

            // 约定配置
            options.Conventions.UseLowercaseRoutes = true;
            options.Conventions.UsePascalCaseRoutes = false;
            options.Conventions.RouteSeparator = "-";

            // 路由配置
            options.Routes.UseModuleNameAsRoute = true;
            options.Routes.UseNamespaceAsRoute = false;
        });
    }

    /// <summary>
    /// 应用初始化
    /// </summary>
    /// <param name="context"></param>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        // 动态 API 初始化逻辑
    }
}

