// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Frozen;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions.Permissions;

namespace XiHan.Framework.Security.Database.Grants;

/// <summary>
/// 数据库授予存储（单例）。
/// </summary>
/// <remarks>
/// 按主体与租户一次查询全局行加该租户的行，结果进有界的 TTL 缓存。读数据库不经管道而是直接开只读工作单元（见 <see cref="GrantInvocation"/>）；基础设施异常原样抛出，<see cref="PermissionChecker"/> 不捕获，由内核转成 Unexpected 结果。缓存键是（主体种类、主体 ID、租户）；条目按 <see cref="DatabaseSecurityOptions.CacheTimeToLive"/> 过期，条目数达到 <see cref="DatabaseSecurityOptions.CacheSize"/> 时整体清空；<see cref="PermissionGrantRecordStore"/> 写入后对涉及的主体调用 <see cref="Invalidate"/>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class DatabasePermissionGrantStore : IPermissionGrantStore
{
    /// <summary>
    /// 数据库读取。
    /// </summary>
    private readonly GrantInvocation _invocation;

    /// <summary>
    /// 选项：容量与存活时间。
    /// </summary>
    private readonly DatabaseSecurityOptions _options;

    /// <summary>
    /// 时钟。
    /// </summary>
    /// <remarks>
    /// 判定条目过期。
    /// </remarks>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 保护缓存表与主体索引的锁。
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// 缓存表：键为主体与租户（<see langword="null"/> 为无租户），值为授予集合与过期时刻。
    /// </summary>
    private readonly Dictionary<(GrantSubject Subject, string? Tenant), CacheEntry> _entries = [];

    /// <summary>
    /// 主体索引：主体 → 缓存了哪些租户的条目，供按主体失效。
    /// </summary>
    private readonly Dictionary<GrantSubject, HashSet<string?>> _tenantsBySubject = [];

    /// <summary>
    /// 创建。
    /// </summary>
    public DatabasePermissionGrantStore(IServiceProvider services, DatabaseSecurityOptions options, TimeProvider clock)
    {
        _invocation = new GrantInvocation(services);
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>
    /// 当前缓存的条目数（含尚未被访问到的已过期条目）。
    /// </summary>
    public int CachedEntryCount
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyCollection<string>> GetGrantsAsync(GrantSubject subject, string? tenantId, CancellationToken cancellationToken)
    {
        // 空租户与无租户同义：都只有全局授予，缓存键也归一
        var tenant = string.IsNullOrEmpty(tenantId) ? null : tenantId;
        if (TryGetCached(subject, tenant, out var cached))
        {
            return cached;
        }

        // 未命中时在锁外查库：并发未命中各自查一次，后写入的覆盖前者，结果相同
        var query = EntityQuery.All<PermissionGrantRecord>().Where(GrantRows.EffectiveFor(subject, tenant)).OrderBy(static r => r.Id);
        var rows = await _invocation.ReadAsync((repository, ct) => GrantRows.ListAllAsync(repository, query, ct), cancellationToken).ConfigureAwait(false);
        var grants = rows.Select(static r => r.Permission).ToFrozenSet(StringComparer.Ordinal);
        Store(subject, tenant, grants);
        return grants;
    }

    /// <summary>
    /// 使缓存失效：给主体时清掉该主体在所有租户下的条目，不给时清空全部。
    /// </summary>
    public void Invalidate(GrantSubject? subject = null)
    {
        lock (_gate)
        {
            if (subject is not { } target)
            {
                _entries.Clear();
                _tenantsBySubject.Clear();
                return;
            }

            if (_tenantsBySubject.Remove(target, out var tenants))
            {
                foreach (var tenant in tenants)
                {
                    _entries.Remove((target, tenant));
                }
            }
        }
    }

    /// <summary>
    /// 取未过期的缓存条目。
    /// </summary>
    /// <remarks>
    /// 过期的条目顺手移除。
    /// </remarks>
    private bool TryGetCached(GrantSubject subject, string? tenant, out FrozenSet<string> grants)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue((subject, tenant), out var entry))
            {
                if (entry.ExpiresAt is not { } expiresAt || expiresAt > _clock.GetUtcNow())
                {
                    grants = entry.Grants;
                    return true;
                }

                // 过期条目不再可用：从表与索引里移除，由调用方重新查库
                _entries.Remove((subject, tenant));
                if (_tenantsBySubject.TryGetValue(subject, out var tenants) && tenants.Remove(tenant) && tenants.Count == 0)
                {
                    _tenantsBySubject.Remove(subject);
                }
            }
        }

        grants = FrozenSet<string>.Empty;
        return false;
    }

    /// <summary>
    /// 写入缓存。
    /// </summary>
    /// <remarks>
    /// 新增条目前已满就整体清空（不做逐条淘汰，容量只是防止无界增长）。
    /// </remarks>
    private void Store(GrantSubject subject, string? tenant, FrozenSet<string> grants)
    {
        var expiresAt = _options.CacheTimeToLive is { } ttl ? _clock.GetUtcNow() + ttl : (DateTimeOffset?)null;
        lock (_gate)
        {
            if (!_entries.ContainsKey((subject, tenant)) && _entries.Count >= _options.CacheSize)
            {
                _entries.Clear();
                _tenantsBySubject.Clear();
            }

            _entries[(subject, tenant)] = new CacheEntry(grants, expiresAt);
            if (!_tenantsBySubject.TryGetValue(subject, out var tenants))
            {
                tenants = [];
                _tenantsBySubject[subject] = tenants;
            }

            tenants.Add(tenant);
        }
    }

    /// <summary>
    /// 一条缓存条目：授予集合（不可变，可直接返回给调用方）与过期时刻（<see langword="null"/> 不过期）。
    /// </summary>
    private readonly record struct CacheEntry(FrozenSet<string> Grants, DateTimeOffset? ExpiresAt);
}
