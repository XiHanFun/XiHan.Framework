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

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlSugar;
using System.Linq.Expressions;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Data.SqlSugar.Extensions;
using XiHan.Framework.Data.SqlSugar.Options;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Security.Users;
using XiHan.Framework.Uow.Abstracts;
using XiHan.Framework.Utils.Threading;

namespace XiHan.Framework.Data.SqlSugar;

/// <summary>
/// SqlSugar 数据库上下文。负责多连接管理、租户隔离、全局过滤器、SQL 日志与异常/慢查询记录、以及插入/更新时审计字段与主键的自动赋值。
/// </summary>
public class SqlSugarDbContext : ISqlSugarDbContext, ITransactionApi, ISupportsSavingChanges, ISupportsRollback, IScopedDependency
{
    /// <summary>
    /// 租户过滤是否启用
    /// </summary>
    private static readonly AsyncLocal<bool?> TenantFilterEnabled = new();

    /// <summary>
    /// 选项配置
    /// </summary>
    private readonly XiHanSqlSugarCoreOptions _options;

    /// <summary>
    /// 服务提供器（用于解析 ICurrentUser 等）
    /// </summary>
    private readonly ITransientCachedServiceProvider _transientCachedServiceProvider;

    /// <summary>
    /// 日志记录器
    /// </summary>
    private readonly ILogger<SqlSugarDbContext> _logger;

    /// <summary>
    /// 分布式 ID 生成器（雪花 Id）
    /// </summary>
    private readonly IDistributedIdGenerator<long> _idGenerator;

    /// <summary>
    /// 当前租户上下文
    /// </summary>
    private readonly ICurrentTenant _currentTenant;

    /// <summary>
    /// SqlSugarScope 实例，实现 ISqlSugarClient
    /// </summary>
    private readonly SqlSugarScope _sqlSugarScope;

    /// <summary>
    /// 已配置的连接标识集合（用于租户库匹配）
    /// </summary>
    private readonly HashSet<string> _configIds;

    /// <summary>
    /// 是否存在进行中的事务
    /// </summary>
    private bool _hasActiveTransaction;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">SqlSugar 核心选项（连接、全局过滤、日志开关等）</param>
    /// <param name="transientCachedServiceProvider">瞬态缓存服务提供器</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="idGenerator">分布式 ID 生成器（可选）</param>
    /// <param name="currentTenant">当前租户上下文</param>
    public SqlSugarDbContext(
        IOptions<XiHanSqlSugarCoreOptions> options,
        ITransientCachedServiceProvider transientCachedServiceProvider,
        ILogger<SqlSugarDbContext> logger,
        IDistributedIdGenerator<long> idGenerator,
        ICurrentTenant currentTenant)
    {
        _options = options.Value;
        _transientCachedServiceProvider = transientCachedServiceProvider;
        _logger = logger;
        _idGenerator = idGenerator;
        _currentTenant = currentTenant;
        _configIds = _options.ConnectionConfigs
            .Select(x => x.ConfigId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _sqlSugarScope = CreateSqlSugarScope();
    }

    /// <summary>
    /// 当前租户标识。无租户时为 null。
    /// </summary>
    public long? CurrentTenantId => _currentTenant.Id;

    /// <summary>
    /// 保存实体变更。若有活动事务则仅提交事务；SqlSugar 无变更跟踪，此处主要与工作单元配合。
    /// </summary>
    /// <param name="cancellationToken">取消令牌（当前未使用）</param>
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
    /// 获取 SqlSugar 客户端。若有当前租户且存在对应 ConfigId 则返回该租户连接，否则返回默认 Scope。
    /// </summary>
    /// <returns>当前租户对应的 ISqlSugarClient 或默认连接</returns>
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
    /// 获取 SqlSugarScope 实例（多连接统一入口）。
    /// </summary>
    /// <returns>当前上下文的 SqlSugarScope</returns>
    public SqlSugarScope GetScope()
    {
        return _sqlSugarScope;
    }

    /// <summary>
    /// 创建带租户隔离的查询。若启用租户且 TEntity 含 TenantId，则自动附加 TenantId 过滤条件。
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <returns>已应用租户过滤的 ISugarQueryable</returns>
    public ISugarQueryable<TEntity> CreateQueryable<TEntity>() where TEntity : class, new()
    {
        var queryable = GetClient().Queryable<TEntity>();
        return ApplyTenantFilter(queryable);
    }

    /// <summary>
    /// 尝试为实体设置当前租户标识。仅当实体有 TenantId 属性且当前值为 0 或未设置时写入。
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="entity">要设置租户 Id 的实体</param>
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
    /// 临时禁用租户过滤。返回的 IDisposable 在释放时恢复原状态，用于跨租户查询等场景。
    /// </summary>
    /// <returns>释放时恢复租户过滤的句柄</returns>
    public IDisposable DisableTenantFilter()
    {
        var previous = TenantFilterEnabled.Value;
        TenantFilterEnabled.Value = false;
        return new DisposeAction(() => TenantFilterEnabled.Value = previous);
    }

    /// <summary>
    /// 从当前作用域解析服务。用于在 AOP 等场景获取 ICurrentUser 等依赖。
    /// </summary>
    /// <typeparam name="TService">服务类型</typeparam>
    /// <returns>服务实例，未注册时返回 null</returns>
    public TService? GetService<TService>() where TService : class
    {
        return _transientCachedServiceProvider.GetService<TService>(defaultValue: null!);
    }

    /// <summary>
    /// 开始数据库事务。若已有活动事务则直接返回（不嵌套）。
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
    /// 提交当前事务。若无活动事务则直接返回；提交后清除活动事务标记。
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
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
    /// 回滚当前事务。若无活动事务则直接返回；回滚后清除活动事务标记。
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
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
    /// 释放 SqlSugarScope 及相关资源。
    /// </summary>
    public void Dispose()
    {
        // 释放SqlSugarScope资源
        _sqlSugarScope?.Dispose();

        // 调用 GC.SuppressFinalize 以防止派生类需要重新实现 IDisposable
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 将 SQL 与参数还原为可读的完整 SQL 字符串；还原失败时返回原始 sql。
    /// </summary>
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

    /// <summary>
    /// 创建 SqlSugarScope：注册雪花 Id、连接配置、全局过滤器、SQL/异常/慢查询日志及 DataExecuting 审计赋值。
    /// </summary>
    /// <returns>配置完成的 SqlSugarScope 实例</returns>
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

            if (_options.EnableSqlErrorLog)
            {
                db.Aop.OnError = ex =>
                {
                    var sql = ex.Sql ?? string.Empty;
                    var parameters = ex.Parametres as SugarParameter[] ?? [];
                    _logger.LogError(ex, "SQL执行异常: {Sql}", BuildNativeSql(sql, parameters));
                };
            }

            if (_options.EnableSlowSqlLog)
            {
                db.Aop.OnLogExecuted = (sql, parameters) =>
                {
                    var elapsedMs = db.Ado.SqlExecutionTime.TotalMilliseconds;
                    if (elapsedMs < _options.SlowSqlThresholdMilliseconds)
                    {
                        return;
                    }

                    _logger.LogWarning(
                        "慢SQL({Elapsed}ms): {Sql}",
                        elapsedMs,
                        BuildNativeSql(sql, parameters));
                };
            }

            db.Aop.DataExecuting = (_, entityInfo) =>
            {
                FillDataAuditFields(entityInfo);
            };

            // 执行配置Action
            _options.ConfigureDbAction?.Invoke(db);
        });
    }

    /// <summary>
    /// 将当前执行的 SQL 及参数交给配置的 SqlLogAction 输出；未配置时无操作。
    /// </summary>
    /// <param name="sql">原始 SQL</param>
    /// <param name="parameters">参数列表</param>
    private void LogSqlExecuting(string sql, SugarParameter[] parameters)
    {
        _options.SqlLogAction?.Invoke(sql, parameters);
    }

    /// <summary>
    /// 在 DataExecuting 中填充审计字段：插入时赋雪花 Id、ToCreated（创建人/时间/租户等）；更新时 ToModified、ToDeleted（软删时填删除人/时间）。
    /// </summary>
    /// <param name="entityInfo">SqlSugar 数据过滤模型</param>
    private void FillDataAuditFields(DataFilterModel entityInfo)
    {
        if (entityInfo.EntityValue is null)
        {
            return;
        }

        var currentUser = GetService<ICurrentUser>();
        var auditContext = EntityAuditContext.From(currentUser, CurrentTenantId);

        if (entityInfo.OperationType == DataFilterType.InsertByObject)
        {
            entityInfo.TrySetSnowflakeId(_idGenerator.NextId());
            entityInfo.ToCreated(auditContext);
            return;
        }

        if (entityInfo.OperationType == DataFilterType.UpdateByObject)
        {
            entityInfo.ToModified(auditContext);
            entityInfo.ToDeleted(auditContext);
        }
    }

    /// <summary>
    /// 根据当前租户 Id 解析对应的 SqlSugar ConfigId（先匹配租户 Id，再匹配 Tenant_{id}）。
    /// </summary>
    /// <returns>匹配到的 ConfigId，无匹配时返回 null</returns>
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

    /// <summary>
    /// 若启用租户且 TEntity 有 TenantId 属性，则为查询附加 TenantId = CurrentTenantId 条件；禁用租户过滤或无租户时不处理。
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="queryable">原始查询</param>
    /// <returns>附加租户条件后的查询</returns>
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
