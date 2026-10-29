// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using XiHan.Framework.Caching.Abstractions;
using XiHan.Framework.Caching.Abstractions.Caches;
using XiHan.Framework.Caching.Abstractions.Distributed;
using XiHan.Framework.Caching.Abstractions.Idempotency;
using XiHan.Framework.Caching.Abstractions.Locking;
using XiHan.Framework.Caching.Abstractions.Serialization;
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Caching.Redis;

/// <summary>
/// Redis 缓存的能力包入口。
/// </summary>
/// <remarks>
/// 提供 <c>caching.distributed@1</c>，注册共享缓存、分布式锁与幂等中间件。启动时建立连接，停止时关闭；健康项 <see cref="HealthName"/> 按 <see cref="RedisCachingOptions.HealthProbeInterval"/> 周期 PING，连不上时条目 Degraded。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class RedisCachingFeature : IHostedFeature
{
    /// <summary>
    /// 连接绑定名：<c>cache</c>，类型 <c>cache:distributed</c>，可选。
    /// </summary>
    public const string ConnectionBinding = "cache";

    /// <summary>
    /// 健康项名：Redis 连接是否可用（连接已建立且 PING 有应答）。
    /// </summary>
    public const string HealthName = "caching.redis.connection";

    /// <summary>
    /// 启动时取得的连接，停止时关闭。
    /// </summary>
    /// <remarks>
    /// 未启动或已停止为 <see langword="null"/>。
    /// </remarks>
    private IConnectionMultiplexer? _connection;

    /// <summary>
    /// 启动时起循环的健康探针，停止时结束它。
    /// </summary>
    private PeriodicHealthContributor? _health;

    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.caching.redis",
        Package = "XiHan.Framework.Caching.Redis",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Provider,
        Role = ManifestRole.Provider,
        Provides = [new CapabilityDeclaration(CachingCapabilities.Distributed, CachingCapabilities.Version)],
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Bindings = [new Binding(ConnectionBinding, "cache:distributed", required: false)],
        Contributes = new Contributions
        {
            Pipeline = [new PipelineContribution(typeof(IdempotencyMiddleware).FullName!, PipelineStage.Idempotency)],
            Config = new ConfigContribution("XiHan:Caching"),
            Health = [HealthName],
        },
        Entry = typeof(RedisCachingFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        // 本子系统错误码的多语言目录；同名目录只注册一次，多个 Provider 同时装配也不重复
        context.Services.AddMessageCatalog(CachingMessages.Catalog);
        var options = RedisCachingOptions.FromConfiguration(context.Configuration, context.TryGetBinding(ConnectionBinding));
        context.Services.AddSingleton(options);
        context.Services.AddSingleton(IdempotencyOptions.FromConfiguration(context.Configuration));
        // 连接推迟到首次解析时建立，由 StartAsync 触发并核对；装配阶段不触网
        context.Services.AddSingleton<IConnectionMultiplexer>(sp => Connect(sp.GetRequiredService<RedisCachingOptions>().Configuration));
        context.Services.AddSingleton<RedisCacheStore>();
        if (!CacheSerializerRegistration.TryAddJsonSerializer(context.Services))
        {
            // 本包不支持 AOT，允许退回反射版序列化器
            context.Services.AddSingleton<ICacheSerializer>(static _ => JsonCacheSerializer.CreateReflectionBased());
        }

        context.Services.TryAddSingleton<ISharedCache>(static sp => new SerializingCache(sp.GetRequiredService<RedisCacheStore>(), sp.GetRequiredService<ICacheSerializer>()));
        // 被选中的共享存储也以字节级契约登记：应用在"不存在才写 + 比较删"之上搭自己的租约、续约等机制时按接口取用
        context.Services.TryAddSingleton<ICacheStore>(static sp => sp.GetRequiredService<RedisCacheStore>());
        context.Services.TryAddSingleton<IDistributedLock>(static sp => new StoreDistributedLock(sp.GetRequiredService<RedisCacheStore>()));
        context.Services.AddXiHanDistributedCache(static sp => sp.GetRequiredService<RedisCacheStore>());
        context.AddMiddleware<IdempotencyMiddleware>(PipelineStage.Idempotency);
        // 健康项按名字作键登记：几个 Provider 的探针各自独立，内核经 IHealthContributor 统一采集
        context.Services.AddKeyedSingleton(HealthName, (sp, _) => new PeriodicHealthContributor(HealthName, ct => ProbeAsync(sp, ct), options.HealthProbeInterval, sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<ILoggerFactory>()));
        context.Services.AddSingleton<IHealthContributor>(static sp => sp.GetRequiredKeyedService<PeriodicHealthContributor>(HealthName));
    }

    /// <inheritdoc />
    public async Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        // 解析即建立连接；连不上视为启动失败，不降级运行
        _connection = services.GetRequiredService<IConnectionMultiplexer>();
        if (!_connection.IsConnected)
        {
            throw new FrameworkException(CachingErrors.StoreUnavailable, "Redis is not connected.");
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

        if (_connection is not null)
        {
            await _connection.CloseAsync().ConfigureAwait(false);
            _connection = null;
        }
    }

    /// <summary>
    /// 按连接字符串建立连接。
    /// </summary>
    /// <remarks>
    /// 连接或超时失败包装成携带 <see cref="CachingErrors.StoreUnavailable"/> 的框架异常。
    /// </remarks>
    private static ConnectionMultiplexer Connect(string configuration)
    {
        try
        {
            return ConnectionMultiplexer.Connect(configuration);
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            throw new FrameworkException(CachingErrors.StoreUnavailable, $"Failed to connect to Redis: {exception.Message}", exception);
        }
    }

    /// <summary>
    /// 连通性探针。
    /// </summary>
    /// <remarks>
    /// 对共享连接发一次 PING。连接断开时 PING 抛出连接或超时异常，由探针宿主记成 Degraded 并带异常消息。
    /// </remarks>
    private static async ValueTask<Result> ProbeAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var connection = services.GetRequiredService<IConnectionMultiplexer>();
        await connection.GetDatabase().PingAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}
