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
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Localization.Extensions.ApplicationBuilder;
using XiHan.Framework.MultiTenancy;
using XiHan.Framework.Serialization;
using XiHan.Framework.Web.Api.CircuitBreaking;
using XiHan.Framework.Web.Api.Extensions.DependencyInjection;
using XiHan.Framework.Web.Api.Middlewares;
using XiHan.Framework.Web.Api.RateLimiting;
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

        // 反向代理（nginx/网关）转发头还原：依据 X-Forwarded-Proto/Host/For 还原真实 scheme/host/client IP。
        // 否则后端只见反代到 Kestrel 的 http://127.0.0.1，会导致 OAuth 回调等据请求生成的绝对地址 scheme/host 错误。
        // 默认仅信任本机回环代理（同机 nginx → 127.0.0.1）；反代在其它主机时，应用侧可追加 KnownProxies/KnownNetworks。
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                | ForwardedHeaders.XForwardedProto
                | ForwardedHeaders.XForwardedHost;
        });

        services.AddXiHanWebApi(config);
        services.AddXiHanRateLimiting(config);
        services.AddXiHanCircuitBreaking(config);
    }

    /// <summary>
    /// 应用初始化
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();

        // 反向代理转发头还原：必须先于一切读取 scheme/host/client IP 的中间件（路由/鉴权/CORS/重定向生成等），故置于管线最前。
        app.UseForwardedHeaders();

        app.UseMiddleware<XiHanTraceIdMiddleware>();
        // 请求文化：紧跟 TraceId、先于路由/MVC，使后续管线（含请求上下文/控制器/响应过滤器）在请求文化下执行
        app.UseXiHanRequestCulture();
        app.UseMiddleware<XiHanRequestContextMiddleware>();
        app.UseMiddleware<XiHanExceptionLoggingMiddleware>();
        app.UseMiddleware<XiHanRequestLoggingMiddleware>();
        app.UseRouting();

        // 入站限流：仅 XiHan:Web:RateLimiting:IsEnabled=true 时启用；置于路由后、鉴权前，尽早拒绝超额请求
        if (context.ServiceProvider.GetRequiredService<IOptions<XiHanRateLimitingOptions>>().Value.IsEnabled)
        {
            app.UseRateLimiter();
        }

        // 入站熔断：仅 XiHan:Web:CircuitBreaking:IsEnabled=true 时启用；置于限流后、鉴权前，过载时快速失败保护实例
        if (context.ServiceProvider.GetRequiredService<IOptions<XiHanCircuitBreakingOptions>>().Value.IsEnabled)
        {
            app.UseMiddleware<XiHanCircuitBreakingMiddleware>();
        }

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
