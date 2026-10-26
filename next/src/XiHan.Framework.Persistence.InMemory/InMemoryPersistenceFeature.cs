// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

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

namespace XiHan.Framework.Persistence.InMemory;

/// <summary>
/// 内存持久化的能力包入口：提供 <c>persistence.relational@1</c>，角色为默认实现。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemoryPersistenceFeature : IFeature, IRepositoryRegistrar
{
    /// <summary>
    /// 健康项名：各实体的内存表是否都还有余量。
    /// </summary>
    public const string HealthName = "persistence.inmemory.capacity";

    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.persistence.inmemory",
        Package = "XiHan.Framework.Persistence.InMemory",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Provider,
        Role = ManifestRole.Default,
        Provides = [new CapabilityDeclaration(PersistenceCapabilities.Relational, PersistenceCapabilities.Version)],
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Contributes = new Contributions
        {
            Pipeline = [new PipelineContribution(typeof(UnitOfWorkMiddleware).FullName!, PipelineStage.Transaction)],
            Config = new ConfigContribution("XiHan:Persistence"),
            Health = [HealthName],
        },
        Entry = typeof(InMemoryPersistenceFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        // 本子系统错误码的多语言目录；同名目录只注册一次，多个 Provider 同时装配也不重复
        context.Services.AddMessageCatalog(PersistenceMessages.Catalog);
        var options = InMemoryPersistenceOptions.FromConfiguration(context.Configuration);
        context.Services.AddSingleton(options);
        // 存储是进程内单例，跨作用域共享；工作单元按作用域注册，同一调用内的仓储共享同一个覆盖层
        context.Services.AddSingleton<InMemoryStore>();
        // 存储自己就是健康项的贡献者：直接看各表的条数与容量
        context.Services.AddSingleton<IHealthContributor>(static sp => sp.GetRequiredService<InMemoryStore>());
        context.Services.TryAddScoped<InMemoryUnitOfWork>();
        context.Services.TryAddScoped<IUnitOfWork>(static sp => sp.GetRequiredService<InMemoryUnitOfWork>());
        context.Services.TryAddSingleton<IIdGenerator<Guid>, GuidV7IdGenerator>();
        context.AddMiddleware<UnitOfWorkMiddleware>(PipelineStage.Transaction);
        // 回放此前登记的实体，并让此后登记的实体立即注册到本 Provider
        context.Services.GetRepositoryRegistrations().ApplyTo(context.Services, this);
    }

    /// <inheritdoc />
    public void Register<TEntity, TKey>(IServiceCollection services)
        where TEntity : class, IEntity<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<IRepository<TEntity, TKey>, InMemoryRepository<TEntity, TKey>>();
    }
}
