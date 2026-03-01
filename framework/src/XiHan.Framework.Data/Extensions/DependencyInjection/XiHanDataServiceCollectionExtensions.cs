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
using XiHan.Framework.Data.SqlSugar;
using XiHan.Framework.Data.SqlSugar.Extensions;
using XiHan.Framework.Data.SqlSugar.Initializers;
using XiHan.Framework.Data.SqlSugar.Metadata;
using XiHan.Framework.Data.SqlSugar.Options;
using XiHan.Framework.Data.SqlSugar.Repository;
using XiHan.Framework.Data.SqlSugar.Seeders;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.Domain.Repositories;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Security.Users;
using XiHan.Framework.Utils.Exceptions;
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
        services.TryAddSingleton(sp => CreateScope(sp));
        services.TryAddScoped<ISqlSugarClientProvider, SqlSugarClientProvider>();
        services.TryAddScoped(sp => sp.GetRequiredService<ISqlSugarClientProvider>().GetClient());

        // 注册仓储服务
        services.TryAddScoped(typeof(IReadOnlyRepositoryBase<,>), typeof(SqlSugarReadOnlyRepository<,>));
        services.TryAddScoped(typeof(IRepositoryBase<,>), typeof(SqlSugarRepositoryBase<,>));
        services.TryAddScoped(typeof(ISoftDeleteRepositoryBase<,>), typeof(SqlSugarSoftDeleteRepository<,>));
        services.TryAddScoped(typeof(IAuditedRepository<,>), typeof(SqlSugarAuditedRepository<,>));
        services.TryAddScoped(typeof(IAggregateRootRepository<,>), typeof(SqlSugarAggregateRepository<,>));

        services.TryAddScoped<IDatabaseMetadataProvider, SqlSugarDatabaseMetadataProvider>();
        services.TryAddScoped<IEntityAuditContextProvider, NullEntityAuditContextProvider>();
        services.TryAddScoped<IEntityAuditLogWriter, NullEntityAuditLogWriter>();

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
    /// <returns>服务集合</returns>
    private static SqlSugarScope CreateScope(IServiceProvider services)
    {
        var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();
        var options = services.GetRequiredService<IOptions<XiHanSqlSugarCoreOptions>>().Value;
        var idGenerator = services.GetRequiredService<IDistributedIdGenerator<long>>();

        // 注入多库参考，官方文档 https://www.donet5.com/Home/Doc?typeId=2405
        var connectionConfigs = new List<ConnectionConfig>();
        foreach (var connConfig in options.ConnectionConfigs)
        {
            connectionConfigs.Add(new ConnectionConfig
            {
                ConfigId = connConfig.ConfigId,
                ConnectionString = connConfig.ConnectionString,
                DbType = connConfig.DbType,
                IsAutoCloseConnection = connConfig.IsAutoCloseConnection,
                InitKeyType = connConfig.InitKeyType,
                MoreSettings = connConfig.MoreSettings,
                SlaveConnectionConfigs = connConfig.SlaveConnectionConfigs
            });
        }

        // 设置自定义全局雪花ID生成器
        StaticConfig.CustomSnowFlakeFunc = idGenerator.NextId;
        var sugarScope = new SqlSugarScope(connectionConfigs, client =>
        {
            connectionConfigs.ForEach(config =>
            {
                var dbProvider = client.GetConnectionScope(config.ConfigId);

                ApplySugarGlobalFilters(dbProvider, options);

                SetSugarAop(scopeFactory, dbProvider, options, idGenerator);
            });
        });

        return sugarScope;
    }

    /// <summary>
    /// 应用全局过滤器
    /// </summary>
    /// <param name="provider"></param>
    /// <param name="options"></param>
    private static void ApplySugarGlobalFilters(SqlSugarScopeProvider provider, XiHanSqlSugarCoreOptions options)
    {
        // 动态添加全局过滤器参考，官方文档 https://www.donet5.com/home/doc?masterId=1&typeId=1205
        // 全局过滤器：作用是设置一个查询条件，当你使用查询操作的时候满足这个条件，那么你的语句就会附加你设置的条件。
        // 应用场景：过滤假删除数据，比如，每个查询后面都要加 IsDeleted = false

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

        //// 获取当前请求上下文信息
        //var httpCurrent = App.HttpContextCurrent;
        //if (httpCurrent != null)
        //{
        //    var user = httpCurrent.GetAuthInfo();
        //    // 非超级管理员或未登录用户，添加过滤假删除数据的条件
        //    provider.QueryFilter.AddTableFilterIF<ISoftDelete>(user.IsSuperAdmin == false, it => it.IsDeleted == false);
        //}
    }

    /// <summary>
    /// 配置数据库 Aop 设置
    /// </summary>
    /// <param name="scopeFactory"></param>
    /// <param name="dbProvider"></param>
    /// <param name="options"></param>
    /// <param name="idGenerator"></param>
    /// <exception cref="CustomException"></exception>
    private static void SetSugarAop(IServiceScopeFactory scopeFactory, SqlSugarScopeProvider dbProvider, XiHanSqlSugarCoreOptions options, IDistributedIdGenerator<long> idGenerator)
    {
        var config = dbProvider.CurrentConnectionConfig;
        var configId = config.ConfigId;

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
                HandleSqlOnErrorLog(ex);
            };
        }

        if (options.EnableSlowSqlLog)
        {
            dbProvider.Aop.OnLogExecuted = (sql, parameters) =>
            {
                HandleSqlSlowLog(dbProvider, options, sql, parameters);
            };
        }

        dbProvider.Aop.DataExecuting = (value, entityInfo) =>
        {
            HandleSqlDataExecuting(scopeFactory, entityInfo, idGenerator);
        };

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
        //options.SqlLogAction?.Invoke(sql, parameters);

        // 构建SQL日志信息
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
    /// <param name="ex"></param>
    private static void HandleSqlOnErrorLog(SqlSugarException ex)
    {
        var sql = ex.Sql ?? string.Empty;
        var parameters = ex.Parametres as SugarParameter[] ?? [];
        // 构建原生SQL语句
        var sqlNative = UtilMethods.GetNativeSql(sql, parameters);
        LogHelper.Error(ex, $"SQL执行异常: {sqlNative}");
    }

    /// <summary>
    /// 处理SQL慢查询日志记录
    /// </summary>
    /// <param name="dbProvider"></param>
    /// <param name="options"></param>
    /// <param name="sql"></param>
    /// <param name="parameters"></param>
    private static void HandleSqlSlowLog(SqlSugarScopeProvider dbProvider, XiHanSqlSugarCoreOptions options, string sql, SugarParameter[] parameters)
    {
        var elapsedMs = dbProvider.Ado.SqlExecutionTime.TotalMilliseconds;
        if (elapsedMs < options.SlowSqlThresholdMilliseconds)
        {
            return;
        }
        // 构建原生SQL语句
        var sqlNative = UtilMethods.GetNativeSql(sql, parameters);
        LogHelper.Warn($"慢SQL({elapsedMs}ms): {sqlNative}");
    }

    /// <summary>
    /// 处理SQL数据执行事件
    /// </summary>
    /// <param name="scopeFactory"></param>
    /// <param name="entityInfo"></param>
    /// <param name="idGenerator"></param>
    private static void HandleSqlDataExecuting(IServiceScopeFactory scopeFactory, DataFilterModel entityInfo, IDistributedIdGenerator<long> idGenerator)
    {
        if (entityInfo.EntityValue is null)
        {
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var currentUser = scope.ServiceProvider.GetService<ICurrentUser>();
        var currentTenant = scope.ServiceProvider.GetService<ICurrentTenant>();
        var auditContext = EntityAuditContext.From(currentUser, currentTenant?.Id);

        switch (entityInfo.OperationType)
        {
            // 新增操作
            case DataFilterType.InsertByObject:
                entityInfo.TrySetSnowflakeId(idGenerator.NextId());
                entityInfo.ToCreated(auditContext);
                break;
            // 更新操作
            case DataFilterType.UpdateByObject:
                entityInfo.ToModified(auditContext);
                entityInfo.ToDeleted(auditContext);
                break;
            // 删除操作
            case DataFilterType.DeleteByObject:
                entityInfo.ToDeleted(auditContext);
                break;
        }
    }
}
