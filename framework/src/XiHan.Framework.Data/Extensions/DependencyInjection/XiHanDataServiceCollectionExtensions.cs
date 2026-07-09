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
using System.Diagnostics;
using System.Linq.Expressions;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Tracing;
using XiHan.Framework.Data.Auditing;
using XiHan.Framework.Data.SqlSugar.Auditing;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.Data.SqlSugar.Extensions;
using XiHan.Framework.Data.SqlSugar.HealthCheck;
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
        // 连接配置器：统一为静态/运行时动态连接装配全局过滤器与 AOP
        services.TryAddSingleton<ISqlSugarConnectionConfigurator, SqlSugarConnectionConfigurator>();

        // 从库健康探针（现成探针，默认关闭；由 EnableSlaveHealthCheck 开关，未开启则空转零成本）
        services.AddHostedService<SqlSugarSlaveHealthCheckService>();

        // 注册仓储服务
        services.TryAddScoped(typeof(IReadOnlyRepositoryBase<,>), typeof(SqlSugarReadOnlyRepository<,>));
        services.TryAddScoped(typeof(IRepositoryBase<,>), typeof(SqlSugarRepositoryBase<,>));
        services.TryAddScoped(typeof(ISoftDeleteRepositoryBase<,>), typeof(SqlSugarSoftDeleteRepository<,>));
        services.TryAddScoped(typeof(IAuditedRepository<,>), typeof(SqlSugarAuditedRepository<,>));
        services.TryAddScoped(typeof(IAggregateRootRepository<,>), typeof(SqlSugarAggregateRepository<,>));

        services.TryAddScoped<IDatabaseMetadataProvider, SqlSugarDatabaseMetadataProvider>();

        // 审计上下文提供器（DefaultEntityAuditContextProvider）与差异日志写入器（NullEntityDiffLogWriter）
        // 的默认注册已下沉至 XiHanAuditingModule（XiHan.Framework.Auditing）；本模块依赖它。

        // 实体变更拦截器：基于命令级 AOP 自动捕获 INSERT / UPDATE / DELETE 差异日志
        // 无需仓储显式调用 EnableDiffLogEvent，通过 ISqlSugarClient.UseEntityChangeInterceptor() 挂载
        //services.TryAddScoped<EntityChangeInterceptor>();

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
        var options = services.GetRequiredService<IOptions<XiHanSqlSugarCoreOptions>>().Value;
        var idGenerator = services.GetRequiredService<IDistributedIdGenerator<long>>();
        var configurator = services.GetRequiredService<ISqlSugarConnectionConfigurator>();

        var connectionConfigs = options.ConnectionConfigs
            .Select(connConfig =>
            {
                var config = new ConnectionConfig
                {
                    ConfigId = connConfig.ConfigId,
                    ConnectionString = connConfig.ConnectionString,
                    DbType = connConfig.DbType,
                    IsAutoCloseConnection = connConfig.IsAutoCloseConnection,
                    InitKeyType = connConfig.InitKeyType,
                    MoreSettings = BuildMoreSettings(connConfig.MoreSettings, options),
                    // appsettings 里 HitRate 是字段绑不上（恒为 0），此处归一化为默认权重，否则从库永不被选中
                    SlaveConnectionConfigs = NormalizeSlaveHitRates(connConfig.SlaveConnectionConfigs, options),
                    DbLinkName = connConfig.DbLinkName
                };
                if (connConfig.LanguageType.HasValue)
                {
                    config.LanguageType = connConfig.LanguageType.Value;
                }
                if (!string.IsNullOrWhiteSpace(connConfig.IndexSuffix))
                {
                    config.IndexSuffix = connConfig.IndexSuffix;
                }
                return config;
            })
            .ToList();

        // 设置自定义全局雪花ID生成器
        StaticConfig.CustomSnowFlakeFunc = idGenerator.NextId;

        // 构建 SqlSugarScope 前，把已填好框架默认值的原生连接配置完整交给调用方定制（想改就改，不改吃默认）
        options.ConfigureConnectionConfigs?.Invoke(connectionConfigs);

        return new SqlSugarScope(connectionConfigs, client =>
        {
            foreach (var config in connectionConfigs)
            {
                var dbProvider = client.GetConnectionScope(config.ConfigId);
                configurator.Configure(dbProvider);
            }
        });
    }

    internal static ConnMoreSettings BuildMoreSettings(ConnMoreSettings? rawSettings, XiHanSqlSugarCoreOptions options)
    {
        rawSettings ??= new ConnMoreSettings();
        rawSettings.IsAutoUpdateQueryFilter = options.EnableAutoUpdateQueryFilter;
        rawSettings.IsAutoDeleteQueryFilter = options.EnableAutoDeleteQueryFilter;
        return rawSettings;
    }

    /// <summary>
    /// 归一化从库权重：SqlSugar 的 <see cref="SlaveConnectionConfig.HitRate"/> 是字段，无法经 appsettings 绑定（恒为 0）；
    /// 把 <c>HitRate &lt;= 0</c> 的从库回填为 <see cref="XiHanSqlSugarCoreOptions.DefaultSlaveHitRate"/>，
    /// 保证经配置文件声明的从库能真正参与读写分离。差异化权重请用 <c>ConfigureConnectionConfigs</c> 代码钩子。
    /// </summary>
    internal static List<SlaveConnectionConfig>? NormalizeSlaveHitRates(List<SlaveConnectionConfig>? slaves, XiHanSqlSugarCoreOptions options)
    {
        if (slaves is null || slaves.Count == 0)
        {
            return slaves;
        }

        var defaultRate = options.DefaultSlaveHitRate > 0 ? options.DefaultSlaveHitRate : 10;
        foreach (var slave in slaves)
        {
            if (slave.HitRate <= 0)
            {
                slave.HitRate = defaultRate;
            }
        }

        return slaves;
    }

    /// <summary>
    /// 应用全局过滤器
    /// </summary>
    /// <param name="provider"></param>
    /// <param name="options"></param>
    /// <param name="currentTenantAccessor"></param>
    internal static void ApplySugarGlobalFilters(
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
            // 租户过滤策略（表达式内只允许出现 long/bool 标量：绝不引用 BasicTenantInfo 复杂对象、不对可空值取 .Value，
            // 否则 SqlSugar 会把整个 BasicTenantInfo 当 SQL 参数交给驱动，报 “Can't write CLR type BasicTenantInfo”）：
            // - 平台态（无租户上下文 / 上下文 TenantId 为空）：ResolveTenantScopeId 返回哨兵值 → 首个恒真子句放行全部
            // - 有租户上下文：只看（本租户数据 OR 全局模板 TenantId=0）
            provider.QueryFilter.AddTableFilter<IMultiTenantEntity>(
                entity => ResolveTenantScopeId(currentTenantAccessor) == PlatformTenantScopeSentinel ||
                          entity.TenantId == 0 ||
                          entity.TenantId == ResolveTenantScopeId(currentTenantAccessor));
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
    /// 平台态（无租户上下文）哨兵值：租户过滤器据此放行全部数据。
    /// 取 <see cref="long.MinValue"/> 确保不与平台租户(0)或业务租户(≥1)冲突。
    /// </summary>
    private const long PlatformTenantScopeSentinel = long.MinValue;

    /// <summary>
    /// 解析当前租户过滤标量：有租户上下文返回其 TenantId，否则返回平台哨兵值。
    /// </summary>
    /// <remarks>
    /// 供全局租户 QueryFilter 使用：仅返回 <see cref="long"/> 标量，绝不向过滤表达式泄漏 BasicTenantInfo 复杂对象，
    /// 且对空上下文以哨兵兜底而非取 <c>.Value</c>，从而规避 SqlSugar 表达式翻译期的类型/空值异常。
    /// SqlSugar 对过滤表达式按查询即时求值，本方法随之每次查询重算，保证租户上下文动态生效。
    /// </remarks>
    /// <param name="currentTenantAccessor">当前租户访问器</param>
    /// <returns>当前租户 Id 或平台哨兵值</returns>
    private static long ResolveTenantScopeId(ICurrentTenantAccessor currentTenantAccessor)
    {
        return currentTenantAccessor.Current?.TenantId ?? PlatformTenantScopeSentinel;
    }

    /// <summary>
    /// 配置数据库 Aop 设置
    /// </summary>
    /// <param name="scopeFactory"></param>
    /// <param name="dbProvider"></param>
    /// <param name="options"></param>
    /// <param name="dataExecutingHandler"></param>
    internal static void SetSugarAop(
        IServiceScopeFactory scopeFactory,
        SqlSugarScopeProvider dbProvider,
        XiHanSqlSugarCoreOptions options,
        SqlSugarDataExecutingHandler dataExecutingHandler)
    {
        var config = dbProvider.CurrentConnectionConfig;

        // 设置超时时间
        dbProvider.Ado.CommandTimeOut = options.SlowSqlThresholdMilliseconds / 1000;

        // 组合 OnLogExecuting 处理器（SQL 日志 + 实体变更拦截器）
        // 使用本地委托组合后再赋值，避免 SqlSugar 仅写属性无法 += 的问题
        Action<string, SugarParameter[]>? onLogExecuting = null;

        if (options.EnableSqlLog)
        {
            onLogExecuting += (sql, parameters) =>
            {
                HandleSqlExecutingLog(config, sql, parameters);
            };
        }

        // 实体变更拦截器：始终附加（解析自 Scope，业务层可通过注册 Null 实现停用）
        onLogExecuting += (sql, parameters) =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var interceptor = scope.ServiceProvider.GetService<Auditing.EntityChangeInterceptor>();
                interceptor?.OnDataExecuting(sql, parameters);
            }
            catch
            {
                // 拦截器异常不影响主业务
            }
        };

        if (onLogExecuting is not null)
        {
            dbProvider.Aop.OnLogExecuting = onLogExecuting;
        }

        // OnError：SQL 异常日志（可选）+ DB Span 记录异常（OTel 未监听时零开销）
        dbProvider.Aop.OnError = ex =>
        {
            if (options.EnableSqlErrorLog)
            {
                HandleSqlOnErrorLog(config, ex);
            }
            RecordDbActivity(dbProvider, config, ex.Sql ?? string.Empty, ex);
        };

        // 组合 OnLogExecuted 处理器（慢 SQL 日志 + 实体变更拦截器）
        Action<string, SugarParameter[]>? onLogExecuted = null;

        if (options.EnableSlowSqlLog)
        {
            onLogExecuted += (sql, parameters) =>
            {
                HandleSqlSlowLog(dbProvider, options, config, sql, parameters);
            };
        }

        // 实体变更拦截器：始终附加
        onLogExecuted += (sql, parameters) =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var interceptor = scope.ServiceProvider.GetService<Auditing.EntityChangeInterceptor>();
                interceptor?.OnDataExecuted(sql, parameters);
            }
            catch
            {
                // 拦截器异常不影响主业务
            }
        };

        // 数据库 Span：为每条执行完成的 SQL 补记 DB Client span（挂当前请求 span 下；OTel 未监听时零开销）
        onLogExecuted += (sql, _) =>
        {
            RecordDbActivity(dbProvider, config, sql, null);
        };

        if (onLogExecuted is not null)
        {
            dbProvider.Aop.OnLogExecuted = onLogExecuted;
        }

        // 核心焊死、只许追加：框架的雪花主键/审计/租户注入始终先跑且不可覆盖，
        // 调用方经 options.AppendDataExecuting 仅能在其后追加逻辑（此处为组合式赋值，非直接暴露 Aop 供覆盖）。
        dbProvider.Aop.DataExecuting = (oldValue, entityInfo) =>
        {
            dataExecutingHandler.Handle(entityInfo);
            options.AppendDataExecuting?.Invoke(oldValue, entityInfo);
        };

        // 实体差异日志 AOP：基于 SqlSugar 原生 OnDiffLogEvent 的真 AOP 审计
        // 仓储层通过 .EnableDiffLogEvent(businessData) 启用，本处理器自动生成审计记录
        if (options.EnableDiffLog)
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

    /// <summary>
    /// 记录数据库 Span（OpenTelemetry Tracing）
    /// </summary>
    /// <remarks>
    /// 在 OnLogExecuted/OnError 中按已知执行耗时「回溯」创建一个 ActivityKind.Client 子 span（挂当前请求 Activity 下），
    /// 无跨回调状态、无泄漏；OTel 未激活（无监听者）时直接返回，零开销。不覆盖既有 SQL 日志/实体变更拦截器。
    /// </remarks>
    /// <param name="dbProvider">SqlSugar 作用域提供者</param>
    /// <param name="config">连接配置</param>
    /// <param name="sql">SQL 语句</param>
    /// <param name="error">异常（成功路径为 null）</param>
    private static void RecordDbActivity(SqlSugarScopeProvider dbProvider, ConnectionConfig config, string sql, Exception? error)
    {
        if (!XiHanActivitySources.DataSource.HasListeners())
        {
            return;
        }

        var elapsed = dbProvider.Ado.SqlExecutionTime;
        var end = DateTimeOffset.UtcNow;
        var start = end - (elapsed > TimeSpan.Zero ? elapsed : TimeSpan.Zero);

        using var activity = XiHanActivitySources.DataSource.StartActivity(
            "db.query",
            ActivityKind.Client,
            Activity.Current?.Context ?? default,
            startTime: start);
        if (activity is null)
        {
            return;
        }

        activity.SetTag("db.system", config.DbType.ToString());
        activity.SetTag("db.statement", sql);
        activity.SetEndTime(end.UtcDateTime);

        if (error is not null)
        {
            activity.SetStatus(ActivityStatusCode.Error, error.Message);
            activity.AddException(error);
        }
    }
}
