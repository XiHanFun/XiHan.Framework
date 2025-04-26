#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanAspNetCoreServiceCollectionExtensions
// Guid:b38bad27-b763-4184-b535-10917dfc8689
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/4/1 19:39:08
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.AspNetCore.Authentication;
using XiHan.Framework.AspNetCore.Security.Claims;
using XiHan.Framework.Core.Extensions.DependencyInjection;

namespace XiHan.Framework.AspNetCore.Extensions.DependencyInjection;

/// <summary>
/// 曦寒框架 AspNetCore 服务集合扩展
/// </summary>
public static class XiHanAspNetCoreServiceCollectionExtensions
{
    /// <summary>
    /// 获取主机环境
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IWebHostEnvironment GetHostingEnvironment(this IServiceCollection services)
    {
        var hostingEnvironment = services.GetSingletonInstanceOrNull<IWebHostEnvironment>();

        return hostingEnvironment == null
            ? new EmptyHostingEnvironment()
            {
                EnvironmentName = Environments.Development
            }
            : hostingEnvironment;
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
