// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Abstractions.Sharding;

/// <summary>
/// 从 <see cref="ITenantStore"/> 的租户属性推出分片。
/// </summary>
/// <remarks>
/// <c>ConnectionString</c> 有值即分库，<c>DatabaseProvider</c> 为提供者名，<c>Shard.*</c> 透传。结果按租户缓存（有界，满时整体清空）。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class TenantStoreShardResolver : ITenantShardResolver
{
    /// <summary>
    /// 租户存储。
    /// </summary>
    private readonly ITenantStore _store;

    /// <summary>
    /// 缓存容量。
    /// </summary>
    private readonly int _capacity;

    /// <summary>
    /// 租户 ID → 分片（无分片也缓存，避免每次调用都查存储）。
    /// </summary>
    private readonly ConcurrentDictionary<string, Maybe<TenantShard>> _cache = new(StringComparer.Ordinal);

    /// <summary>
    /// 创建。
    /// </summary>
    public TenantStoreShardResolver(ITenantStore store, MultiTenancyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _capacity = options.ShardCacheSize;
    }

    /// <summary>
    /// 已缓存的租户数。
    /// </summary>
    public int CachedCount => _cache.Count;

    /// <inheritdoc />
    public async ValueTask<Result<Maybe<TenantShard>>> ResolveAsync(string tenantId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantId);
        if (_cache.TryGetValue(tenantId, out var cached))
        {
            return Result.Success(cached);
        }

        Maybe<TenantInfo> found;
        try
        {
            found = await _store.FindAsync(tenantId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Error.Unavailable(MultiTenancyErrors.ShardUnavailable, MultiTenancyMessages.ShardUnavailable, MessageArguments.Of(("tenantId", tenantId), ("reason", exception.Message)));
        }

        var shard = found.TryGetValue(out var info) ? FromProperties(info) : Maybe.None<TenantShard>();
        // 有界：满了整体清空，比逐条淘汰简单且足够（租户数远小于容量）
        if (_cache.Count >= _capacity)
        {
            _cache.Clear();
        }

        _cache[tenantId] = shard;
        return Result.Success(shard);
    }

    /// <inheritdoc />
    public void Invalidate(string? tenantId = null)
    {
        if (tenantId is null)
        {
            _cache.Clear();
        }
        else
        {
            _cache.TryRemove(tenantId, out _);
        }
    }

    /// <summary>
    /// 从租户属性建分片。
    /// </summary>
    /// <remarks>
    /// 没有连接串即无分片。
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
}
