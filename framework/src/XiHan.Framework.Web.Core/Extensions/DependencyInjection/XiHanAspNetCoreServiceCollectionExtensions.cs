#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanAspNetCoreServiceCollectionExtensions
// Guid:b38bad27-b763-4184-b535-10917dfc8689
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/04/01 19:39:08
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Security.Claims;
using XiHan.Framework.Web.Core.Clients;
using XiHan.Framework.Web.Core.Options;
using XiHan.Framework.Web.Core.Security.Claims;

namespace XiHan.Framework.Web.Core.Extensions.DependencyInjection;

/// <summary>
/// 曦寒框架 AspNetCore 服务集合扩展
/// </summary>
public static class XiHanWebCoreServiceCollectionExtensions
{
    /// <summary>
    /// 获取主机环境
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IWebHostEnvironment GetHostingEnvironment(this IServiceCollection services)
    {
        var hostingEnvironment = services.GetSingletonInstanceOrNull<IWebHostEnvironment>();

        return hostingEnvironment ?? new EmptyHostingEnvironment
        {
            EnvironmentName = Environments.Development
        };
    }

    /// <summary>
    /// 添加曦寒 Web 核心服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">应用配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanWebCore(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddObjectAccessor<IApplicationBuilder>();
        services.AddHttpContextAccessor();
        services.Configure<XiHanClientInfoOptions>(configuration.GetSection(XiHanClientInfoOptions.SectionName));
        services.AddSingleton<IClientInfoProvider, HttpContextClientInfoProvider>();
        // 使用 HttpContext.User 作为当前主体，使 ICurrentUser 在 Web 请求中可用
        services.AddScoped<ICurrentPrincipalAccessor, HttpContextCurrentPrincipalAccessor>();

        return services;
    }

    /// <summary>
    /// 转换框架身份声明
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection TransformXiHanClaims(this IServiceCollection services)
    {
        return services.AddTransient<IClaimsTransformation, XiHanClaimsTransformation>();
    }
}
