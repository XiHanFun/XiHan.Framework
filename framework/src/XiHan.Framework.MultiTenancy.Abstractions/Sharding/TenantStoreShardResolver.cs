// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Abstractions.Sharding;

/// <summary>
/// 从 <see cref="ITenantStore"/> 的租户属性推出分片
/// </summary>
/// <remarks>
/// <c>ConnectionString</c> 有值即分库，<c>DatabaseProvider</c> 为提供者名，<c>Shard.*</c> 透传。自己不缓存：每次解析读租户存储，缓存与失效只在租户存储一处（数据库租户存储走 <c>ICache</c>，所有实例一起失效），
/// 租户迁库、改连接串、删除经租户管理写入后，所有实例的下一次解析就用上新结果（ADR-0229；以前另有一层按存活时间过期的进程内缓存，别的实例要等它过期，ADR-0193）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class TenantStoreShardResolver : ITenantShardResolver
{
    /// <summary>
    /// 租户存储
    /// </summary>
    private readonly ITenantStore _store;

    /// <summary>
    /// 日志
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 创建
    /// </summary>
    public TenantStoreShardResolver(ITenantStore store, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _logger = loggerFactory.CreateLogger<TenantStoreShardResolver>();
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <inheritdoc />
    public async ValueTask<Result<Maybe<TenantShard>>> ResolveAsync(string tenantId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantId);
        var read = await _store.FindAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (!read.TryGetValue(out var found))
        {
            // 存储的错误作为内部错误保留；reason 只写错误码，存储的细节只进日志（ADR-0115）
            LogStoreFailed(_logger, tenantId, read.Error!.Code);
            return new Error(MultiTenancyErrors.ShardUnavailable, MultiTenancyMessages.ShardUnavailable, MessageArguments.Of(("tenantId", tenantId), ("reason", read.Error.Code)), read.Error);
        }

        return Result.Success(found.TryGetValue(out var info) ? FromProperties(info) : Maybe.None<TenantShard>());
    }

    /// <inheritdoc />
    /// <remarks>
    /// 本实现不缓存，什么也不做：失效由租户存储负责（数据库租户存储经 <c>TenantRecordStore</c> 写入时失效）
    /// </remarks>
    public void Invalidate(IReadOnlyList<string> tenantIds)
    {
        ArgumentNullException.ThrowIfNull(tenantIds);
        foreach (var tenantId in tenantIds)
        {
            ArgumentNullException.ThrowIfNull(tenantId, nameof(tenantIds));
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 本实现不缓存，什么也不做
    /// </remarks>
    public void InvalidateAll()
    {
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
