// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Authorization.Abac;
using XiHan.Framework.Authorization.AspNetCore;
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
        services.TryAddScoped<IRoleStore, DefaultRoleStore>();
        // 注册权限存储
        services.TryAddScoped<IPermissionStore, DefaultPermissionStore>();
        // 注册权限检查器
        services.TryAddScoped<IPermissionChecker, DefaultPermissionChecker>();
        // 注册策略存储
        services.TryAddScoped<IPolicyStore, DefaultPolicyStore>();
        // 注册策略评估器
        services.TryAddScoped<IPolicyEvaluator, DefaultPolicyEvaluator>();
        // 注册 ABAC 属性收集器
        services.TryAddScoped<IAbacAttributeCollector, DefaultAbacAttributeCollector>();
        // 注册 ABAC 评估器
        services.TryAddScoped<IAbacEvaluator, DefaultAbacEvaluator>();
        // 注册授权服务
        services.TryAddScoped<IAuthorizationService, DefaultAuthorizationService>();
        // 注册混合授权策略提供器
        services.TryAddSingleton<IAuthorizationPolicyProvider, HybridPermissionPolicyProvider>();
        // 注册混合授权处理器
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAuthorizationHandler, HybridPermissionAuthorizationHandler>());

        return services;
    }
}
