#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SqlSugarServiceCollectionExtensions
// Guid:a7b8c9d0-e1f2-7890-1234-567890123456
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/12 23:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Data.Core;
using XiHan.Framework.Data.SqlSugar.Options;
using XiHan.Framework.Domain.Aggregates.Abstracts;
using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Repositories;

namespace XiHan.Framework.Data.SqlSugar.Extensions;

/// <summary>
/// SqlSugar 服务集合扩展
/// </summary>
public static class SqlSugarServiceCollectionExtensions
{
    /// <summary>
    /// 添加SqlSugar数据访问服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configureOptions">配置选项</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddSqlSugarData(this IServiceCollection services, Action<XiHanSqlSugarCoreOptions>? configureOptions = null)
    {
        // 配置选项
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }

        // 注册核心服务
        services.TryAddScoped<ISqlSugarDbContext, SqlSugarDbContext>();

        // 注册仓储服务
        services.TryAddScoped(typeof(IDataRepository<,>), typeof(SqlSugarDataRepository<,>));
        services.TryAddScoped(typeof(IAggregateRootRepository<,>), typeof(SqlSugarAggregateRepository<,>));

        return services;
    }

    /// <summary>
    /// 添加实体仓储
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TKey">主键类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddRepository<TEntity, TKey>(this IServiceCollection services)
        where TEntity : class, IEntityBase<TKey>, new()
        where TKey : IEquatable<TKey>
    {
        services.TryAddScoped<IDataRepository<TEntity, TKey>, SqlSugarDataRepository<TEntity, TKey>>();
        return services;
    }

    /// <summary>
    /// 添加聚合根仓储
    /// </summary>
    /// <typeparam name="TAggregateRoot">聚合根类型</typeparam>
    /// <typeparam name="TKey">主键类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddAggregateRepository<TAggregateRoot, TKey>(this IServiceCollection services)
        where TAggregateRoot : class, IAggregateRoot<TKey>, new()
        where TKey : IEquatable<TKey>
    {
        services.TryAddScoped<IAggregateRootRepository<TAggregateRoot, TKey>, SqlSugarAggregateRepository<TAggregateRoot, TKey>>();
        return services;
    }

    /// <summary>
    /// 添加自定义仓储实现
    /// </summary>
    /// <typeparam name="TInterface">仓储接口</typeparam>
    /// <typeparam name="TImplementation">仓储实现</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="lifetime">服务生命周期</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddCustomRepository<TInterface, TImplementation>(this IServiceCollection services, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TInterface : class
        where TImplementation : class, TInterface
    {
        services.Add(new ServiceDescriptor(typeof(TInterface), typeof(TImplementation), lifetime));
        return services;
    }
}
