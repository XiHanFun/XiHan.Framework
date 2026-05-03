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
using Microsoft.Extensions.Options;
using SqlSugar;
using System.Linq.Expressions;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Data.Auditing;
using XiHan.Framework.Data.SqlSugar.Auditing;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.Data.SqlSugar.Extensions;
using XiHan.Framework.Data.SqlSugar.Initializers;
using XiHan.Framework.Data.SqlSugar.Metadata;
using XiHan.Framework.Data.SqlSugar.Options;
using XiHan.Framework.Data.SqlSugar.Repository;
using XiHan.Framework.Data.SqlSugar.Seeders;
using XiHan.Framework.Data.SqlSugar.Tenanting;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Repositories;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Utils.Logging;

namespace XiHan.Framework.Data.Extensions.DependencyInjection;

/// <summary>
/// 曦寒数据访问服务集合扩展
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
        services.TryAddSingleton(CreateScope);
        services.TryAddScoped<ISqlSugarTenantConnectionResolver, SqlSugarTenantConnectionResolver>();
        // 客户端解析器（按当前租户解析连接，并在事务型 UoW 中自动接入事务）
        services.TryAddScoped<ISqlSugarClientResolver, SqlSugarClientResolver>();
        // 当前租户对应的 ISqlSugarClient 直接注入
        services.TryAddScoped(sp => sp.GetRequiredService<ISqlSugarClientResolver>().GetCurrentClient());
        // SqlSugar DataExecuting AOP：主键、租户和审计字段注入
        services.TryAddSingleton<SqlSugarDataExecutingHandler>();

        // 注册仓储服务
        services.TryAddScoped(typeof(IReadOnlyRepositoryBase<,>), typeof(SqlSugarReadOnlyRepository<,>));
        services.TryAddScoped(typeof(IRepositoryBase<,>), typeof(SqlSugarRepositoryBase<,>));
        services.TryAddScoped(typeof(ISoftDeleteRepositoryBase<,>), typeof(SqlSugarSoftDeleteRepository<,>));
        services.TryAddScoped(typeof(IAuditedRepository<,>), typeof(SqlSugarAuditedRepository<,>));
        services.TryAddScoped(typeof(IAggregateRootRepository<,>), typeof(SqlSugarAggregateRepository<,>));

        services.TryAddScoped<IDatabaseMetadataProvider, SqlSugarDatabaseMetadataProvider>();
        // 默认 Null 实现：未启用审计或业务层未实现时零开销
        services.TryAddScoped<IEntityAuditContextProvider, NullEntityAuditContextProvider>();
        services.TryAddScoped<IEntityDiffLogWriter, NullEntityDiffLogWriter>();

        // 注册数据库初始化器
        services.TryAddScoped<IDbInitializer, DbInitializer>();

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

    /// <summary>
    /// 添加 SqlSugar
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns></returns>
    private static SqlSugarScope CreateScope(IServiceProvider services)
    {
        var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();
        var options = services.GetRequiredService<IOptions<XiHanSqlSugarCoreOptions>>().Value;
        var idGenerator = services.GetRequiredService<IDistributedIdGenerator<long>>();
        var currentTenantAccessor = services.GetRequiredService<ICurrentTenantAccessor>();
        var dataExecutingHandler = services.GetRequiredService<SqlSugarDataExecutingHandler>();

        var connectionConfigs = options.ConnectionConfigs
            .Select(connConfig => new ConnectionConfig
            {
                ConfigId = connConfig.ConfigId,
                ConnectionString = connConfig.ConnectionString,
                DbType = connConfig.DbType,
                IsAutoCloseConnection = connConfig.IsAutoCloseConnection,
                InitKeyType = connConfig.InitKeyType,
                MoreSettings = BuildMoreSettings(connConfig.MoreSettings, options),
                SlaveConnectionConfigs = connConfig.SlaveConnectionConfigs
            })
            .ToList();

        // 设置自定义全局雪花ID生成器
        StaticConfig.CustomSnowFlakeFunc = idGenerator.NextId;

        return new SqlSugarScope(connectionConfigs, client =>
        {
            foreach (var config in connectionConfigs)
            {
                var dbProvider = client.GetConnectionScope(config.ConfigId);
                ApplySugarGlobalFilters(dbProvider, options, currentTenantAccessor);
                SetSugarAop(scopeFactory, dbProvider, options, dataExecutingHandler);
            }
        });
    }

    private static ConnMoreSettings BuildMoreSettings(ConnMoreSettings? rawSettings, XiHanSqlSugarCoreOptions options)
    {
        rawSettings ??= new ConnMoreSettings();
        rawSettings.IsAutoUpdateQueryFilter = options.EnableAutoUpdateQueryFilter;
        rawSettings.IsAutoDeleteQueryFilter = options.EnableAutoDeleteQueryFilter;
        return rawSettings;
    }

    /// <summary>
    /// 应用全局过滤器
    /// </summary>
    /// <param name="provider"></param>
    /// <param name="options"></param>
    /// <param name="currentTenantAccessor"></param>
    private static void ApplySugarGlobalFilters(
        SqlSugarScopeProvider provider,
        XiHanSqlSugarCoreOptions options,
        ICurrentTenantAccessor currentTenantAccessor)
    {
        if (options.EnableSoftDeleteFilter)
        {
            provider.QueryFilter.AddTableFilter<ISoftDelete>(entity => !entity.IsDeleted);
        }

        if (options.EnableTenantFilter)
        {
            // 租户过滤策略：
            // - 当前无租户上下文（跨租户/平台运维）：不过滤
            // - 当前有租户上下文：只看（自己租户数据 OR 全局模板 TenantId=0）
            provider.QueryFilter.AddTableFilter<IMultiTenantEntity>(
                entity => currentTenantAccessor.Current == null ||
                          !currentTenantAccessor.Current.TenantId.HasValue ||
                          entity.TenantId == currentTenantAccessor.Current.TenantId.Value ||
                          entity.TenantId == 0);
        }

        foreach (var filter in options.GlobalFilters)
        {
            var entityType = filter.Key;
            var funcType = typeof(Func<,>).MakeGenericType(entityType, typeof(bool));
            var parameter = Expression.Parameter(entityType, "e");
            var filterFunc = filter.Value;
            var convertedParam = Expression.Convert(parameter, typeof(object));
            var filterCall = Expression.Call(
                Expression.Constant(filterFunc.Target),
                filterFunc.Method,
                convertedParam);
            var lambda = Expression.Lambda(funcType, filterCall, parameter);

            var methodInfo = typeof(QueryFilterProvider).GetMethod("AddTableFilter")?.MakeGenericMethod(entityType);
            methodInfo?.Invoke(provider.QueryFilter, [lambda]);
        }
    }

    /// <summary>
    /// 配置数据库 Aop 设置
    /// </summary>
    /// <param name="scopeFactory"></param>
    /// <param name="dbProvider"></param>
    /// <param name="options"></param>
    /// <param name="dataExecutingHandler"></param>
    private static void SetSugarAop(
        IServiceScopeFactory scopeFactory,
        SqlSugarScopeProvider dbProvider,
        XiHanSqlSugarCoreOptions options,
        SqlSugarDataExecutingHandler dataExecutingHandler)
    {
        var config = dbProvider.CurrentConnectionConfig;

        // 设置超时时间
        dbProvider.Ado.CommandTimeOut = options.SlowSqlThresholdMilliseconds / 1000;

        if (options.EnableSqlLog)
        {
            dbProvider.Aop.OnLogExecuting = (sql, parameters) =>
            {
                HandleSqlExecutingLog(config, sql, parameters);
            };
        }

        if (options.EnableSqlErrorLog)
        {
            dbProvider.Aop.OnError = ex =>
            {
                HandleSqlOnErrorLog(config, ex);
            };
        }

        if (options.EnableSlowSqlLog)
        {
            dbProvider.Aop.OnLogExecuted = (sql, parameters) =>
            {
                HandleSqlSlowLog(dbProvider, options, config, sql, parameters);
            };
        }

        dbProvider.Aop.DataExecuting = (_, entityInfo) =>
        {
            dataExecutingHandler.Handle(entityInfo);
        };

        // 实体差异日志 AOP：基于 SqlSugar 原生 OnDiffLogEvent 的真 AOP 审计
        // 仓储层通过 .EnableDiffLogEvent(businessData) 启用，本处理器自动生成审计记录
        if (options.EnableAuditLog)
        {
            SqlSugarDiffLogAop.Attach(scopeFactory, dbProvider);
        }

        options.ConfigureDbAction?.Invoke(dbProvider);
    }

    /// <summary>
    /// 处理SQL执行日志记录
    /// </summary>
    /// <param name="config"></param>
    /// <param name="sql"></param>
    /// <param name="parameters"></param>
    private static void HandleSqlExecutingLog(ConnectionConfig config, string sql, SugarParameter[] parameters)
    {
        var sqlInfo = UtilMethods.GetSqlString(config.DbType, sql, parameters);
        if (sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            LogHelper.Handle(sqlInfo);
        }

        if (sql.TrimStart().StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase) ||
            sql.TrimStart().StartsWith("INSERT", StringComparison.OrdinalIgnoreCase))
        {
            LogHelper.Warn(sqlInfo);
        }

        if (sql.TrimStart().StartsWith("DELETE", StringComparison.OrdinalIgnoreCase) ||
            sql.TrimStart().StartsWith("TRUNCATE", StringComparison.OrdinalIgnoreCase))
        {
            LogHelper.Error(sqlInfo);
        }
    }

    /// <summary>
    /// 处理SQL执行异常日志记录
    /// </summary>
    /// <param name="config"></param>
    /// <param name="ex"></param>
    private static void HandleSqlOnErrorLog(ConnectionConfig config, SqlSugarException ex)
    {
        var sql = ex.Sql ?? string.Empty;
        var parameters = ex.Parametres as SugarParameter[] ?? [];
        var sqlInfo = UtilMethods.GetSqlString(config.DbType, sql, parameters);
        LogHelper.Error(ex, $"SQL执行异常: {sqlInfo}");
    }

    /// <summary>
    /// 处理SQL慢查询日志记录
    /// </summary>
    /// <param name="dbProvider"></param>
    /// <param name="options"></param>
    /// <param name="config"></param>
    /// <param name="sql"></param>
    /// <param name="parameters"></param>
    private static void HandleSqlSlowLog(SqlSugarScopeProvider dbProvider, XiHanSqlSugarCoreOptions options, ConnectionConfig config, string sql, SugarParameter[] parameters)
    {
        var elapsedMs = dbProvider.Ado.SqlExecutionTime.TotalMilliseconds;
        if (elapsedMs < options.SlowSqlThresholdMilliseconds)
        {
            return;
        }

        var sqlInfo = UtilMethods.GetSqlString(config.DbType, sql, parameters);
        LogHelper.Warn($"慢SQL({elapsedMs}ms): {sqlInfo}");
    }

}
