#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanWebApiModule
// Guid:7b69fc24-fbf3-4e1b-8175-eed3f45a7c76
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/12 00:38:39
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.MultiTenancy;
using XiHan.Framework.Serialization;
using XiHan.Framework.Application.Contracts.Dtos;
using XiHan.Framework.Application.Contracts.Enums;
using XiHan.Framework.Web.Api.DynamicApi.Extensions;
using XiHan.Framework.Web.Api.Filters;
using XiHan.Framework.Web.Api.Middlewares;
using XiHan.Framework.Web.Api.Logging;
using XiHan.Framework.Web.Api.TenantResolvers;
using XiHan.Framework.Web.Core;
using XiHan.Framework.Web.Core.Extensions;
using XiHan.Framework.Core.Extensions.DependencyInjection;

namespace XiHan.Framework.Web.Api;

/// <summary>
/// 曦寒框架 Web REST API 接口模块
/// </summary>
[DependsOn(
    typeof(XiHanWebCoreModule),
    typeof(XiHanMultiTenancyModule),
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
        var config = services.GetConfiguration();

        services.AddScoped<XiHanGlobalExceptionFilter>();
        services.AddScoped<XiHanActionLoggingFilter>();
        services.TryAddScoped<IAccessLogWriter, NullAccessLogWriter>();
        services.TryAddScoped<IOperationLogWriter, NullOperationLogWriter>();
        services.TryAddScoped<IExceptionLogWriter, NullExceptionLogWriter>();
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

        var aspNetCoreMvcOptions = new XiHanWebCoreMvcOptions()
            .ConfigureJsonOptionsDefault()
            .ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = actionContext =>
                {
                    var traceId = actionContext.HttpContext.TraceIdentifier;
                    var errors = actionContext.ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .Where(e => !string.IsNullOrWhiteSpace(e))
                        .Distinct()
                        .ToArray();

                    var message = errors.Length > 0
                        ? string.Join("; ", errors)
                        : "请求参数校验失败";

                    return new BadRequestObjectResult(ApiResponse.Fail(
                        message,
                        ApiResponseCodes.BadRequest,
                        traceId));
                };
            });

        services.Configure<XiHanTenantResolveOptions>(options =>
        {
            options.TenantResolvers.Add(new HeaderTenantResolveContributor());
            options.TenantResolvers.Add(new QueryStringTenantResolveContributor());
        });

        services.AddControllers(options =>
        {
            options.Filters.AddService<XiHanGlobalExceptionFilter>();
            options.Filters.AddService<XiHanActionLoggingFilter>();
        })
        .ConfigureApiBehaviorOptions(options =>
        {
            options.InvalidModelStateResponseFactory = aspNetCoreMvcOptions.ApiBehaviorOptions.InvalidModelStateResponseFactory;
        })
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.WriteIndented = aspNetCoreMvcOptions.JsonOptions.JsonSerializerOptions.WriteIndented;
            options.JsonSerializerOptions.ReferenceHandler = aspNetCoreMvcOptions.JsonOptions.JsonSerializerOptions.ReferenceHandler;
            options.JsonSerializerOptions.NumberHandling = aspNetCoreMvcOptions.JsonOptions.JsonSerializerOptions.NumberHandling;
            options.JsonSerializerOptions.AllowTrailingCommas = aspNetCoreMvcOptions.JsonOptions.JsonSerializerOptions.AllowTrailingCommas;
            options.JsonSerializerOptions.ReadCommentHandling = aspNetCoreMvcOptions.JsonOptions.JsonSerializerOptions.ReadCommentHandling;
            options.JsonSerializerOptions.PropertyNameCaseInsensitive = aspNetCoreMvcOptions.JsonOptions.JsonSerializerOptions.PropertyNameCaseInsensitive;
            options.JsonSerializerOptions.PropertyNamingPolicy = aspNetCoreMvcOptions.JsonOptions.JsonSerializerOptions.PropertyNamingPolicy;
            options.JsonSerializerOptions.Encoder = aspNetCoreMvcOptions.JsonOptions.JsonSerializerOptions.Encoder;
            options.JsonSerializerOptions.AllowOutOfOrderMetadataProperties = aspNetCoreMvcOptions.JsonOptions.JsonSerializerOptions.AllowOutOfOrderMetadataProperties;
            options.JsonSerializerOptions.IgnoreReadOnlyFields = aspNetCoreMvcOptions.JsonOptions.JsonSerializerOptions.IgnoreReadOnlyFields;
            options.JsonSerializerOptions.DefaultIgnoreCondition = aspNetCoreMvcOptions.JsonOptions.JsonSerializerOptions.DefaultIgnoreCondition;
            foreach (var converter in aspNetCoreMvcOptions.JsonOptions.JsonSerializerOptions.Converters)
            {
                options.JsonSerializerOptions.Converters.Add(converter);
            }
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

        app.UseMiddleware<XiHanTraceIdMiddleware>();
        app.UseMiddleware<XiHanRequestLoggingMiddleware>();
        app.UseRouting();
        app.UseCors();
        app.UseAuthentication();
        app.UseMiddleware<XiHanTenantResolveMiddleware>();
        app.UseAuthorization();
        app.UseEndpoints(endpoints =>
        {
            // 不对约定路由做任何假设，也就是不使用约定路由，依赖用户的特性路由
            endpoints.MapControllers();
            endpoints.MapOpenApi();
        });
    }
}
