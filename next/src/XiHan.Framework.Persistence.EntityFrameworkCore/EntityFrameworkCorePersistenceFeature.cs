// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Domain;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Persistence.EntityFrameworkCore;

/// <summary>
/// EF Core 持久化的能力包入口：提供 <c>persistence.relational@1</c>，以应用的 <typeparamref name="TContext"/> 实现仓储与工作单元。
/// </summary>
/// <typeparam name="TContext">应用的 DbContext。</typeparam>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class EntityFrameworkCorePersistenceFeature<TContext> : IHostedFeature, IRepositoryRegistrar
    where TContext : DbContext
{
    /// <summary>
    /// 连接绑定名：<c>db</c>，类型 <c>connection:relational</c>，可选。
    /// </summary>
    public const string ConnectionBinding = "db";

    private readonly Action<Maybe<BindingValue>, DbContextOptionsBuilder> _configure;
    private EntityFrameworkCorePersistenceOptions _options = new();

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
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Bindings = [new Binding(ConnectionBinding, "connection:relational", required: false)],
        Contributes = new Contributions
        {
            Pipeline = [new PipelineContribution(typeof(UnitOfWorkMiddleware).FullName!, PipelineStage.Transaction)],
            Config = new ConfigContribution("XiHan:Persistence"),
            Health = ["persistence.entityframeworkcore.database"],
        },
        Entry = typeof(EntityFrameworkCorePersistenceFeature<>).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _options = EntityFrameworkCorePersistenceOptions.FromConfiguration(context.Configuration);
        var binding = context.TryGetBinding(ConnectionBinding);

        context.Services.AddSingleton(_options);
        context.Services.AddDbContext<TContext>(options => _configure(binding, options));
        context.Services.TryAddScoped<DbContext>(static sp => sp.GetRequiredService<TContext>());
        context.Services.TryAddScoped<EntityFrameworkCoreUnitOfWork>();
        context.Services.TryAddScoped<IUnitOfWork>(static sp => sp.GetRequiredService<EntityFrameworkCoreUnitOfWork>());
        context.Services.TryAddSingleton<IIdGenerator<Guid>, GuidV7IdGenerator>();
        context.AddMiddleware<UnitOfWorkMiddleware>(PipelineStage.Transaction);
        context.Services.GetRepositoryRegistrations().ApplyTo(context.Services, this);
    }

    /// <inheritdoc />
    public async Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (!_options.EnsureCreated)
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<TContext>().Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public void Register<TEntity, TKey>(IServiceCollection services)
        where TEntity : class, IEntity<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<IRepository<TEntity, TKey>, EntityFrameworkCoreRepository<TEntity, TKey>>();
    }
}
