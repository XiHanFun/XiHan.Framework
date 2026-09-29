// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Traffic.Extensions.DependencyInjection;
using XiHan.Framework.Traffic.SqlSugar.Options;
using XiHan.Framework.Traffic.SqlSugar.Repositories;

namespace XiHan.Framework.Traffic.SqlSugar.Extensions.DependencyInjection;

/// <summary>
/// 流量治理 SqlSugar 服务集合扩展
/// </summary>
public static class XiHanTrafficSqlSugarServiceCollectionExtensions
{
    /// <summary>
    /// 以 SqlSugar 仓储替换灰度规则的内存实现
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanTrafficSqlSugar(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.ReplaceGrayRuleRepository<SqlSugarGrayRuleRepository>();

        return services;
    }

    /// <summary>
    /// 以 SqlSugar 仓储替换灰度规则的内存实现，并绑定配置节
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanTrafficSqlSugar(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<XiHanTrafficSqlSugarOptions>()
            .Bind(configuration.GetSection(XiHanTrafficSqlSugarOptions.SectionName))
            .Validate(
                options => options.RefreshInterval > TimeSpan.Zero,
                "流量治理 SqlSugar 配置无效：RefreshInterval 必须大于零。")
            .ValidateOnStart();

        return services.AddXiHanTrafficSqlSugar();
    }
}
