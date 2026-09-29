// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Security.Services;
using XiHan.Framework.Security.SqlSugar.Services;

namespace XiHan.Framework.Security.SqlSugar.Extensions.DependencyInjection;

/// <summary>
/// 安全模块 SqlSugar 服务集合扩展
/// </summary>
public static class XiHanSecuritySqlSugarServiceCollectionExtensions
{
    /// <summary>
    /// 以 SqlSugar 存储替换密码历史记录的内存实现
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanSecuritySqlSugar(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<SqlSugarPasswordHistoryStore>();
        services.Replace(ServiceDescriptor.Scoped<IPasswordHistoryStore>(
            sp => sp.GetRequiredService<SqlSugarPasswordHistoryStore>()));

        return services;
    }
}
