// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SqlSugar;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.MultiTenancy.Abstractions.Sharding;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.DataSources;
using XiHan.Framework.Persistence.Abstractions.Identifiers;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.Schema;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// SqlSugar 持久化的能力包入口
/// </summary>
/// <remarks>
/// 提供 <c>persistence.relational@1</c>，以作用域内的 <see cref="ISqlSugarClient"/> 实现仓储与工作单元。客户端有两种来源：由本特性按配置回调创建（自动应用 <see cref="SqlSugarConventions"/>；默认连接 <c>ConfigId</c> 为 <see cref="SqlSugarConnections.Default"/>，<see cref="SqlSugarPersistenceOptions.Connections"/> 的命名连接随后，带租户分片的调用再加一个 <see cref="SqlSugarConnections.Shard"/> 连接），或由应用自己注册（应用须先对连接配置调用 <see cref="SqlSugarConventions.Apply"/>，多连接时自己决定 <c>ConfigId</c>）。实体落在哪个连接由 <see cref="IEntityConnectionSelector"/> 决定。健康项 <see cref="HealthName"/> 按 <see cref="SqlSugarPersistenceOptions.HealthProbeInterval"/> 周期在独立作用域里打开再关闭默认连接
/// 回调在 <c>ConnectionConfig.SlaveConnectionConfigs</c> 上给出从库时读写分离（RFC-0005「读写分离」，ADR-0118）：只读工作单元读从库，会写的工作单元读写都走主库；固定连接的从库周期探测，不可读的摘除，全部不可读时读回到主库，见健康项 <see cref="ReplicaHealthName"/>。回调对每个连接都调用，从库只给它所属的连接（看 <c>ConfigId</c>），主库不同的连接共用从库是 <see cref="PersistenceErrors.ReplicaMisplaced"/>
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class SqlSugarPersistenceFeature : IHostedFeature, IRepositoryRegistrar
{
    /// <summary>
    /// 连接绑定名：<c>db</c>，类型 <c>connection:relational</c>，可选
    /// </summary>
    public const string ConnectionBinding = "db";

    /// <summary>
    /// 健康项名：默认连接的数据库是否可达
    /// </summary>
    public const string HealthName = "persistence.sqlsugar.database";

    /// <summary>
    /// 健康项名：从库能否提供读取
    /// </summary>
    /// <remarks>
    /// 只在固定连接配了从库且没有关掉探测时存在
    /// </remarks>
    public const string ReplicaHealthName = "persistence.sqlsugar.replicas";

    /// <summary>
    /// 连接绑定的类型
    /// </summary>
    private const string ConnectionBindingType = "connection:relational";

    /// <summary>
    /// 应用提供的连接配置回调
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 表示客户端由应用自己注册
    /// </remarks>
    private readonly Action<Maybe<BindingValue>, ConnectionConfig>? _configure;

    /// <summary>
    /// 已登记的实体类型
    /// </summary>
    /// <remarks>
    /// <see cref="StartAsync"/> 按它们建表
    /// </remarks>
    private readonly List<Type> _entityTypes = [];

    /// <summary>
    /// 配置阶段读取的选项
    /// </summary>
    /// <remarks>
    /// <see cref="StartAsync"/> 据此决定是否建表
    /// </remarks>
    private SqlSugarPersistenceOptions _options = new();

    /// <summary>
    /// 启动时起循环的健康探针，停止时结束它
    /// </summary>
    private PeriodicHealthContributor? _health;

    /// <summary>
    /// 从库的健康探针
    /// </summary>
    /// <remarks>
    /// 没配从库或关掉探测时为空
    /// </remarks>
    private PeriodicHealthContributor? _replicaHealth;

    /// <summary>
    /// 以应用自己注册的 <see cref="ISqlSugarClient"/>（作用域或 <c>SqlSugarScope</c>）装配
    /// </summary>
    /// <remarks>
    /// 应用须在创建客户端前对 <c>ConnectionConfig</c> 调用 <see cref="SqlSugarConventions.Apply"/>
    /// </remarks>
    public SqlSugarPersistenceFeature()
    {
    }

    /// <summary>
    /// 由本特性创建作用域内的 <see cref="SqlSugarClient"/>
    /// </summary>
    /// <remarks>
    /// 回调设置数据库类型、连接串等，之后自动应用协议约定
    /// </remarks>
    /// <param name="configure">配置连接；第一个参数是宿主解析出的 <see cref="ConnectionBinding"/>（可能缺失）</param>
    public SqlSugarPersistenceFeature(Action<Maybe<BindingValue>, ConnectionConfig> configure)
    {
        _configure = configure ?? throw new ArgumentNullException(nameof(configure));
    }

    /// <summary>
    /// 哪些实体的仓储写入经 SqlSugar 差异日志（见 <see cref="SqlSugarPersistenceOptions.DiffLog"/>）
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 时一个都不开
    /// </remarks>
    [ApiLevel(Stability.Experimental, "5.0")]
    public Func<Type, bool>? DiffLog { get; init; }

    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.persistence.sqlsugar",
        Package = "XiHan.Framework.Persistence.SqlSugar",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Provider,
        Role = ManifestRole.Provider,
        Provides = [new CapabilityDeclaration(PersistenceCapabilities.Relational, PersistenceCapabilities.Version)],
        Requires =
        [
            new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version),
            new CapabilityRequirement(MultiTenancyCapabilities.Sharding, MultiTenancyCapabilities.Version) { Optional = true },
        ],
        Bindings = [new Binding(ConnectionBinding, ConnectionBindingType) { Required = false }],
        Contributes = new Contributions
        {
            Pipeline = [new PipelineContribution(typeof(UnitOfWorkMiddleware).FullName!, PipelineStage.Transaction)],
            // 读本包的 XiHan:Persistence:SqlSugar 与运行时数据源的 XiHan:Persistence:DataSources，写两者的最近公共节（ADR-0075）
            Config = new ConfigContribution("XiHan:Persistence"),
            Health = [HealthName, ReplicaHealthName],
        },
        Entry = typeof(SqlSugarPersistenceFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        // 本子系统错误码的多语言目录；同名目录只注册一次，多个 Provider 同时装配也不重复
        context.Services.AddMessageCatalog(PersistenceMessages.Catalog);
        _options = SqlSugarPersistenceOptions.FromConfiguration(context.Configuration);
        if (DiffLog is { } diffLog)
        {
            _options = _options with { DiffLog = diffLog };
        }

        context.Services.AddSingleton(_options);

        if (_configure is { } configure)
        {
            // 绑定可选：缺失时把 Maybe 原样交给回调，由应用决定回退到何种连接
            // 应用默认连接的配置只建一份供所有作用域共用；带租户分片的调用在作用域内按分片绑定另建一个 shard 连接的配置，约定同样施加
            // 默认连接始终在客户端里：平台目录表落默认连接、租户表落分片连接，由 IEntityConnectionSelector 决定，两者在同一客户端里由 AsTenant() 统一开事务
            var binding = context.FindBinding(ConnectionBinding);
            var defaultHitRate = _options.Replicas.DefaultHitRate;
            var commandTimeout = _options.CommandTimeout;
            var shared = CreateConfig(configure, binding, SqlSugarConnections.Default, defaultHitRate);
            // 命名连接（RFC-0005「命名连接」）同样只建一份配置：回调按 ConfigId 区分，绑定的主值换成该连接串、属性沿用 db 绑定（库类型等）
            // 它们排在默认连接之后，根连接仍是默认连接；工作单元只在触及的连接上开事务，业务调用不碰命名连接时那个库不被业务事务锁住
            var named = _options.Connections.Select(pair => CreateConfig(configure, NamedBinding(binding, pair.Value), pair.Key, defaultHitRate)).ToList();
            // 配了从库才有下面这些：没配时作用域直接用共享的配置，没有探测、没有健康条目，也不多一次分配（RFC-0005「读写分离」）
            var replicated = new[] { shared }.Concat(named).Where(SqlSugarReplicaRules.HasReplicas).ToList();
            var owners = SqlSugarReplicaRules.Owners(replicated);
            if (replicated.Count > 0)
            {
                RegisterReplicas(context.Services, replicated);
            }

            context.Services.AddScoped<ISqlSugarClient>(sp =>
            {
                var shard = TenantShardBinding.Resolve(sp, ConnectionBinding, ConnectionBindingType, binding);
                var replicas = replicated.Count > 0 ? sp.GetRequiredService<SqlSugarReplicaMonitor>() : null;
                var root = replicas?.ForScope(shared) ?? shared;
                if (!shard.HasValue && named.Count == 0)
                {
                    return WithCommandTimeout(new SqlSugarClient(root), [], commandTimeout);
                }

                List<ConnectionConfig> configs = [root, .. named.Select(config => replicas?.ForScope(config) ?? config)];
                if (shard.TryGetValue(out var onShard))
                {
                    // 记下分片的租户，工作单元开始时核对它的租户与之一致（ADR-0223）
                    var bound = sp.GetRequiredService<SqlSugarShardTenant>();
                    bound.IsBound = true;
                    bound.TenantId = onShard.Properties.TryGetValue(TenantShardKeys.TenantProperty, out var tenant) ? tenant : null;
                    // 分片连接的配置本来就是这个作用域现建的，从库由回调按分片绑定给出；核对它没有拿别的库的从库，分片不探测
                    var config = CreateConfig(configure, shard, SqlSugarConnections.Shard, defaultHitRate);
                    SqlSugarReplicaRules.EnsureOwn(config, owners);
                    configs.Add(config);
                }

                return WithCommandTimeout(new SqlSugarClient(configs), configs, commandTimeout);
            });
        }
        else if (_options.Connections.Count > 0)
        {
            // 方式二由应用自己组装连接集合：这一节对它没有作用，静默忽略会让应用以为命名连接已经建好
            throw new ArgumentException($"'{SqlSugarPersistenceOptions.ConfigurationSection}:Connections' only applies when the feature creates the client from a configure callback; the application registers its own ISqlSugarClient, so add connections with these ConfigIds to it instead.");
        }
        else if (_options.CommandTimeout != SqlSugarPersistenceOptions.StandardCommandTimeout)
        {
            // 同上：客户端是应用建的，这个键碰不到它，静默忽略会让应用以为超时已经生效
            throw new ArgumentException($"'{SqlSugarPersistenceOptions.ConfigurationSection}:CommandTimeoutSeconds' only applies when the feature creates the client from a configure callback; the application registers its own ISqlSugarClient, so set 'Ado.CommandTimeOut' on it instead.");
        }

        context.Services.TryAddSingleton<IEntityConnectionSelector, SqlSugarConnectionSelector>();
        context.Services.TryAddScoped<SqlSugarShardTenant>();
        // 先取客户端：建出带分片的客户端时才写入分片的租户，工作单元开始时核对
        context.Services.TryAddScoped(static sp => new SqlSugarUnitOfWork(sp.GetRequiredService<ISqlSugarClient>(), sp.GetRequiredService<IEntityConnectionSelector>()) { Shard = sp.GetRequiredService<SqlSugarShardTenant>() });
        context.Services.TryAddScoped<IUnitOfWork>(static sp => sp.GetRequiredService<SqlSugarUnitOfWork>());
        context.Services.TryAddSingleton<IIdGenerator<Guid>, GuidV7IdGenerator>();
        // 运行时数据源：按应用记录里的定义连外部库，与上面的作用域客户端互不相干（RFC-0005「运行时数据源」）
        context.Services.AddDataSourceProvider<SqlSugarDataSourceProvider>(context.Configuration);
        // 库结构读取与实体目录：缺省连接与运行时数据源都能读，分表按已登记实体的模板折叠（RFC-0005「库结构读取与实体目录」）
        context.Services.TryAddSingleton<SqlSugarEntityCatalog>();
        context.Services.TryAddSingleton<IEntityCatalog>(static sp => sp.GetRequiredService<SqlSugarEntityCatalog>());
        context.Services.TryAddSingleton<ISchemaReader, SqlSugarSchemaReader>();
        context.AddMiddleware<UnitOfWorkMiddleware>(PipelineStage.Transaction);
        // 回放此前登记的实体，并让此后登记的实体立即注册到本 Provider
        context.Services.GetRepositoryRegistrations().ApplyTo(context.Services, this);
        // 健康项按名字作键登记：几个 Provider 的探针各自独立，内核经 IHealthContributor 统一采集
        var interval = _options.HealthProbeInterval;
        context.Services.AddKeyedSingleton(HealthName, (sp, _) => new PeriodicHealthContributor(HealthName, ct => ProbeAsync(sp, ct), interval, sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<ILoggerFactory>()));
        context.Services.AddSingleton<IHealthContributor>(static sp => sp.GetRequiredKeyedService<PeriodicHealthContributor>(HealthName));
    }

    /// <inheritdoc />
    public async Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        await ValidatePlacementsAsync(services).ConfigureAwait(false);
        await ValidatePartialIndexesAsync(services).ConfigureAwait(false);
        if (_options.InitTables && _entityTypes.Count > 0)
        {
            await InitTablesAsync(services).ConfigureAwait(false);
        }

        _health = services.GetRequiredKeyedService<PeriodicHealthContributor>(HealthName);
        await _health.StartAsync(cancellationToken).ConfigureAwait(false);
        // 首探在这里完成：宿主 Ready 时读已经绕开了连不上的从库
        _replicaHealth = services.GetKeyedService<PeriodicHealthContributor>(ReplicaHealthName);
        if (_replicaHealth is not null)
        {
            await _replicaHealth.StartAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_health is not null)
        {
            await _health.StopAsync(cancellationToken).ConfigureAwait(false);
            _health = null;
        }

        if (_replicaHealth is not null)
        {
            await _replicaHealth.StopAsync(cancellationToken).ConfigureAwait(false);
            _replicaHealth = null;
        }
    }

    /// <summary>
    /// 启动时校验实体落点
    /// </summary>
    /// <remarks>
    /// 用缺省落点规则时，带 <c>[Tenant(configId)]</c> 或登记到命名连接的已登记实体指向客户端里没有的连接、两者指向不同的连接，即以 <see cref="PersistenceErrors.UnknownConnection"/> 中断启动，不等第一次使用才报。<see cref="SqlSugarConnections.Shard"/> 只在带租户分片的调用里存在，不在启动时校验；应用替换了落点规则的不校验。没有这样的实体时不取客户端
    /// </remarks>
    private async Task ValidatePlacementsAsync(IServiceProvider services)
    {
        var registrations = services.GetRequiredService<RepositoryRegistrations>();
        var placed = _entityTypes
            .Where(type => SqlSugarConnectionSelector.PinnedConnection(type) is not null || registrations.ConnectionOf(type) is not null)
            .ToList();
        if (placed.Count == 0)
        {
            return;
        }

        // 客户端是作用域服务，在独立作用域里取；与建表、健康探针一样不经管道
        await using var scope = services.CreateAsyncScope();
        if (scope.ServiceProvider.GetRequiredService<IEntityConnectionSelector>() is not SqlSugarConnectionSelector)
        {
            return;
        }

        var tenant = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>().AsTenant();
        foreach (var type in placed)
        {
            // [Tenant] 与登记的连接不一致在这里抛出；shard 只在带分片的调用里存在
            var configId = SqlSugarConnectionSelector.Placement(type, registrations.ConnectionOf(type));
            if (configId is not null && configId != SqlSugarConnections.Shard && !tenant.IsAnyConnection(configId))
            {
                throw SqlSugarConnectionSelector.UnknownConnection(type, configId);
            }
        }
    }

    /// <summary>
    /// 启动时核对部分唯一索引的数据库支持
    /// </summary>
    /// <remarks>
    /// 已登记的软删除实体声明了唯一索引（只约束未删除的行），而它落的连接的数据库类型不支持部分索引（PostgreSQL、SQLite、SQL Server 之外）时，一次列出全部实体、以 <see cref="PersistenceErrors.NotSupported"/> 中断启动——不论是否开着 <c>InitTables</c>：生产库由迁移脚本建表时，唯一约束同样建不出来，不能等到写入时才发现。<see cref="SqlSugarConnections.Shard"/> 只在带租户分片的调用里存在，不在启动时核对，分片库由应用经 <see cref="SqlSugarConventions.InitTables"/> 建表时核对。没有这样的实体时不取客户端
    /// </remarks>
    private async Task ValidatePartialIndexesAsync(IServiceProvider services)
    {
        var declaring = _entityTypes.Where(PartialUniqueIndexes.Declares).ToList();
        if (declaring.Count == 0)
        {
            return;
        }

        // 客户端是作用域服务，在独立作用域里取；与建表、健康探针一样不经管道
        await using var scope = services.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<SqlSugarUnitOfWork>();
        var offenders = declaring
            .Select(type => (EntityType: type, unitOfWork.ConnectionFor(type).CurrentConnectionConfig.DbType))
            .Where(static entry => !PartialUniqueIndexes.IsSupported(entry.DbType))
            .ToList();
        if (offenders.Count > 0)
        {
            throw PartialUniqueIndexes.NotSupported(offenders);
        }
    }

    /// <summary>
    /// 为已登记的实体建表
    /// </summary>
    /// <remarks>
    /// 客户端是作用域服务，建表要在独立作用域内完成，不能从根容器直接解析；CodeFirst 只有同步 API。按落点分组建表：启动时没有调用也就没有分片，租户实体落默认连接；带 <c>[Tenant]</c> 或登记到命名连接的实体在各自的连接上建表。每个连接经 <see cref="SqlSugarConventions.InitTables"/> 建表，软删除实体的部分唯一索引随之建出
    /// </remarks>
    private async Task InitTablesAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<SqlSugarUnitOfWork>();
        foreach (var group in _entityTypes.GroupBy(unitOfWork.ConnectionFor))
        {
            SqlSugarConventions.InitTables(group.Key, [.. group]);
        }
    }

    /// <summary>
    /// 连通性探针
    /// </summary>
    /// <remarks>
    /// 对默认连接走一次数据库往返（ADR-0123）；不开工作单元，不经管道。失败时异常原样抛出，由 <see cref="PeriodicHealthContributor"/> 记成 Degraded：原因只写类型，完整异常进日志
    /// </remarks>
    private static async ValueTask<Result> ProbeAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var scope = services.CreateAsyncScope();
        var client = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
        await SqlSugarRoundTrip.RunAsync(client.CurrentConnectionConfig, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <summary>
    /// 命名连接交给回调的绑定
    /// </summary>
    /// <remarks>
    /// 主值是命名连接的连接串，名字、类型与属性（库类型等）沿用 <see cref="ConnectionBinding"/>；没有这个绑定时只有主值
    /// </remarks>
    private static Maybe<BindingValue> NamedBinding(Maybe<BindingValue> binding, string connectionString)
        => Maybe.Some(binding.TryGetValue(out var value)
            ? value with { Value = connectionString }
            : new BindingValue(ConnectionBinding, ConnectionBindingType, connectionString, new Dictionary<string, string>(StringComparer.Ordinal)));

    /// <summary>
    /// 按绑定创建连接配置
    /// </summary>
    /// <remarks>
    /// 应用回调设置数据库类型与连接串，然后施加协议约定；<c>ConfigId</c> 由本特性决定，它是落点规则的依据，回调改了也以特性为准
    /// </remarks>
    private static ConnectionConfig CreateConfig(Action<Maybe<BindingValue>, ConnectionConfig> configure, Maybe<BindingValue> binding, string configId, int defaultHitRate)
    {
        var config = new ConnectionConfig { ConfigId = configId, IsAutoCloseConnection = true };
        configure(binding, config);
        config.ConfigId = configId;
        SqlSugarReplicaRules.Normalize(config, defaultHitRate);
        return SqlSugarConventions.Apply(config);
    }

    /// <summary>
    /// 把命令超时设到客户端的每个连接上
    /// </summary>
    /// <remarks>
    /// <c>Ado</c> 每个连接一个，连接配置上没有这一项，应用的回调也碰不到建好的客户端，所以在这里设（RFC-0005「命令超时」）。多连接时要把各连接的上下文都建出来，缺省值与 SqlSugar 相同，不必设，也就不多这一步
    /// </remarks>
    private static SqlSugarClient WithCommandTimeout(SqlSugarClient client, List<ConnectionConfig> connections, TimeSpan timeout)
    {
        if (timeout == SqlSugarPersistenceOptions.StandardCommandTimeout)
        {
            return client;
        }

        // ADO.NET 的超时是整秒：向上取整，不让 1.5 秒变成 1 秒
        var seconds = (int)Math.Ceiling(timeout.TotalSeconds);

        client.Ado.CommandTimeOut = seconds;
        foreach (var connection in connections)
        {
            client.AsTenant().GetConnection(connection.ConfigId).Ado.CommandTimeOut = seconds;
        }

        return client;
    }

    /// <summary>
    /// 登记从库的路由与健康探测
    /// </summary>
    /// <remarks>
    /// 关掉探测时只有路由：权重不再变化，也没有健康条目
    /// </remarks>
    private void RegisterReplicas(IServiceCollection services, List<ConnectionConfig> replicated)
    {
        var options = _options.Replicas;
        services.AddSingleton(sp => new SqlSugarReplicaMonitor(replicated, options, sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<ILoggerFactory>()));
        if (options.HealthCheck)
        {
            services.AddKeyedSingleton(ReplicaHealthName, (sp, _) => new PeriodicHealthContributor(ReplicaHealthName, ct => sp.GetRequiredService<SqlSugarReplicaMonitor>().ProbeAsync(ct), options.HealthCheckInterval, sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<ILoggerFactory>()));
            services.AddSingleton<IHealthContributor>(static sp => new SqlSugarReplicaHealth(sp.GetRequiredKeyedService<PeriodicHealthContributor>(ReplicaHealthName), sp.GetRequiredService<SqlSugarReplicaMonitor>()));
        }
    }

    /// <inheritdoc />
    public void Register<TEntity, TKey>(IServiceCollection services)
        where TEntity : class, IEntity<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<IRepository<TEntity, TKey>, SqlSugarRepository<TEntity, TKey>>();
        _entityTypes.Add(typeof(TEntity));
    }
}
