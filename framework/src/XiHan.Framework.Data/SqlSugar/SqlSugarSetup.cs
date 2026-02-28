#region <<版权版本注释>>

//
// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SqlSugarSetup
// Guid:7f77cbb1-2d02-44cc-8d9e-1c7d6d9c8d4e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/28 12:00:00
// ----------------------------------------------------------------
//

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlSugar;
using System.Linq.Expressions;
using XiHan.Framework.Data.SqlSugar.Extensions;
using XiHan.Framework.Data.SqlSugar.Options;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Security.Users;

namespace XiHan.Framework.Data.SqlSugar;

/// <summary>
/// SqlSugar 初始化配置
/// </summary>
public static class SqlSugarSetup
{
    /// <summary>
    /// 创建 SqlSugarScope
    /// </summary>
    /// <param name="serviceProvider">服务提供者</param>
    /// <returns></returns>
    public static SqlSugarScope CreateScope(IServiceProvider serviceProvider)
    {
        var options = serviceProvider.GetRequiredService<IOptions<XiHanSqlSugarCoreOptions>>().Value;
        var idGenerator = serviceProvider.GetRequiredService<IDistributedIdGenerator<long>>();
        //var logger = serviceProvider.GetRequiredService<ILogger<SqlSugarSetup>>();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        return CreateScope(options, idGenerator, scopeFactory);
    }

    private static SqlSugarScope CreateScope(
        XiHanSqlSugarCoreOptions options,
        IDistributedIdGenerator<long> idGenerator,
        //ILogger logger,
        IServiceScopeFactory scopeFactory)
    {
        StaticConfig.CustomSnowFlakeFunc = idGenerator.NextId;

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

        return new SqlSugarScope(connectionConfigs, db =>
        {
            ApplyGlobalFilters(db, options);

            if (options.EnableSqlLog)
            {
                db.Aop.OnLogExecuting = (sql, parameters) =>
                {
                    LogSqlExecuting(options, sql, parameters);
                };
            }

            if (options.EnableSqlErrorLog)
            {
                db.Aop.OnError = ex =>
                {
                    var sql = ex.Sql ?? string.Empty;
                    var parameters = ex.Parametres as SugarParameter[] ?? [];
                    //logger.LogError(ex, "SQL执行异常: {Sql}", BuildNativeSql(sql, parameters));
                };
            }

            if (options.EnableSlowSqlLog)
            {
                db.Aop.OnLogExecuted = (sql, parameters) =>
                {
                    var elapsedMs = db.Ado.SqlExecutionTime.TotalMilliseconds;
                    if (elapsedMs < options.SlowSqlThresholdMilliseconds)
                    {
                        return;
                    }

                    //logger.LogWarning(
                    //    "慢SQL({Elapsed}ms): {Sql}",
                    //    elapsedMs,
                    //    BuildNativeSql(sql, parameters));
                };
            }

            db.Aop.DataExecuting = (_, entityInfo) =>
            {
                FillDataAuditFields(entityInfo, idGenerator, scopeFactory);
            };

            options.ConfigureDbAction?.Invoke(db);
        });
    }

    private static void ApplyGlobalFilters(SqlSugarClient db, XiHanSqlSugarCoreOptions options)
    {
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
            methodInfo?.Invoke(db.QueryFilter, [lambda]);
        }
    }

    private static void LogSqlExecuting(XiHanSqlSugarCoreOptions options, string sql, SugarParameter[] parameters)
    {
        options.SqlLogAction?.Invoke(sql, parameters);
    }

    private static string BuildNativeSql(string sql, SugarParameter[] parameters)
    {
        try
        {
            return UtilMethods.GetNativeSql(sql, parameters);
        }
        catch
        {
            return sql;
        }
    }

    private static void FillDataAuditFields(
        DataFilterModel entityInfo,
        IDistributedIdGenerator<long> idGenerator,
        IServiceScopeFactory scopeFactory)
    {
        if (entityInfo.EntityValue is null)
        {
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var currentUser = scope.ServiceProvider.GetService<ICurrentUser>();
        var currentTenant = scope.ServiceProvider.GetService<ICurrentTenant>();
        var auditContext = EntityAuditContext.From(currentUser, currentTenant?.Id);

        if (entityInfo.OperationType == DataFilterType.InsertByObject)
        {
            entityInfo.TrySetSnowflakeId(idGenerator.NextId());
            entityInfo.ToCreated(auditContext);
            return;
        }

        if (entityInfo.OperationType == DataFilterType.UpdateByObject)
        {
            entityInfo.ToModified(auditContext);
            entityInfo.ToDeleted(auditContext);
        }
    }
}
