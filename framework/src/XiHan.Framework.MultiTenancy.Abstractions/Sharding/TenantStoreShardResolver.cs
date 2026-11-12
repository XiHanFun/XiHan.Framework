// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Abstractions.Sharding;

/// <summary>
/// 从 <see cref="ITenantStore"/> 的租户属性推出分片
/// </summary>
/// <remarks>
/// <c>ConnectionString</c> 有值即分库，<c>DatabaseProvider</c> 为提供者名，<c>Shard.*</c> 透传。结果按租户缓存（有界，满时整体清空），条目过了 <see cref="MultiTenancyOptions.ShardCacheTimeToLive"/> 即重新读租户存储：多实例部署里失效通知只到本实例（ADR-0193）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class TenantStoreShardResolver : ITenantShardResolver
{
    /// <summary>
    /// 租户存储
    /// </summary>
    private readonly ITenantStore _store;

    /// <summary>
    /// 缓存容量
    /// </summary>
    private readonly int _capacity;

    /// <summary>
    /// 缓存条目的存活时间，为空不过期
    /// </summary>
    private readonly TimeSpan? _timeToLive;

    /// <summary>
    /// 时钟
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 租户 ID → 分片与过期时刻（无分片也缓存，避免每次调用都查存储）
    /// </summary>
    private readonly ConcurrentDictionary<string, (Maybe<TenantShard> Shard, DateTimeOffset? ExpiresAt)> _cache = new(StringComparer.Ordinal);

    /// <summary>
    /// 保护代数与「核对代数再写回」的锁
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// 缓存的代数
    /// </summary>
    /// <remarks>
    /// 每次失效加一。解析在读租户存储之前记下它，写回缓存时不一致就丢弃：失效之前开始的解析可能读到写入提交之前的租户（ADR-0194）
    /// </remarks>
    private long _generation;

    /// <summary>
    /// 日志
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 创建
    /// </summary>
    public TenantStoreShardResolver(ITenantStore store, MultiTenancyOptions options, ILoggerFactory loggerFactory, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _logger = loggerFactory.CreateLogger<TenantStoreShardResolver>();
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _capacity = options.ShardCacheSize;
        _timeToLive = options.ShardCacheTimeToLive;
    }

    /// <summary>
    /// 已缓存的租户数
    /// </summary>
    public int CachedCount => _cache.Count;

    /// <inheritdoc />
    public async ValueTask<Result<Maybe<TenantShard>>> ResolveAsync(string tenantId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantId);
        var now = _clock.GetUtcNow();
        // 过期的条目当作没有：重新读租户存储，别的实例改过的连接串在存活时间之后生效
        if (_cache.TryGetValue(tenantId, out var cached) && (cached.ExpiresAt is not { } expiresAt || now < expiresAt))
        {
            return Result.Success(cached.Shard);
        }

        long generation;
        lock (_gate)
        {
            generation = _generation;
        }

        var read = await _store.FindAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (!read.TryGetValue(out var found))
        {
            // 存储的错误作为内部错误保留；reason 只写错误码，存储的细节只进日志（ADR-0115）
            LogStoreFailed(_logger, tenantId, read.Error!.Code);
            return new Error(MultiTenancyErrors.ShardUnavailable, MultiTenancyMessages.ShardUnavailable, MessageArguments.Of(("tenantId", tenantId), ("reason", read.Error.Code)), read.Error);
        }

        var shard = found.TryGetValue(out var info) ? FromProperties(info) : Maybe.None<TenantShard>();
        lock (_gate)
        {
            // 读存储之后有过失效：这次结果可能早于那次写入的提交，照常返回但不缓存
            if (generation == _generation)
            {
                // 有界：满了整体清空，比逐条淘汰简单且足够（租户数远小于容量）
                if (_cache.Count >= _capacity)
                {
                    _cache.Clear();
                }

                _cache[tenantId] = (shard, now + _timeToLive);
            }
        }

        return Result.Success(shard);
    }

    /// <inheritdoc />
    public void Invalidate(IReadOnlyList<string> tenantIds)
    {
        ArgumentNullException.ThrowIfNull(tenantIds);
        lock (_gate)
        {
            _generation++;
            foreach (var tenantId in tenantIds)
            {
                ArgumentNullException.ThrowIfNull(tenantId, nameof(tenantIds));
                _cache.TryRemove(tenantId, out _);
            }
        }
    }

    /// <inheritdoc />
    public void InvalidateAll()
    {
        lock (_gate)
        {
            _generation++;
            _cache.Clear();
        }
    }

    /// <summary>
    /// 从租户属性建分片
    /// </summary>
    /// <remarks>
    /// 没有连接串即无分片
    /// </remarks>
    public static Maybe<TenantShard> FromProperties(TenantInfo tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        if (!tenant.Properties.TryGetValue(TenantShardKeys.ConnectionString, out var connectionString) || string.IsNullOrWhiteSpace(connectionString))
        {
            return Maybe.None<TenantShard>();
        }

        var properties = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in tenant.Properties)
        {
            if (key.StartsWith(TenantShardKeys.PropertyPrefix, StringComparison.Ordinal) && key.Length > TenantShardKeys.PropertyPrefix.Length)
            {
                properties[key[TenantShardKeys.PropertyPrefix.Length..]] = value;
            }
        }

        tenant.Properties.TryGetValue(TenantShardKeys.DatabaseProvider, out var provider);
        return Maybe.Some(new TenantShard(tenant.Id, connectionString, provider, properties));
    }

    /// <summary>
    /// 查租户存储失败
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Resolving the shard of tenant '{TenantId}' failed: the tenant store returned '{ErrorCode}'.")]
    private static partial void LogStoreFailed(ILogger logger, string tenantId, string errorCode);
}
