// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Domain.Specifications;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Database.Tenants;

/// <summary>
/// 数据库租户目录（单例）
/// </summary>
/// <remarks>
/// 按 ID 读 <see cref="TenantRecord"/>，结果按租户有界缓存（不存在也缓存），每条带存活时间；<see cref="ListAsync"/> 按 ID 一页一页读、不走缓存。读取直接开工作单元（见 <see cref="CatalogInvocation"/>），数据库故障以 <see cref="MultiTenancyErrors.StoreUnavailable"/> 返回，不伪装成不存在。写入经 <see cref="TenantRecordStore"/>，它写完即失效对应条目；失效与提交之间的窗口由存活时间收敛
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class DatabaseTenantStore : ITenantStore
{
    /// <summary>
    /// 开作用域与工作单元读仓储的方式
    /// </summary>
    private readonly CatalogInvocation _catalog;

    /// <summary>
    /// 缓存容量与存活时间
    /// </summary>
    private readonly DatabaseMultiTenancyOptions _options;

    /// <summary>
    /// 时钟
    /// </summary>
    /// <remarks>
    /// 判定缓存条目是否过期
    /// </remarks>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 日志
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 租户 ID → 缓存条目
    /// </summary>
    /// <remarks>
    /// 读取无锁
    /// </remarks>
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);

    /// <summary>
    /// 保护代数与「核对代数再写回」的锁
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// 缓存的代数
    /// </summary>
    /// <remarks>
    /// 每次失效加一。读取在查库之前记下它，写回缓存时不一致就丢弃：失效之前开始的读取可能读到写入提交之前的旧行（ADR-0194）
    /// </remarks>
    private long _generation;

    /// <summary>
    /// 创建
    /// </summary>
    public DatabaseTenantStore(IServiceProvider services, DatabaseMultiTenancyOptions options, TimeProvider clock, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _catalog = new CatalogInvocation(services, loggerFactory.CreateLogger<CatalogInvocation>());
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = loggerFactory.CreateLogger<DatabaseTenantStore>();
    }

    /// <summary>
    /// 已缓存的条目数（含“不存在”的条目）
    /// </summary>
    public int CachedCount => _cache.Count;

    /// <inheritdoc />
    public async ValueTask<Result<Maybe<TenantInfo>>> FindAsync(string tenantId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantId);
        // 先校验文法再查库：不合法的 ID 不可能有行，也不让它占缓存
        if (!TenantIdentifier.IsValid(tenantId))
        {
            return Maybe.None<TenantInfo>();
        }

        if (_cache.TryGetValue(tenantId, out var entry) && !entry.IsExpired(_clock.GetUtcNow()))
        {
            return entry.Tenant;
        }

        // 查库之前记下代数：查的过程中有失效（目录写入正在提交），读回的行可能是提交之前的旧值，不写回缓存
        long generation;
        lock (_gate)
        {
            generation = _generation;
        }

        var read = await _catalog.ReadAsync((repository, ct) => repository.FindAsync(tenantId, ct), cancellationToken).ConfigureAwait(false);
        // 失败不进缓存：存储恢复后下一次解析立即重新查库，而不是在存活时间内一直报不可达
        if (!read.TryGetValue(out var found))
        {
            return read.Error!;
        }

        var tenant = found.TryGetValue(out var record) ? Maybe.Some(TenantRecordMapping.ToTenantInfo(record)) : Maybe.None<TenantInfo>();
        // 有界：满了整体清空，比逐条淘汰简单且足够（租户数远小于容量）；不存在也缓存，避免反复查不存在的租户
        lock (_gate)
        {
            // 查库之后有过失效：这次读回的结果可能早于那次写入的提交，照常返回但不缓存
            if (generation == _generation)
            {
                if (_cache.Count >= _options.CacheSize)
                {
                    _cache.Clear();
                }

                _cache[tenantId] = new CacheEntry(tenant, _options.CacheTimeToLive is { } ttl ? _clock.GetUtcNow() + ttl : null);
            }
        }

        return tenant;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 续页令牌是下一页的起始偏移，对调用方不透明；按 ID 排序分页，期间有租户增删时相邻两页可能错开一条。
    /// 不多取一条判断有没有下一页：每页条数可以取到仓储的单次上限，恰好读完时最后会多一个空页
    /// </remarks>
    public async ValueTask<Result<TenantPage>> ListAsync(TenantQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var valid = query.Validate();
        if (valid.IsFailure)
        {
            return valid.Error!;
        }

        var skip = 0;
        if (query.ContinuationToken is { } token && !(int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out skip) && skip > 0))
        {
            return Error.Validation(MultiTenancyErrors.QueryInvalid, MultiTenancyMessages.QueryTokenInvalid);
        }

        var source = EntityQuery.All<TenantRecord>();
        var filtered = query.EnabledOnly ? source.Where(new Specification<TenantRecord>(static r => r.IsEnabled)) : source;
        var page = filtered.OrderBy(static r => r.Id).Page(skip, query.PageSize);
        var read = await _catalog.ReadAsync((repository, ct) => new ValueTask<IReadOnlyList<TenantRecord>>(repository.ListAsync(page, ct)), cancellationToken).ConfigureAwait(false);
        if (!read.TryGetValue(out var records))
        {
            return read.Error!;
        }

        return new TenantPage
        {
            Items = records.Select(TenantRecordMapping.ToTenantInfo).ToList(),
            ContinuationToken = records.Count == query.PageSize ? (skip + records.Count).ToString(CultureInfo.InvariantCulture) : null,
        };
    }

    /// <summary>
    /// 丢弃这些租户的缓存
    /// </summary>
    /// <remarks>
    /// 含「不存在」的条目；空集合什么也不做。清全部用 <see cref="InvalidateAll"/>（ADR-0160）
    /// </remarks>
    public void Invalidate(IReadOnlyList<string> tenantIds)
    {
        ArgumentNullException.ThrowIfNull(tenantIds);
        lock (_gate)
        {
            // 代数在锁内加一：此刻还在查库的读取都不能把结果写回来
            _generation++;
            foreach (var tenantId in tenantIds)
            {
                ArgumentNullException.ThrowIfNull(tenantId, nameof(tenantIds));
                _cache.TryRemove(tenantId, out _);
            }
        }
    }

    /// <summary>
    /// 丢弃全部缓存
    /// </summary>
    public void InvalidateAll()
    {
        lock (_gate)
        {
            _generation++;
            _cache.Clear();
        }
    }

    /// <summary>
    /// 一条缓存：租户（不存在为无值）与过期时刻（无存活时间为 <see langword="null"/>）
    /// </summary>
    private readonly record struct CacheEntry(Maybe<TenantInfo> Tenant, DateTimeOffset? ExpiresAt)
    {
        /// <summary>
        /// 到 <paramref name="now"/> 是否已过期
        /// </summary>
        public bool IsExpired(DateTimeOffset now) => ExpiresAt is { } expiresAt && expiresAt <= now;
    }
}
