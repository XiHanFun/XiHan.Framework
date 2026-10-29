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
using XiHan.Framework.Persistence.Abstractions.Identifiers;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Persistence.EntityFrameworkCore;

/// <summary>
/// EF Core 持久化的能力包入口。
/// </summary>
/// <remarks>
/// 提供 <c>persistence.relational@1</c>，以应用的 <typeparamref name="TContext"/> 实现仓储与工作单元。健康项 <see cref="HealthName"/> 按 <see cref="EntityFrameworkCorePersistenceOptions.HealthProbeInterval"/> 周期在独立作用域里 <c>CanConnect</c>。
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
            Config = new ConfigContribution("XiHan:Persistence"),
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

        context.Services.AddSingleton(_options);
        // 选项按作用域构建：带租户分片的调用把分片连接作为绑定交给回调，其余用应用默认连接
        context.Services.AddDbContext<TContext>((sp, options) =>
        {
            var shard = TenantShardBinding.Resolve(sp, ConnectionBinding, ConnectionBindingType, binding);
            _configure(shard.HasValue ? shard : binding, options);
        });
        // 工作单元只依赖基类 DbContext，把应用的 TContext 转发为作用域内的同一实例，避免产生第二个上下文
        context.Services.TryAddScoped<DbContext>(static sp => sp.GetRequiredService<TContext>());
        context.Services.TryAddScoped<EntityFrameworkCoreUnitOfWork>();
        context.Services.TryAddScoped<IUnitOfWork>(static sp => sp.GetRequiredService<EntityFrameworkCoreUnitOfWork>());
        context.Services.TryAddSingleton<IIdGenerator<Guid>, GuidV7IdGenerator>();
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
        if (_options.EnsureCreated)
        {
            // DbContext 是作用域服务，建表要在独立作用域内完成，不能从根容器直接解析
            await using var scope = services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<TContext>().Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
        }

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
        services.TryAddScoped<IRepository<TEntity, TKey>, EntityFrameworkCoreRepository<TEntity, TKey>>();
    }

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
