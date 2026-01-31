#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SqlSugarDbContext
// Guid:a7d5e2bc-f843-4e8a-9f1d-e3c79db3610a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2023-11-15 8:35:21
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;
using SqlSugar;
using System.Linq.Expressions;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Data.SqlSugar.Options;
using XiHan.Framework.Uow.Abstracts;

namespace XiHan.Framework.Data.SqlSugar;

/// <summary>
/// SqlSugar数据库上下文
/// </summary>
public class SqlSugarDbContext : ISqlSugarDbContext, ITransactionApi, ISupportsSavingChanges, ISupportsRollback, IScopedDependency
{
    // 选项配置
    private readonly XiHanSqlSugarCoreOptions _options;

    // 服务提供器
    private readonly ITransientCachedServiceProvider _transientCachedServiceProvider;

    // SqlSugarScope 实现了 ISqlSugarClient 接口，可以直接返回
    private readonly SqlSugarScope _sqlSugarScope;

    // 标记是否有活动事务
    private bool _hasActiveTransaction;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options"></param>
    /// <param name="transientCachedServiceProvider"></param>
    public SqlSugarDbContext(IOptions<XiHanSqlSugarCoreOptions> options, ITransientCachedServiceProvider transientCachedServiceProvider)
    {
        _options = options.Value;
        _transientCachedServiceProvider = transientCachedServiceProvider;

        _sqlSugarScope = CreateSqlSugarScope();
    }

    /// <summary>
    /// 保存实体变更
    /// </summary>
    /// <param name="cancellationToken">取消令牌(未使用)</param>
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // SqlSugar 没有提供 SaveChangesAsync 方法
        if (_hasActiveTransaction)
        {
            return;
        }
        // 提交事务
        await CommitAsync(cancellationToken);
    }

    /// <summary>
    /// 获取SqlSugarClient客户端
    /// </summary>
    /// <returns></returns>
    public ISqlSugarClient GetClient()
    {
        // SqlSugarScope 实现了 ISqlSugarClient 接口，可以直接返回
        return _sqlSugarScope;
    }

    /// <summary>
    /// 获取SqlSugarScope客户端
    /// </summary>
    /// <returns></returns>
    public SqlSugarScope GetScope()
    {
        return _sqlSugarScope;
    }

    /// <summary>
    /// 获取SimpleClient简单客户端
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <returns></returns>
    public SimpleClient<T> GetSimpleClient<T>() where T : class, new()
    {
        return _sqlSugarScope.GetSimpleClient<T>();
    }

    /// <summary>
    /// 开始事务
    /// </summary>
    public void BeginTransaction()
    {
        if (_hasActiveTransaction)
        {
            return;
        }

        _sqlSugarScope.Ado.BeginTran();
        _hasActiveTransaction = true;
    }

    /// <summary>
    /// 提交事务
    /// </summary>
    /// <param name="cancellationToken"></param>
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (!_hasActiveTransaction)
        {
            return;
        }

        try
        {
            await Task.Run(_sqlSugarScope.Ado.CommitTranAsync, cancellationToken);
        }
        finally
        {
            _hasActiveTransaction = false;
        }
    }

    /// <summary>
    /// 回滚事务
    /// </summary>
    /// <param name="cancellationToken"></param>
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (!_hasActiveTransaction)
        {
            return;
        }

        try
        {
            await Task.Run(_sqlSugarScope.Ado.RollbackTranAsync, cancellationToken);
        }
        finally
        {
            _hasActiveTransaction = false;
        }
    }

    /// <summary>
    /// 创建SqlSugarScope
    /// </summary>
    /// <returns></returns>
    private SqlSugarScope CreateSqlSugarScope()
    {
        var connectionConfigs = new List<ConnectionConfig>();
        foreach (var connConfig in _options.ConnectionConfigs)
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
            // 配置全局过滤器 - 使用表达式树创建Lambda表达式
            foreach (var filter in _options.GlobalFilters)
            {
                var entityType = filter.Key;
                var funcType = typeof(Func<,>).MakeGenericType(entityType, typeof(bool));

                // 创建参数表达式
                var parameter = Expression.Parameter(entityType, "e");

                // 提供的过滤器是一个Func<object, bool>，在这里我们将其转换为对应实体类型的表达式
                var filterFunc = filter.Value;

                // 创建对filterFunc的方法调用表达式，并将参数类型转换
                var convertedParam = Expression.Convert(parameter, typeof(object));
                var filterCall = Expression.Call(
                    Expression.Constant(filterFunc.Target),
                    filterFunc.Method,
                    convertedParam);

                // 创建Lambda表达式
                var lambda = Expression.Lambda(funcType, filterCall, parameter);

                // 使用反射调用AddTableFilter方法
                var methodInfo = typeof(QueryFilterProvider).GetMethod("AddTableFilter")?.MakeGenericMethod(entityType);
                methodInfo?.Invoke(db.QueryFilter, [lambda]);
            }

            // 开启SQL执行日志
            if (_options.EnableSqlLog)
            {
                db.Aop.OnLogExecuting = (sql, parameters) =>
                {
                    LogSqlExecuting(sql, parameters);
                };
            }

            // 执行配置Action
            _options.ConfigureDbAction?.Invoke(db);
        });
    }

    /// <summary>
    /// 打印执行SQL日志
    /// </summary>
    /// <param name="sql"></param>
    /// <param name="parameters"></param>
    private void LogSqlExecuting(string sql, SugarParameter[] parameters)
    {
        _options.SqlLogAction?.Invoke(sql, parameters);
    }
}
