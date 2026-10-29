// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Database.Tenants;

/// <summary>
/// 数据库租户目录（单例）。
/// </summary>
/// <remarks>
/// 按 ID 读 <see cref="TenantRecord"/>，结果按租户有界缓存（不存在也缓存），每条带存活时间；<see cref="ListAsync"/> 按 ID 分页读完、不走缓存。读取直接开工作单元（见 <see cref="CatalogInvocation"/>），基础设施异常原样抛出，不伪装成不存在。写入经 <see cref="TenantRecordStore"/>，它写完即失效对应条目；失效与提交之间的窗口由存活时间收敛。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class DatabaseTenantStore : ITenantStore
{
    /// <summary>
    /// 开作用域与工作单元读仓储的方式。
    /// </summary>
    private readonly CatalogInvocation _catalog;

    /// <summary>
    /// 缓存容量、存活时间与枚举上限。
    /// </summary>
    private readonly DatabaseMultiTenancyOptions _options;

    /// <summary>
    /// 时钟。
    /// </summary>
    /// <remarks>
    /// 判定缓存条目是否过期。
    /// </remarks>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 租户 ID → 缓存条目。
    /// </summary>
    /// <remarks>
    /// 读取无锁。
    /// </remarks>
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);

    /// <summary>
    /// 创建。
    /// </summary>
    public DatabaseTenantStore(IServiceProvider services, DatabaseMultiTenancyOptions options, TimeProvider clock, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _catalog = new CatalogInvocation(services);
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = loggerFactory.CreateLogger<DatabaseTenantStore>();
    }

    /// <summary>
    /// 已缓存的条目数（含“不存在”的条目）。
    /// </summary>
    public int CachedCount => _cache.Count;

    /// <inheritdoc />
    public async ValueTask<Maybe<TenantInfo>> FindAsync(string tenantId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantId);
        // 先校验文法再查库：不合法的 ID 不可能有行，也不让它占缓存
        if (!TenantIdentifier.IsValid(tenantId))
        {
            return default;
        }

        if (_cache.TryGetValue(tenantId, out var entry) && !entry.IsExpired(_clock.GetUtcNow()))
        {
            return entry.Tenant;
        }

        var found = await _catalog.ReadAsync((repository, ct) => repository.FindAsync(tenantId, ct), cancellationToken).ConfigureAwait(false);
        var tenant = found.TryGetValue(out var record) ? Maybe.Some(TenantRecordMapping.ToTenantInfo(record)) : Maybe.None<TenantInfo>();
        // 有界：满了整体清空，比逐条淘汰简单且足够（租户数远小于容量）；不存在也缓存，避免反复查不存在的租户
        if (_cache.Count >= _options.CacheSize)
        {
            _cache.Clear();
        }

        _cache[tenantId] = new CacheEntry(tenant, _options.CacheTimeToLive is { } ttl ? _clock.GetUtcNow() + ttl : null);
        return tenant;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<TenantInfo>> ListAsync(CancellationToken cancellationToken)
    {
        var (records, truncated) = await _catalog.ReadAsync(ReadAllAsync, cancellationToken).ConfigureAwait(false);
        if (truncated)
        {
            LogLimitReached(_logger, _options.MaxListedTenants);
        }

        return records.Select(TenantRecordMapping.ToTenantInfo).ToList();
    }

    /// <summary>
    /// 丢弃缓存：<paramref name="tenantId"/> 为空清全部，否则只清该租户（含“不存在”的条目）。
    /// </summary>
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
    /// 在一个工作单元里按 ID 分页读完全部行。
    /// </summary>
    /// <remarks>
    /// 达到上限即停止并报告截断。
    /// </remarks>
    private async ValueTask<(List<TenantRecord> Records, bool Truncated)> ReadAllAsync(IRepository<TenantRecord, string> repository, CancellationToken cancellationToken)
    {
        var loaded = new List<TenantRecord>();
        var skip = 0;
        while (true)
        {
            // 按 ID 分页读到不足一页或达到上限；上限之外的租户不返回，枚举不能无界
            var take = Math.Min(EntityQuery.DefaultMaxTake, _options.MaxListedTenants - loaded.Count);
            var page = await repository.ListAsync(EntityQuery.All<TenantRecord>().OrderBy(static t => t.Id).Page(skip, take), cancellationToken).ConfigureAwait(false);
            loaded.AddRange(page);
            if (page.Count < take)
            {
                return (loaded, false);
            }

            skip += page.Count;
            if (loaded.Count >= _options.MaxListedTenants)
            {
                return (loaded, true);
            }
        }
    }

    /// <summary>
    /// 枚举达到上限。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Only the first {Limit} tenants were listed; raise XiHan:MultiTenancy:Database:MaxListedTenants to list the rest.")]
    private static partial void LogLimitReached(ILogger logger, int limit);

    /// <summary>
    /// 一条缓存：租户（不存在为无值）与过期时刻（无存活时间为 <see langword="null"/>）。
    /// </summary>
    private readonly record struct CacheEntry(Maybe<TenantInfo> Tenant, DateTimeOffset? ExpiresAt)
    {
        /// <summary>
        /// 到 <paramref name="now"/> 是否已过期。
        /// </summary>
        public bool IsExpired(DateTimeOffset now) => ExpiresAt is { } expiresAt && expiresAt <= now;
    }
}
