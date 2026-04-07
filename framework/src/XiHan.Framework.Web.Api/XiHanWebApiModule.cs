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

using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using XiHan.Framework.Application.Contracts.Dtos;
using XiHan.Framework.Authentication.Jwt;
using XiHan.Framework.Authentication.OAuth;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.MultiTenancy;
using XiHan.Framework.Serialization;
using XiHan.Framework.Web.Api.Auth;
using XiHan.Framework.Web.Api.Contexts;
using XiHan.Framework.Web.Api.Cors;
using XiHan.Framework.Web.Api.DynamicApi.Extensions;
using XiHan.Framework.Web.Api.Filters;
using XiHan.Framework.Web.Api.Logging.Options;
using XiHan.Framework.Web.Api.Logging.Pipelines;
using XiHan.Framework.Web.Api.Logging.Queues;
using XiHan.Framework.Web.Api.Logging.Workers;
using XiHan.Framework.Web.Api.Logging.Writers;
using XiHan.Framework.Web.Api.Middlewares;
using XiHan.Framework.Web.Api.Security.OpenApi;
using XiHan.Framework.Web.Api.TenantResolvers;
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

        services.AddSingleton<IRequestContextAccessor, RequestContextAccessor>();
        services.Configure<XiHanOpenApiSecurityOptions>(config.GetSection(XiHanOpenApiSecurityOptions.SectionName));
        services.TryAddScoped<IOpenApiSecurityClientStore, DefaultOpenApiSecurityClientStore>();
        services.Configure<XiHanWebApiLogQueueOptions>(config.GetSection(XiHanWebApiLogQueueOptions.SectionName));

        // 配置 CORS 跨域资源共享
        services.Configure<XiHanCorsOptions>(config.GetSection(XiHanCorsOptions.SectionName));
        var corsOptions = config.GetSection(XiHanCorsOptions.SectionName).Get<XiHanCorsOptions>() ?? new XiHanCorsOptions();
        if (corsOptions.IsEnabled)
        {
            services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    // 配置来源
                    if (corsOptions.AllowAnyOrigin)
                    {
                        policy.AllowAnyOrigin();
                    }
                    else if (corsOptions.AllowedOrigins.Count > 0)
                    {
                        policy.WithOrigins([.. corsOptions.AllowedOrigins]);
                    }

                    // 配置方法
                    if (corsOptions.AllowAnyMethod)
                    {
                        policy.AllowAnyMethod();
                    }
                    else if (corsOptions.AllowedMethods.Count > 0)
                    {
                        policy.WithMethods([.. corsOptions.AllowedMethods]);
                    }

                    // 配置请求头
                    if (corsOptions.AllowAnyHeader)
                    {
                        policy.AllowAnyHeader();
                    }
                    else if (corsOptions.AllowedHeaders.Count > 0)
                    {
                        policy.WithHeaders([.. corsOptions.AllowedHeaders]);
                    }

                    // 配置凭据（AllowAnyOrigin 与 AllowCredentials 互斥）
                    if (corsOptions.AllowCredentials && !corsOptions.AllowAnyOrigin)
                    {
                        policy.AllowCredentials();
                    }

                    // 配置暴露的响应头
                    if (corsOptions.ExposedHeaders.Count > 0)
                    {
                        policy.WithExposedHeaders([.. corsOptions.ExposedHeaders]);
                    }

                    // 配置预检请求缓存时间
                    if (corsOptions.PreflightMaxAgeSeconds > 0)
                    {
                        policy.SetPreflightMaxAge(TimeSpan.FromSeconds(corsOptions.PreflightMaxAgeSeconds));
                    }
                });
            });
        }

        // 配置认证授权
        services.Configure<XiHanWebAuthOptions>(config.GetSection(XiHanWebAuthOptions.SectionName));
        var webAuthOptions = config.GetSection(XiHanWebAuthOptions.SectionName).Get<XiHanWebAuthOptions>() ?? new XiHanWebAuthOptions();
        var jwtOptions = config.GetSection(JwtOptions.SectionName).Get<JwtOptions>();

        if (webAuthOptions.EnableJwtBearer && jwtOptions != null && !string.IsNullOrEmpty(jwtOptions.SecretKey))
        {
            var authBuilder = services.AddAuthentication(authOptions =>
            {
                authOptions.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                authOptions.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(bearerOptions =>
            {
                bearerOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = jwtOptions.ValidateIssuer,
                    ValidateAudience = jwtOptions.ValidateAudience,
                    ValidateLifetime = jwtOptions.ValidateLifetime,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
                    ClockSkew = TimeSpan.FromMinutes(jwtOptions.ClockSkewMinutes)
                };

                bearerOptions.Events = new JwtBearerEvents
                {
                    // SignalR WebSocket/SSE 无法携带 Authorization Header，需从 query string 提取 token
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments(webAuthOptions.SignalRHubPathPrefix))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = context =>
                    {
                        if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
                        {
                            context.Response.Headers["Token-Expired"] = "true";
                        }
                        return Task.CompletedTask;
                    }
                };
            });

            // OAuth 第三方登录所需的临时 Cookie scheme
            var oauthOptions = config.GetSection(OAuthOptions.SectionName).Get<OAuthOptions>();
            if (oauthOptions is { Enabled: true })
            {
                authBuilder.AddCookie("ExternalCookie", cookieOptions =>
                {
                    cookieOptions.Cookie.Name = ".XiHan.External";
                    cookieOptions.Cookie.HttpOnly = true;
                    cookieOptions.Cookie.SameSite = SameSiteMode.Lax;
                    cookieOptions.ExpireTimeSpan = TimeSpan.FromMinutes(5);
                });
            }

            // 授权策略
            if (webAuthOptions.RequireAuthenticatedUser)
            {
                services.AddAuthorization(authzOptions =>
                {
                    authzOptions.FallbackPolicy = new AuthorizationPolicyBuilder()
                        .RequireAuthenticatedUser()
                        .Build();
                });
            }
            else
            {
                services.AddAuthorization();
            }
        }

        services.AddSingleton(typeof(ILogQueue<>), typeof(ChannelLogQueue<>));
        services.AddHostedService<AccessLogQueueWorker>();
        services.AddHostedService<OperationLogQueueWorker>();
        services.AddHostedService<ExceptionLogQueueWorker>();
        services.AddScoped<IAccessLogPipeline, AccessLogPipeline>();
        services.AddScoped<IOperationLogPipeline, OperationLogPipeline>();
        services.AddScoped<IExceptionLogPipeline, ExceptionLogPipeline>();
        services.AddScoped<XiHanActionLoggingFilter>();
        services.AddScoped<XiHanApiResponseResultFilter>();
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
            options.Conventions.PreserveRoutePredicate = true;
            options.Conventions.UsePascalCaseRoutes = true;
            options.Conventions.UseLowercaseRoutes = false;
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

                    return new BadRequestObjectResult(ApiResponse.Fail(message, traceId));
                };
            });

        services.Configure<XiHanTenantResolveOptions>(options =>
        {
            options.TenantResolvers.Add(new HeaderTenantResolveContributor());
            options.TenantResolvers.Add(new QueryStringTenantResolveContributor());
        });

        services.AddControllers(options =>
        {
            options.Filters.AddService<XiHanActionLoggingFilter>();
            options.Filters.AddService<XiHanApiResponseResultFilter>();
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
            options.JsonSerializerOptions.DictionaryKeyPolicy = aspNetCoreMvcOptions.JsonOptions.JsonSerializerOptions.DictionaryKeyPolicy;
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
        app.UseMiddleware<XiHanRequestContextMiddleware>();
        app.UseMiddleware<XiHanExceptionLoggingMiddleware>();
        app.UseMiddleware<XiHanRequestLoggingMiddleware>();
        app.UseRouting();
        app.UseCors();
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
}
