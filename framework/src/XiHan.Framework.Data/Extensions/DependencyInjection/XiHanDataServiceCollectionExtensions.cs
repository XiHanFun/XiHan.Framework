#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanDataServiceCollectionExtensions
// Guid:a7b8c9d0-e1f2-7890-1234-567890123456
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/12 23:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Data.SqlSugar;
using XiHan.Framework.Data.SqlSugar.Initializers;
using XiHan.Framework.Data.SqlSugar.Options;
using XiHan.Framework.Data.SqlSugar.Repository;
using XiHan.Framework.Data.SqlSugar.Seeders;
using XiHan.Framework.Domain.Repositories;

namespace XiHan.Framework.Data.Extensions.DependencyInjection;

/// <summary>
/// SqlSugar 服务集合扩展
/// </summary>
public static class XiHanDataServiceCollectionExtensions
{
    /// <summary>
    /// 添加SqlSugar数据访问服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置选项</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanDataSqlSugar(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<XiHanSqlSugarCoreOptions>(configuration.GetSection(XiHanSqlSugarCoreOptions.SectionName));

        // 注册核心服务
        services.TryAddScoped<ISqlSugarDbContext, SqlSugarDbContext>();

        // 注册数据库初始化器
        services.TryAddScoped<IDbInitializer, DbInitializer>();

        // 注册仓储服务
        services.TryAddScoped(typeof(IReadOnlyRepositoryBase<,>), typeof(SqlSugarReadOnlyRepository<,>));
        services.TryAddScoped(typeof(IRepositoryBase<,>), typeof(SqlSugarRepositoryBase<,>));
        services.TryAddScoped(typeof(ISoftDeleteRepositoryBase<,>), typeof(SqlSugarSoftDeleteRepository<,>));
        services.TryAddScoped(typeof(IAuditedRepository<,>), typeof(SqlSugarAuditedRepository<,>));
        services.TryAddScoped(typeof(IAggregateRootRepository<,>), typeof(SqlSugarAggregateRepository<,>));

        return services;
    }

    /// <summary>
    /// 添加种子数据提供者
    /// </summary>
    /// <typeparam name="TSeeder">种子数据类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddDataSeeder<TSeeder>(this IServiceCollection services)
        where TSeeder : class, IDataSeeder
    {
        services.AddScoped<IDataSeeder, TSeeder>();
        return services;
    }

    /// <summary>
    /// 批量添加种子数据提供者
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="seederTypes">种子数据类型集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddDataSeeders(this IServiceCollection services, params Type[] seederTypes)
    {
        foreach (var seederType in seederTypes)
        {
            if (!typeof(IDataSeeder).IsAssignableFrom(seederType))
            {
                throw new ArgumentException($"类型 {seederType.Name} 必须实现 IDataSeeder 接口");
            }

            services.AddScoped(typeof(IDataSeeder), seederType);
        }

        return services;
    }
}
