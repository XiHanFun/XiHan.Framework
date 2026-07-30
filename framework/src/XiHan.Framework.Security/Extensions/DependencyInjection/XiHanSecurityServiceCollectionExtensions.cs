// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Security.Password;
using XiHan.Framework.Security.Services;

namespace XiHan.Framework.Security.Extensions.DependencyInjection;

/// <summary>
/// 曦寒安全服务集合扩展
/// </summary>
public static class XiHanSecurityServiceCollectionExtensions
{
    /// <summary>
    /// 添加曦寒安全服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanSecurityServices(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);

        // 密码相关选项与服务的类型均归属本模块（Security.Password），故在此统一绑定/注册以保证自洽。
        // 上层 Authentication 不再重复登记（Authentication [DependsOn Security]，本模块先于其加载）。
        services.Configure<PasswordHasherOptions>(configuration.GetSection(PasswordHasherOptions.SectionName));
        services.Configure<PasswordPolicyOptions>(configuration.GetSection(PasswordPolicyOptions.SectionName));

        // 注册密码哈希器
        services.TryAddSingleton<IPasswordHasher, PasswordHasher>();

        // 注册密码策略服务（Scoped，因消费方可能需要 Scoped 仓储）
        services.TryAddScoped<IPasswordPolicyService, PasswordPolicyService>();

        // 注册密码历史记录存储（Scoped，默认内存实现，可被应用层替换为数据库实现）
        services.TryAddScoped<IPasswordHistoryStore, DefaultPasswordHistoryStore>();

        return services;
    }
}
