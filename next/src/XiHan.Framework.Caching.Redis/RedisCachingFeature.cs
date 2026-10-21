// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;
using XiHan.Framework.Caching.Abstractions;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Caching.Redis;

/// <summary>
/// Redis 缓存的能力包入口：提供 <c>caching.distributed@1</c>，注册共享缓存、分布式锁与幂等中间件。启动时建立连接，停止时关闭。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class RedisCachingFeature : IHostedFeature
{
    /// <summary>
    /// 连接绑定名：<c>cache</c>，类型 <c>cache:distributed</c>，可选。
    /// </summary>
    public const string ConnectionBinding = "cache";

    /// <summary>
    /// 启动时取得的连接，停止时关闭；未启动或已停止为 <see langword="null"/>。
    /// </summary>
    private IConnectionMultiplexer? _connection;

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
            Health = ["caching.redis.connection"],
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
        context.Services.TryAddSingleton<IDistributedLock>(static sp => new StoreDistributedLock(sp.GetRequiredService<RedisCacheStore>()));
        context.AddMiddleware<IdempotencyMiddleware>(PipelineStage.Idempotency);
    }

    /// <inheritdoc />
    public Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        // 解析即建立连接；连不上视为启动失败，不降级运行
        _connection = services.GetRequiredService<IConnectionMultiplexer>();
        if (!_connection.IsConnected)
        {
            throw new FrameworkException(CachingErrors.StoreUnavailable, "Redis is not connected.");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
        {
            await _connection.CloseAsync().ConfigureAwait(false);
            _connection = null;
        }
    }

    /// <summary>
    /// 按连接字符串建立连接；连接或超时失败包装成携带 <see cref="CachingErrors.StoreUnavailable"/> 的框架异常。
    /// </summary>
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
}
