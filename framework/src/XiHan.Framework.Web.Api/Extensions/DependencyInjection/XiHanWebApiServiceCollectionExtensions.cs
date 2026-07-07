#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanWebApiServiceCollectionExtensions
// Guid:c9d0e1f2-3456-7890-abcd-ef1234567890
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/06 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using XiHan.Framework.Application.Contracts.Dtos;
using XiHan.Framework.Authentication.Jwt;
using XiHan.Framework.Authentication.OAuth;
using XiHan.Framework.Web.Api.Auth;
using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Web.Api.Contexts;
using XiHan.Framework.Web.Api.Cors;
using XiHan.Framework.Web.Api.DynamicApi.Extensions;
using XiHan.Framework.Web.Api.Filters;
using XiHan.Framework.Auditing.Options;
using XiHan.Framework.Auditing.Pipelines;
using XiHan.Framework.Auditing.Queues;
using XiHan.Framework.Auditing.Workers;
using XiHan.Framework.Auditing.Writers;
using XiHan.Framework.MultiTenancy;
using XiHan.Framework.Web.Api.Security.OpenApi;
using XiHan.Framework.Web.Api.TenantResolvers;

namespace XiHan.Framework.Web.Api.Extensions.DependencyInjection;

/// <summary>
/// 曦寒框架 Web API 服务集合扩展
/// </summary>
public static class XiHanWebApiServiceCollectionExtensions
{
    /// <summary>
    /// 添加曦寒 Web API 全部服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">应用配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanWebApi(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddXiHanWebApiSecurity(configuration);
        services.AddXiHanWebApiCors(configuration);
        services.AddXiHanWebApiAuth(configuration);
        services.AddXiHanWebApiLogging(configuration);
        services.AddXiHanWebApiMvc();

        return services;
    }

    /// <summary>
    /// 添加 OpenApi 安全与请求上下文服务
    /// </summary>
    public static IServiceCollection AddXiHanWebApiSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IRequestContextAccessor, RequestContextAccessor>();
        services.TryAddScoped<ITraceIdProvider, HttpTraceIdProvider>();
        services.Configure<XiHanOpenApiSecurityOptions>(configuration.GetSection(XiHanOpenApiSecurityOptions.SectionName));
        services.TryAddScoped<IOpenApiSecurityClientStore, DefaultOpenApiSecurityClientStore>();

        return services;
    }

    /// <summary>
    /// 添加 CORS 跨域资源共享服务
    /// </summary>
    public static IServiceCollection AddXiHanWebApiCors(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<XiHanCorsOptions>(configuration.GetSection(XiHanCorsOptions.SectionName));
        var corsOptions = configuration.GetSection(XiHanCorsOptions.SectionName).Get<XiHanCorsOptions>() ?? new XiHanCorsOptions();

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

        return services;
    }

    /// <summary>
    /// 添加 JWT Bearer 认证与授权服务
    /// </summary>
    public static IServiceCollection AddXiHanWebApiAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<XiHanWebAuthOptions>(configuration.GetSection(XiHanWebAuthOptions.SectionName));
        var webAuthOptions = configuration.GetSection(XiHanWebAuthOptions.SectionName).Get<XiHanWebAuthOptions>() ?? new XiHanWebAuthOptions();
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>();
        var hasJwtBearerConfiguration = jwtOptions != null && !string.IsNullOrWhiteSpace(jwtOptions.SecretKey);

        var authBuilder = hasJwtBearerConfiguration
            ? services.AddAuthentication(authOptions =>
            {
                authOptions.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                authOptions.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            : services.AddAuthentication();

        if (hasJwtBearerConfiguration)
        {
            authBuilder.AddJwtBearer(bearerOptions =>
            {
                bearerOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = jwtOptions!.ValidateIssuer,
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
        }

        // OAuth 第三方登录所需的临时 Cookie scheme
        var oauthOptions = configuration.GetSection(OAuthOptions.SectionName).Get<OAuthOptions>();
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
            if (!hasJwtBearerConfiguration)
            {
                throw new InvalidOperationException(
                    "XiHan:Web:Api:Auth:RequireAuthenticatedUser=true 时，必须配置有效的 XiHan:Authentication:Jwt:SecretKey。");
            }

            services.AddAuthorization(authzOptions =>
            {
                authzOptions.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser()
                    .Build();
            });
        }
        else
        {
            services.AddAuthorization();
        }

        return services;
    }

    /// <summary>
    /// 添加 Web API 日志管道、队列和写入服务
    /// </summary>
    public static IServiceCollection AddXiHanWebApiLogging(this IServiceCollection services, IConfiguration configuration)
    {
        // 审计日志基础设施（Options / 队列 / 后台消费者 / 采集管道 / 空写入器）已下沉至
        // XiHanAuditingModule（XiHan.Framework.Auditing）；Web.Api 模块依赖它。
        // 此处仅注册 Web 特有的 MVC 过滤器。
        services.AddScoped<XiHanActionLoggingFilter>();
        services.AddScoped<XiHanApiResponseResultFilter>();

        return services;
    }

    /// <summary>
    /// 添加动态 API、MVC Controllers、JSON 序列化、租户解析和 OpenApi 服务
    /// </summary>
    public static IServiceCollection AddXiHanWebApiMvc(this IServiceCollection services)
    {
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
            // 全局约定为"剥离动词"路由（CreateXxxAsync → POST /Xxx）：全部前端按此对接。
            // 历史上此处曾设 true，但被特性 bool 默认值静默压掉从未生效；特性改为
            // 可空继承后，这里必须与真实生效行为保持一致，否则全部路由翻转。
            options.Conventions.PreserveRoutePredicate = false;
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

                    // 模型校验失败为 400：用工厂构造，业务码与状态一致；具体错误置于 Data（前端优先取 data 展示），
                    // 不再用 ApiResponse.Fail（会写死 500）
                    return new BadRequestObjectResult(ApiResponse.BadRequest(message, traceId));
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

        return services;
    }
}
