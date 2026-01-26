#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanAuthorizationServiceCollectionExtensions
// Guid:f08d98d7-2ff2-484b-aa45-acf3d88c0c09
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/26
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Authorization.Permissions;
using XiHan.Framework.Authorization.Policies;
using XiHan.Framework.Authorization.Roles;

namespace XiHan.Framework.Authorization.Extensions.DependencyInjection;

/// <summary>
/// 曦寒授权服务集合扩展
/// </summary>
public static class XiHanAuthorizationServiceCollectionExtensions
{
    /// <summary>
    /// 添加曦寒授权服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanAuthorization(this IServiceCollection services, IConfiguration configuration)
    {
        // 注册角色存储
        services.AddScoped<IRoleStore, DefaultRoleStore>();
        // 注册角色管理器
        services.AddScoped<IRoleManager, DefaultRoleManager>();
        // 注册权限存储
        services.AddScoped<IPermissionStore, DefaultPermissionStore>();
        // 注册权限检查器
        services.AddScoped<IPermissionChecker, DefaultPermissionChecker>();
        // 注册策略存储
        services.AddScoped<IPolicyStore, DefaultPolicyStore>();
        // 注册策略评估器
        services.AddScoped<IPolicyEvaluator, DefaultPolicyEvaluator>();
        // 注册授权服务
        services.AddScoped<IAuthorizationService, DefaultAuthorizationService>();

        return services;
    }
}
