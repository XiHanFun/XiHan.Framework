// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Upgrade.Abstractions;
using XiHan.Framework.Upgrade.SqlSugar.Services;

namespace XiHan.Framework.Upgrade.SqlSugar.Extensions.DependencyInjection;

/// <summary>
/// 升级模块 SqlSugar 服务集合扩展
/// </summary>
public static class XiHanUpgradeSqlSugarServiceCollectionExtensions
{
    /// <summary>
    /// 以 SqlSugar 存储替换升级版本记录的内存实现
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanUpgradeSqlSugar(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Replace(ServiceDescriptor.Scoped<IUpgradeVersionStore, SqlSugarUpgradeVersionStore>());

        return services;
    }
}
