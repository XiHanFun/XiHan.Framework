#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanTrafficServiceCollectionExtensions
// Guid:7c8d9e0f-1a2b-3c4d-5e6f-7a8b9c0d1e2f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/22 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Traffic.GrayRouting.Abstractions;
using XiHan.Framework.Traffic.GrayRouting.Implementations;
using XiHan.Framework.Traffic.GrayRouting.Matchers;

namespace XiHan.Framework.Traffic.Extensions.DependencyInjection;

/// <summary>
/// 流量治理服务集合扩展
/// </summary>
public static class XiHanTrafficServiceCollectionExtensions
{
    /// <summary>
    /// 添加灰度路由服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddGrayRouting(this IServiceCollection services)
    {
        // 注册灰度规则引擎
        services.TryAddSingleton<IGrayRuleEngine, DefaultGrayRuleEngine>();

        // 注册默认的内存规则仓储（生产环境应替换为数据库实现）
        services.TryAddSingleton<IGrayRuleRepository, InMemoryGrayRuleRepository>();

        // 注册所有内置的灰度匹配器
        services.AddSingleton<IGrayMatcher, PercentageGrayMatcher>();
        services.AddSingleton<IGrayMatcher, UserIdGrayMatcher>();
        services.AddSingleton<IGrayMatcher, TenantIdGrayMatcher>();
        services.AddSingleton<IGrayMatcher, HeaderGrayMatcher>();

        return services;
    }

    /// <summary>
    /// 添加自定义灰度匹配器
    /// </summary>
    /// <typeparam name="TMatcher">匹配器类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddGrayMatcher<TMatcher>(this IServiceCollection services)
        where TMatcher : class, IGrayMatcher
    {
        services.AddSingleton<IGrayMatcher, TMatcher>();
        return services;
    }

    /// <summary>
    /// 替换灰度规则仓储实现
    /// </summary>
    /// <typeparam name="TRepository">仓储类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection ReplaceGrayRuleRepository<TRepository>(this IServiceCollection services)
        where TRepository : class, IGrayRuleRepository
    {
        services.Replace(ServiceDescriptor.Singleton<IGrayRuleRepository, TRepository>());
        return services;
    }
}
