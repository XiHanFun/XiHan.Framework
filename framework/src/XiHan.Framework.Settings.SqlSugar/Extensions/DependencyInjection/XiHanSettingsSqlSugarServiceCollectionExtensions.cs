// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Settings.SqlSugar.Stores;
using XiHan.Framework.Settings.Stores;

namespace XiHan.Framework.Settings.SqlSugar.Extensions.DependencyInjection;

/// <summary>
/// 设置管理 SqlSugar 服务集合扩展
/// </summary>
public static class XiHanSettingsSqlSugarServiceCollectionExtensions
{
    /// <summary>
    /// 以 SqlSugar 存储替换设置管理的空存储
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanSettingsSqlSugar(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Replace(ServiceDescriptor.Scoped<ISettingStore, SqlSugarSettingStore>());

        return services;
    }
}
