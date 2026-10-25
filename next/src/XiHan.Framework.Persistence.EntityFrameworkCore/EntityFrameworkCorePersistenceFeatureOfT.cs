// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.Identifiers;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
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

    /// <summary>
    /// 应用提供的 DbContext 配置回调；在每次构建 DbContext 选项时调用。
    /// </summary>
    private readonly Action<Maybe<BindingValue>, DbContextOptionsBuilder> _configure;

    /// <summary>
    /// 配置阶段读取的选项；<see cref="StartAsync"/> 据此决定是否建表。
    /// </summary>
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
        // 本子系统错误码的多语言目录；同名目录只注册一次，多个 Provider 同时装配也不重复
        context.Services.AddMessageCatalog(PersistenceMessages.Catalog);
        _options = EntityFrameworkCorePersistenceOptions.FromConfiguration(context.Configuration);
        // 绑定可选：缺失时把 Maybe 原样交给回调，由应用决定回退到何种连接
        var binding = context.TryGetBinding(ConnectionBinding);

        context.Services.AddSingleton(_options);
        context.Services.AddDbContext<TContext>(options => _configure(binding, options));
        // 工作单元只依赖基类 DbContext，把应用的 TContext 转发为作用域内的同一实例，避免产生第二个上下文
        context.Services.TryAddScoped<DbContext>(static sp => sp.GetRequiredService<TContext>());
        context.Services.TryAddScoped<EntityFrameworkCoreUnitOfWork>();
        context.Services.TryAddScoped<IUnitOfWork>(static sp => sp.GetRequiredService<EntityFrameworkCoreUnitOfWork>());
        context.Services.TryAddSingleton<IIdGenerator<Guid>, GuidV7IdGenerator>();
        context.AddMiddleware<UnitOfWorkMiddleware>(PipelineStage.Transaction);
        // 回放此前登记的实体，并让此后登记的实体立即注册到本 Provider
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

        // DbContext 是作用域服务，建表要在独立作用域内完成，不能从根容器直接解析
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
