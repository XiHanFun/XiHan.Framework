// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
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

namespace XiHan.Framework.Persistence.EntityFrameworkCore;

/// <summary>
/// EF Core 持久化的能力包入口。
/// </summary>
/// <remarks>
/// 提供 <c>persistence.relational@1</c>，以应用的 <typeparamref name="TContext"/> 实现仓储与工作单元。启动时在独立作用域里建一次模型，索引声明不合法、Provider 不支持软删除实体的部分唯一索引这类建模错误在启动时失败；SQLite 上逐连接核对 UTC ticks 时间列，库里还是旧的文本形式即启动失败（<see cref="PersistenceErrors.StorageFormatMismatch"/>）。命名连接（<see cref="EntityFrameworkCorePersistenceOptions.Connections"/>）：绑定它的工作单元所在作用域的 DbContext 按它建出，配置回调收到的绑定主值是它的连接串、属性沿用 <see cref="ConnectionBinding"/> 绑定。健康项 <see cref="HealthName"/> 按 <see cref="EntityFrameworkCorePersistenceOptions.HealthProbeInterval"/> 周期在独立作用域里 <c>CanConnect</c>。
/// </remarks>
/// <typeparam name="TContext">应用的 DbContext。</typeparam>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class EntityFrameworkCorePersistenceFeature<TContext> : IHostedFeature, IRepositoryRegistrar
    where TContext : DbContext
{
    /// <summary>
    /// 连接绑定名：<c>db</c>，类型 <c>connection:relational</c>，可选。
    /// </summary>
    public const string ConnectionBinding = "db";

    /// <summary>
    /// 健康项名：默认连接的数据库是否可达。
    /// </summary>
    public const string HealthName = "persistence.entityframeworkcore.database";

    /// <summary>
    /// 连接绑定的类型。
    /// </summary>
    private const string ConnectionBindingType = "connection:relational";

    /// <summary>
    /// 默认连接在核对消息里的名字。
    /// </summary>
    /// <remarks>
    /// 与命名连接保留的名字 <c>default</c> 一致（命名连接不能叫这个名字）。
    /// </remarks>
    private const string DefaultConnection = "default";

    /// <summary>
    /// 应用提供的 DbContext 配置回调。
    /// </summary>
    /// <remarks>
    /// 在每次构建 DbContext 选项时调用。
    /// </remarks>
    private readonly Action<Maybe<BindingValue>, DbContextOptionsBuilder> _configure;

    /// <summary>
    /// 配置阶段读取的选项。
    /// </summary>
    /// <remarks>
    /// <see cref="StartAsync"/> 据此决定是否建表。
    /// </remarks>
    private EntityFrameworkCorePersistenceOptions _options = new();

    /// <summary>
    /// 启动时起循环的健康探针，停止时结束它。
    /// </summary>
    private PeriodicHealthContributor? _health;

    /// <summary>
    /// 创建特性。
    /// </summary>
    /// <param name="configure">配置 DbContext 选项；第一个参数是宿主解析出的 <see cref="ConnectionBinding"/>（可能缺失）。</param>
    public EntityFrameworkCorePersistenceFeature(Action<Maybe<BindingValue>, DbContextOptionsBuilder> configure)
    {
        _configure = configure ?? throw new ArgumentNullException(nameof(configure));
    }

    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.persistence.entityframeworkcore",
        Package = "XiHan.Framework.Persistence.EntityFrameworkCore",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Provider,
        Role = ManifestRole.Provider,
        Provides = [new CapabilityDeclaration(PersistenceCapabilities.Relational, PersistenceCapabilities.Version)],
        Requires =
        [
            new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version),
            new CapabilityRequirement(MultiTenancyCapabilities.Sharding, MultiTenancyCapabilities.Version, optional: true),
        ],
        Bindings = [new Binding(ConnectionBinding, ConnectionBindingType, required: false)],
        Contributes = new Contributions
        {
            Pipeline = [new PipelineContribution(typeof(UnitOfWorkMiddleware).FullName!, PipelineStage.Transaction)],
            Config = new ConfigContribution(EntityFrameworkCorePersistenceOptions.ConfigurationSection),
            Health = [HealthName],
        },
        Entry = typeof(EntityFrameworkCorePersistenceFeature<>).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        // 本子系统错误码的多语言目录；同名目录只注册一次，多个 Provider 同时装配也不重复
        context.Services.AddMessageCatalog(PersistenceMessages.Catalog);
        _options = EntityFrameworkCorePersistenceOptions.FromConfiguration(context.Configuration);
        // 绑定可选：缺失时把 Maybe 原样交给回调，由应用决定回退到何种连接
        var binding = context.TryGetBinding(ConnectionBinding);

        var persistence = _options;
        context.Services.AddSingleton(persistence);
        // 选项按作用域构建：绑定了命名连接的作用域把那个连接作为绑定交给回调（主值换成它的连接串、属性沿用 db 绑定，与 SqlSugar 一致），
        // 带租户分片的调用把分片连接作为绑定交给回调，其余用应用默认连接
        context.Services.AddScoped<ContextConnection>();
        context.Services.AddDbContext<TContext>((sp, options) =>
        {
            if (sp.GetRequiredService<ContextConnection>().Build() is { } named)
            {
                _configure(NamedBinding(binding, persistence.Connections[named]), options);
                return;
            }

            var shard = TenantShardBinding.Resolve(sp, ConnectionBinding, ConnectionBindingType, binding);
            _configure(shard.HasValue ? shard : binding, options);
        });
        // 工作单元只依赖基类 DbContext，把应用的 TContext 转发为作用域内的同一实例，避免产生第二个上下文
        context.Services.TryAddScoped<DbContext>(static sp => sp.GetRequiredService<TContext>());
        // DbContext 由工作单元在开始时才取：那时才知道这个作用域要不要绑定命名连接
        context.Services.TryAddScoped(static sp => new EntityFrameworkCoreUnitOfWork(
            sp.GetRequiredService<DbContext>,
            sp.GetRequiredService<ContextConnection>(),
            sp.GetRequiredService<EntityFrameworkCorePersistenceOptions>(),
            sp.GetRequiredService<RepositoryRegistrations>()));
        context.Services.TryAddScoped<IUnitOfWork>(static sp => sp.GetRequiredService<EntityFrameworkCoreUnitOfWork>());
        context.Services.TryAddSingleton<IIdGenerator<Guid>, GuidV7IdGenerator>();
        // 运行时数据源不支持：数据库 Provider（UseNpgsql 等）由应用选、本包不引用，按数据库类型建不出连接；注册表照样登记，取用返回 PERSISTENCE_UNAVAILABLE_008
        context.Services.AddDataSourceRegistry();
        // 实体目录取 DbContext 模型；库结构读取不支持：跨库读结构在 EF Core 里是设计期脚手架服务，返回 PERSISTENCE_UNAVAILABLE_008
        context.Services.TryAddSingleton<IEntityCatalog, EntityFrameworkCoreEntityCatalog>();
        context.Services.AddUnsupportedSchemaReader();
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
        var registrations = services.GetRequiredService<RepositoryRegistrations>();
        // DbContext 是作用域服务，建模与建表要在独立作用域内完成，不能从根容器直接解析
        await using (var scope = services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TContext>();
            // 取一次模型（EF Core 按上下文类型缓存，之后的作用域直接复用）：OnModelCreating 里的建模错误——[EntityIndex] 声明不合法、
            // Provider 不支持软删除实体的部分唯一索引——在启动时失败，不等第一次请求才报；生产库不开 EnsureCreated 时同样核对
            _ = context.Model;
            if (_options.EnsureCreated)
            {
                await context.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
            }

            // 默认连接上是没有登记到命名连接的实体（含没经仓储登记、只在 DbContext 里配置的）
            await VerifyTimestampsAsync(context, DefaultConnection, type => registrations.ConnectionOf(type) is null, cancellationToken).ConfigureAwait(false);
        }

        await StartNamedAsync(services, registrations, cancellationToken).ConfigureAwait(false);

        _health = services.GetRequiredKeyedService<PeriodicHealthContributor>(HealthName);
        await _health.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_health is not null)
        {
            await _health.StopAsync(cancellationToken).ConfigureAwait(false);
            _health = null;
        }
    }

    /// <inheritdoc />
    public void Register<TEntity, TKey>(IServiceCollection services)
        where TEntity : class, IEntity<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(services);
        // 登记集合先记下实体再交给 Provider 注册，这里查得到它的落点；登记的命名连接没有声明就在装配时拒绝，不把它静默放到缺省连接上
        if (services.GetRepositoryRegistrations().ConnectionOf(typeof(TEntity)) is { } connection && !_options.Connections.ContainsKey(connection))
        {
            throw new FrameworkException(PersistenceErrors.UnknownConnection, $"Entity '{typeof(TEntity).FullName}' is registered on the named connection '{connection}', but '{EntityFrameworkCorePersistenceOptions.ConfigurationSection}:Connections' does not declare it.");
        }

        services.TryAddScoped<IRepository<TEntity, TKey>, EntityFrameworkCoreRepository<TEntity, TKey>>();
    }

    /// <summary>
    /// 在登记了实体的每个命名连接上建表并核对时间列。
    /// </summary>
    /// <remarks>
    /// 每个连接一个独立作用域，DbContext 按那个连接建出：开着 <c>EnsureCreated</c> 时先建表（EF Core 按整个模型建表，那个库里多出的空表不用；只用于开发与测试，与默认连接一样），再核对登记到这个连接的实体的时间列（<see cref="VerifyTimestampsAsync"/>）。
    /// </remarks>
    private async Task StartNamedAsync(IServiceProvider services, RepositoryRegistrations registrations, CancellationToken cancellationToken)
    {
        var placed = registrations.Entities
            .Select(static entity => entity.Connection)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal);
        foreach (var connection in placed)
        {
            await using var scope = services.CreateAsyncScope();
            // 先绑定再取 DbContext：选项回调按这个连接建
            scope.ServiceProvider.GetRequiredService<ContextConnection>().Bind(connection);
            var context = scope.ServiceProvider.GetRequiredService<TContext>();
            if (_options.EnsureCreated)
            {
                await context.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
            }

            await VerifyTimestampsAsync(context, connection, type => string.Equals(registrations.ConnectionOf(type), connection, StringComparison.Ordinal), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 核对一个连接上的 UTC ticks 时间列，库里还是旧的存储形式就启动失败。
    /// </summary>
    /// <remarks>
    /// SQLite 上由约定映射成 UTC ticks 整数的列，在库里声明的不是 INTEGER（旧库的文本列，空表也算）或抽样到非整数的值（重建表时没换算），照读会静默读成错误的时刻：这个连接上的不符列一次列出，抛出携带 <see cref="PersistenceErrors.StorageFormatMismatch"/> 的 <see cref="FrameworkException"/>（RFC-0005「EF Core 在 SQLite 上的时间列核对」，ADR-0094）。其余 Provider、库文件还不存在时什么也不做。
    /// </remarks>
    private static async Task VerifyTimestampsAsync(TContext context, string connection, Func<Type, bool> placedHere, CancellationToken cancellationToken)
    {
        var mismatches = await SqliteTimestampCheck.FindMismatchesAsync(context, placedHere, cancellationToken).ConfigureAwait(false);
        if (mismatches.Count > 0)
        {
            // 照读不报错、只是读成错误的时刻：不能带着旧数据进入服务，消息里给出全部不符的列与换算式的出处
            Result.Failure(Error.Validation(PersistenceErrors.StorageFormatMismatch, PersistenceMessages.TimestampFormatMismatch, MessageArguments.Of(("connection", connection), ("columns", string.Join(", ", mismatches))))).ThrowIfFailure();
        }
    }

    /// <summary>
    /// 命名连接的绑定。
    /// </summary>
    /// <remarks>
    /// 主值换成命名连接的连接串，属性沿用 <see cref="ConnectionBinding"/> 绑定（数据库类型等，没有绑定时为空）。
    /// </remarks>
    private static Maybe<BindingValue> NamedBinding(Maybe<BindingValue> binding, string connectionString)
        => Maybe.Some(binding.TryGetValue(out var value)
            ? value with { Value = connectionString }
            : new BindingValue(ConnectionBinding, ConnectionBindingType, connectionString, new Dictionary<string, string>(StringComparer.Ordinal)));

    /// <summary>
    /// 连通性探针。
    /// </summary>
    /// <remarks>
    /// 在独立作用域里取应用默认连接的 DbContext 试连；不开工作单元，不经管道。连不上返回 <see cref="PersistenceErrors.StoreUnavailable"/>。
    /// </remarks>
    private static async ValueTask<Result> ProbeAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var reachable = await scope.ServiceProvider.GetRequiredService<TContext>().Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
        return reachable ? Result.Success() : Error.Unavailable(PersistenceErrors.StoreUnavailable, PersistenceMessages.StoreUnavailable);
    }
}
