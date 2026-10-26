// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SqlSugar;
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

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// SqlSugar 持久化的能力包入口：提供 <c>persistence.relational@1</c>，以作用域内的 <see cref="ISqlSugarClient"/> 实现仓储与工作单元。
/// 客户端有两种来源：由本特性按配置回调创建（自动应用 <see cref="SqlSugarConventions"/>；默认连接 <c>ConfigId</c> 为 <see cref="SqlSugarConnections.Default"/>，带租户分片的调用再加一个 <see cref="SqlSugarConnections.Shard"/> 连接），
/// 或由应用自己注册（应用须先对连接配置调用 <see cref="SqlSugarConventions.Apply"/>，多连接时自己决定 <c>ConfigId</c>）。实体落在哪个连接由 <see cref="IEntityConnectionSelector"/> 决定。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class SqlSugarPersistenceFeature : IHostedFeature, IRepositoryRegistrar
{
    /// <summary>
    /// 连接绑定名：<c>db</c>，类型 <c>connection:relational</c>，可选。
    /// </summary>
    public const string ConnectionBinding = "db";

    /// <summary>
    /// 连接绑定的类型。
    /// </summary>
    private const string ConnectionBindingType = "connection:relational";

    /// <summary>
    /// 应用提供的连接配置回调；为 <see langword="null"/> 表示客户端由应用自己注册。
    /// </summary>
    private readonly Action<Maybe<BindingValue>, ConnectionConfig>? _configure;

    /// <summary>
    /// 已登记的实体类型；<see cref="StartAsync"/> 按它们建表。
    /// </summary>
    private readonly List<Type> _entityTypes = [];

    /// <summary>
    /// 配置阶段读取的选项；<see cref="StartAsync"/> 据此决定是否建表。
    /// </summary>
    private SqlSugarPersistenceOptions _options = new();

    /// <summary>
    /// 以应用自己注册的 <see cref="ISqlSugarClient"/>（作用域或 <c>SqlSugarScope</c>）装配。应用须在创建客户端前对 <c>ConnectionConfig</c> 调用 <see cref="SqlSugarConventions.Apply"/>。
    /// </summary>
    public SqlSugarPersistenceFeature()
    {
    }

    /// <summary>
    /// 由本特性创建作用域内的 <see cref="SqlSugarClient"/>：回调设置数据库类型、连接串等，之后自动应用协议约定。
    /// </summary>
    /// <param name="configure">配置连接；第一个参数是宿主解析出的 <see cref="ConnectionBinding"/>（可能缺失）。</param>
    public SqlSugarPersistenceFeature(Action<Maybe<BindingValue>, ConnectionConfig> configure)
    {
        _configure = configure ?? throw new ArgumentNullException(nameof(configure));
    }

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
            new CapabilityRequirement(MultiTenancyCapabilities.Sharding, MultiTenancyCapabilities.Version, optional: true),
        ],
        Bindings = [new Binding(ConnectionBinding, ConnectionBindingType, required: false)],
        Contributes = new Contributions
        {
            Pipeline = [new PipelineContribution(typeof(UnitOfWorkMiddleware).FullName!, PipelineStage.Transaction)],
            Config = new ConfigContribution("XiHan:Persistence"),
            Health = ["persistence.sqlsugar.database"],
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
        context.Services.AddSingleton(_options);

        if (_configure is { } configure)
        {
            // 绑定可选：缺失时把 Maybe 原样交给回调，由应用决定回退到何种连接。
            // 应用默认连接的配置只建一份供所有作用域共用；带租户分片的调用在作用域内按分片绑定另建一个 shard 连接的配置，约定同样施加。
            // 默认连接始终在客户端里：平台目录表落默认连接、租户表落分片连接，由 IEntityConnectionSelector 决定，两者在同一客户端里由 AsTenant() 统一开事务
            var binding = context.TryGetBinding(ConnectionBinding);
            var shared = CreateConfig(configure, binding, SqlSugarConnections.Default);
            context.Services.AddScoped<ISqlSugarClient>(sp =>
            {
                var shard = TenantShardBinding.Resolve(sp, ConnectionBinding, ConnectionBindingType, binding);
                return shard.HasValue
                    ? new SqlSugarClient([shared, CreateConfig(configure, shard, SqlSugarConnections.Shard)])
                    : new SqlSugarClient(shared);
            });
        }

        context.Services.TryAddSingleton<IEntityConnectionSelector, SqlSugarConnectionSelector>();
        context.Services.TryAddScoped<SqlSugarUnitOfWork>();
        context.Services.TryAddScoped<IUnitOfWork>(static sp => sp.GetRequiredService<SqlSugarUnitOfWork>());
        context.Services.TryAddSingleton<IIdGenerator<Guid>, GuidV7IdGenerator>();
        context.AddMiddleware<UnitOfWorkMiddleware>(PipelineStage.Transaction);
        // 回放此前登记的实体，并让此后登记的实体立即注册到本 Provider
        context.Services.GetRepositoryRegistrations().ApplyTo(context.Services, this);
    }

    /// <inheritdoc />
    public async Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (!_options.InitTables || _entityTypes.Count == 0)
        {
            return;
        }

        // 客户端是作用域服务，建表要在独立作用域内完成，不能从根容器直接解析；CodeFirst 只有同步 API。
        // 按落点分组建表：启动时没有调用也就没有分片，方式一的全部实体都落默认连接；方式二带 [Tenant] 的实体在各自的连接上建表
        await using var scope = services.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<SqlSugarUnitOfWork>();
        foreach (var group in _entityTypes.GroupBy(unitOfWork.ConnectionFor))
        {
            // 分表实体走分表的建表入口（按当前周期建第一张表，之后的分表由写入时按需建出），普通实体走常规建表
            var split = group.Where(static type => type.IsDefined(typeof(SplitTableAttribute), inherit: false)).ToArray();
            var plain = group.Where(static type => !type.IsDefined(typeof(SplitTableAttribute), inherit: false)).ToArray();
            if (plain.Length > 0)
            {
                group.Key.CodeFirst.InitTables(plain);
            }

            if (split.Length > 0)
            {
                group.Key.CodeFirst.SplitTables().InitTables(split);
            }
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// 按绑定创建连接配置：应用回调设置数据库类型与连接串，然后施加协议约定；<c>ConfigId</c> 由本特性决定，它是落点规则的依据，回调改了也以特性为准。
    /// </summary>
    private static ConnectionConfig CreateConfig(Action<Maybe<BindingValue>, ConnectionConfig> configure, Maybe<BindingValue> binding, string configId)
    {
        var config = new ConnectionConfig { ConfigId = configId, IsAutoCloseConnection = true };
        configure(binding, config);
        config.ConfigId = configId;
        return SqlSugarConventions.Apply(config);
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
