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

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.MultiTenancy;
using XiHan.Framework.Serialization;
using XiHan.Framework.Web.Api.Extensions.DependencyInjection;
using XiHan.Framework.Web.Api.Middlewares;
using XiHan.Framework.Web.Api.Security.OpenApi;
using XiHan.Framework.Web.Core;
using XiHan.Framework.Web.Core.Extensions;

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

        services.AddXiHanWebApi(config);
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
        app.UseMiddleware<XiHanRequestContextMiddleware>();
        app.UseMiddleware<XiHanExceptionLoggingMiddleware>();
        app.UseMiddleware<XiHanRequestLoggingMiddleware>();
        app.UseRouting();
        app.UseCors();
        // 本地对象存储静态文件服务：在鉴权前注册，使本地存储的公开资源（头像等）可经静态 URL 匿名访问。
        // 目录/路径与 LocalStorageOptions（XiHan:ObjectStorage:Local）约定一致，无配置时回退默认值。
        UseLocalObjectStorageStaticFiles(context);
        app.UseMiddleware<XiHanApiLoggingMiddleware>();
        app.UseMiddleware<XiHanOpenApiSecurityMiddleware>();
        app.UseAuthentication();
        app.UseMiddleware<XiHanTenantResolveMiddleware>();
        app.UseAuthorization();
        app.UseEndpoints(endpoints =>
        {
            // 不对约定路由做任何假设，也就是不使用约定路由，依赖用户的特性路由
            endpoints.MapControllers();
            endpoints.MapOpenApi().AllowAnonymous();
        });
    }

    /// <summary>
    /// 启用本地对象存储静态文件服务。
    /// 与 LocalStorageOptions（配置节 XiHan:ObjectStorage:Local）的 RootPath/UrlPrefix 约定一致，
    /// 经 IConfiguration 读取以避免对 ObjectStorage 项目产生编译期依赖；无配置时回退默认值（wwwroot/uploads、/uploads）。
    /// 注册在鉴权中间件之前，使本地存储返回的静态 URL（头像、公开文件等）可匿名直链访问。
    /// </summary>
    private static void UseLocalObjectStorageStaticFiles(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        var configuration = context.GetConfiguration();
        var environment = context.GetEnvironment();

        var rootPath = configuration["XiHan:ObjectStorage:Local:RootPath"];
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            rootPath = "wwwroot/uploads";
        }

        var urlPrefix = configuration["XiHan:ObjectStorage:Local:UrlPrefix"];
        if (string.IsNullOrWhiteSpace(urlPrefix))
        {
            urlPrefix = "/uploads";
        }

        var requestPath = "/" + urlPrefix.Trim().Trim('/');
        if (requestPath == "/")
        {
            return;
        }

        var physicalRoot = Path.IsPathRooted(rootPath)
            ? rootPath
            : Path.Combine(environment.ContentRootPath, rootPath);

        if (!Directory.Exists(physicalRoot))
        {
            Directory.CreateDirectory(physicalRoot);
        }

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(Path.GetFullPath(physicalRoot)),
            RequestPath = requestPath,
            ServeUnknownFileTypes = false
        });
    }
}
