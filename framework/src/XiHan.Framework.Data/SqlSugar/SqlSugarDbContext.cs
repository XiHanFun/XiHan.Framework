#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SqlSugarDbContext
// Guid:a7d5e2bc-f843-4e8a-9f1d-e3c79db3610a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2023/11/15 08:35:21
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;
using SqlSugar;
using System.Linq.Expressions;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Data.SqlSugar.Options;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Uow.Abstracts;
using XiHan.Framework.Utils.Threading;

namespace XiHan.Framework.Data.SqlSugar;

/// <summary>
/// SqlSugar数据库上下文
/// </summary>
public class SqlSugarDbContext : ISqlSugarDbContext, ITransactionApi, ISupportsSavingChanges, ISupportsRollback, IScopedDependency
{
    private static readonly AsyncLocal<bool?> TenantFilterEnabled = new();

    // 选项配置
    private readonly XiHanSqlSugarCoreOptions _options;

    // 服务提供器
    private readonly ITransientCachedServiceProvider _transientCachedServiceProvider;

    // 分布式ID生成器
    private readonly IDistributedIdGenerator<long> _idGenerator;

    // 当前租户
    private readonly ICurrentTenant _currentTenant;

    // SqlSugarScope 实现了 ISqlSugarClient 接口，可以直接返回
    private readonly SqlSugarScope _sqlSugarScope;

    // 配置过的连接标识
    private readonly HashSet<string> _configIds;

    // 标记是否有活动事务
    private bool _hasActiveTransaction;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options"></param>
    /// <param name="transientCachedServiceProvider"></param>
    /// <param name="idGenerator">分布式ID生成器（可选）</param>
    /// <param name="currentTenant">当前租户上下文</param>
    public SqlSugarDbContext(
        IOptions<XiHanSqlSugarCoreOptions> options,
        ITransientCachedServiceProvider transientCachedServiceProvider,
        IDistributedIdGenerator<long> idGenerator,
        ICurrentTenant currentTenant)
    {
        _options = options.Value;
        _transientCachedServiceProvider = transientCachedServiceProvider;
        _idGenerator = idGenerator;
        _currentTenant = currentTenant;
        _configIds = _options.ConnectionConfigs
            .Select(x => x.ConfigId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _sqlSugarScope = CreateSqlSugarScope();
    }

    /// <summary>
    /// 当前租户标识
    /// </summary>
    public long? CurrentTenantId => _currentTenant.Id;

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
        var tenantConfigId = GetCurrentTenantConfigId();
        if (tenantConfigId is null)
        {
            return _sqlSugarScope;
        }

        return _sqlSugarScope.GetConnectionScope(tenantConfigId);
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
    /// 创建带租户隔离的查询
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <returns></returns>
    public ISugarQueryable<TEntity> CreateQueryable<TEntity>() where TEntity : class, new()
    {
        var queryable = GetClient().Queryable<TEntity>();
        return ApplyTenantFilter(queryable);
    }

    /// <summary>
    /// 尝试写入实体租户标识
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <param name="entity"></param>
    public void TrySetTenantId<TEntity>(TEntity entity) where TEntity : class
    {
        var tenantId = CurrentTenantId;
        if (!tenantId.HasValue)
        {
            return;
        }

        var property = typeof(TEntity).GetProperty("TenantId");
        if (property is null || !property.CanWrite)
        {
            return;
        }

        if (property.PropertyType == typeof(long))
        {
            var currentValue = (long)property.GetValue(entity)!;
            if (currentValue == 0)
            {
                property.SetValue(entity, tenantId.Value);
            }
            return;
        }

        if (property.PropertyType == typeof(long?))
        {
            var currentValue = (long?)property.GetValue(entity);
            if (!currentValue.HasValue || currentValue.Value == 0)
            {
                property.SetValue(entity, tenantId.Value);
            }
        }
    }

    /// <summary>
    /// 临时禁用租户过滤
    /// </summary>
    /// <returns></returns>
    public IDisposable DisableTenantFilter()
    {
        var previous = TenantFilterEnabled.Value;
        TenantFilterEnabled.Value = false;
        return new DisposeAction(() => TenantFilterEnabled.Value = previous);
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
    /// 释放
    /// </summary>
    public void Dispose()
    {
        // 释放SqlSugarScope资源
        _sqlSugarScope?.Dispose();

        // 调用 GC.SuppressFinalize 以防止派生类需要重新实现 IDisposable
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 创建SqlSugarScope
    /// </summary>
    /// <returns></returns>
    private SqlSugarScope CreateSqlSugarScope()
    {
        StaticConfig.CustomSnowFlakeFunc = _idGenerator.NextId;

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

    private string? GetCurrentTenantConfigId()
    {
        if (!CurrentTenantId.HasValue)
        {
            return null;
        }

        var tenantIdText = CurrentTenantId.Value.ToString();
        if (_configIds.Contains(tenantIdText))
        {
            return tenantIdText;
        }

        var prefixed = $"Tenant_{tenantIdText}";
        if (_configIds.Contains(prefixed))
        {
            return prefixed;
        }

        return null;
    }

    private ISugarQueryable<TEntity> ApplyTenantFilter<TEntity>(ISugarQueryable<TEntity> queryable)
        where TEntity : class, new()
    {
        if ((TenantFilterEnabled.Value ?? true) is false)
        {
            return queryable;
        }

        var tenantId = CurrentTenantId;
        if (!tenantId.HasValue)
        {
            return queryable;
        }

        var property = typeof(TEntity).GetProperty("TenantId");
        if (property is null)
        {
            return queryable;
        }

        if (property.PropertyType != typeof(long) && property.PropertyType != typeof(long?))
        {
            return queryable;
        }

        var parameter = Expression.Parameter(typeof(TEntity), "e");
        var propertyAccess = Expression.Property(parameter, property);
        Expression targetValue = property.PropertyType == typeof(long)
            ? Expression.Constant(tenantId.Value, typeof(long))
            : Expression.Convert(Expression.Constant(tenantId.Value, typeof(long)), typeof(long?));
        var body = Expression.Equal(propertyAccess, targetValue);
        var lambda = Expression.Lambda<Func<TEntity, bool>>(body, parameter);
        return queryable.Where(lambda);
    }
}
