// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Authorization.Permissions;
using XiHan.Framework.Authorization.Policies;
using XiHan.Framework.Authorization.Roles;
using XiHan.Framework.Authorization.SqlSugar.Permissions;
using XiHan.Framework.Authorization.SqlSugar.Policies;
using XiHan.Framework.Authorization.SqlSugar.Roles;

namespace XiHan.Framework.Authorization.SqlSugar.Extensions.DependencyInjection;

/// <summary>
/// 授权 SqlSugar 存储服务集合扩展
/// </summary>
public static class XiHanAuthorizationSqlSugarServiceCollectionExtensions
{
    /// <summary>
    /// 以 SqlSugar 实现替换授权模块的内存存储
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanAuthorizationSqlSugar(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Replace(ServiceDescriptor.Scoped<IPermissionStore, SqlSugarPermissionStore>());
        services.TryAddScoped<SqlSugarPermissionStore>();
        services.Replace(ServiceDescriptor.Scoped<IRoleStore, SqlSugarRoleStore>());
        services.Replace(ServiceDescriptor.Scoped<IPermissionChecker, SqlSugarPermissionChecker>());
        services.Replace(ServiceDescriptor.Scoped<IPolicyStore, SqlSugarPolicyStore>());

        return services;
    }
}
