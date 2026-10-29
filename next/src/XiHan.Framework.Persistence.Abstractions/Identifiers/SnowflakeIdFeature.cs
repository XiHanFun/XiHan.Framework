// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Caching.Abstractions.Locking;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Persistence.Abstractions.Identifiers;

/// <summary>
/// 雪花标识特性。
/// </summary>
/// <remarks>
/// 应用显式装配后得到 <see cref="IIdGenerator{TKey}"/>（<see cref="long"/>），Profile 不缺省装配它。配置了 <c>XiHan:Persistence:Snowflake:WorkerId</c> 时机器位固定；否则启动时经容器里的 <see cref="IDistributedLock"/>（<c>caching.distributed</c> 的 Provider 注册，应装配在本特性之前）租约分配，没有锁或机器位已满即启动失败，不做随机机器位。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class SnowflakeIdFeature : IHostedFeature
{
    /// <summary>
    /// 启动时解析出的租约来源。
    /// </summary>
    /// <remarks>
    /// 固定机器位时为 <see langword="null"/>。
    /// </remarks>
    private LeasedWorkerIdSource? _leased;

    /// <summary>
    /// 配置阶段判定是否走租约分配。
    /// </summary>
    private bool _leasing;

    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.persistence.snowflake",
        Package = "XiHan.Framework.Persistence.Abstractions",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Capability,
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Contributes = new Contributions
        {
            Config = new ConfigContribution("XiHan:Persistence"),
            Health = [LeasedWorkerIdSource.HealthName],
        },
        Entry = typeof(SnowflakeIdFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        // 健康项文案的多语言目录；同名目录只注册一次，和持久化 Provider 同时装配也不重复
        context.Services.AddMessageCatalog(PersistenceMessages.Catalog);
        var options = SnowflakeOptions.FromConfiguration(context.Configuration);
        context.Services.AddSingleton(options);
        if (options.WorkerId is { } workerId)
        {
            context.Services.AddSingleton<IWorkerIdSource>(new FixedWorkerIdSource(workerId));
        }
        else
        {
            _leasing = true;
            // 锁在解析时才取：没有的话启动期就以明确的错误码中断，而不是等第一次生成才失败
            context.Services.AddSingleton(sp => new LeasedWorkerIdSource(
                sp.GetService<IDistributedLock>() ?? throw new FrameworkException(PersistenceErrors.WorkerIdUnavailable, $"No '{nameof(IDistributedLock)}' is registered to lease a snowflake worker ID; set '{SnowflakeOptions.ConfigurationSection}:WorkerId' or assemble a 'caching.distributed' provider before '{Manifest.Id}'."),
                options,
                sp.GetRequiredService<TimeProvider>(),
                sp.GetRequiredService<ILoggerFactory>()));
            context.Services.AddSingleton<IWorkerIdSource>(static sp => sp.GetRequiredService<LeasedWorkerIdSource>());
            context.Services.AddSingleton<IHealthContributor>(static sp => sp.GetRequiredService<LeasedWorkerIdSource>());
        }

        context.Services.AddSingleton<SnowflakeIdGenerator>();
        context.Services.TryAddSingleton<IIdGenerator<long>>(static sp => sp.GetRequiredService<SnowflakeIdGenerator>());
    }

    /// <inheritdoc />
    public async Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (_leasing)
        {
            _leased = services.GetRequiredService<LeasedWorkerIdSource>();
            await _leased.StartAsync(cancellationToken).ConfigureAwait(false);
        }

        // 启动期就构造生成器：epoch 在未来之类的配置错误在这里暴露，而不是等第一次生成
        _ = services.GetRequiredService<SnowflakeIdGenerator>();
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_leased is null)
        {
            return;
        }

        await _leased.StopAsync(cancellationToken).ConfigureAwait(false);
        _leased = null;
    }
}
