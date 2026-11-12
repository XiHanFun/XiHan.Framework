// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Security.Abstractions.Permissions;

namespace XiHan.Framework.Security.Database.Grants;

/// <summary>
/// 数据库授予存储（单例）
/// </summary>
/// <remarks>
/// 按主体与租户一次查询全局行加该租户的行，结果进有界的 TTL 缓存。读数据库不经管道而是直接开只读工作单元（见 <see cref="GrantInvocation"/>）；数据库故障（连接、超时、事务失败）返回 <see cref="SecurityErrors.StoreUnavailable"/>（HTTP 503）并记警告日志，失败不进缓存，下一次判定重新查库——不能以空集合代替，否则会被判定为不持有（403）。缓存键是（主体种类、主体 ID、租户）；条目按 <see cref="DatabaseSecurityOptions.CacheTimeToLive"/> 过期，条目数达到 <see cref="DatabaseSecurityOptions.CacheSize"/> 时整体清空；<see cref="PermissionGrantRecordStore"/> 写入后对涉及的主体调用 <see cref="Invalidate"/>
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class DatabasePermissionGrantStore : IPermissionGrantStore
{
    /// <summary>
    /// 数据库读取
    /// </summary>
    private readonly GrantInvocation _invocation;

    /// <summary>
    /// 选项：容量与存活时间
    /// </summary>
    private readonly DatabaseSecurityOptions _options;

    /// <summary>
    /// 时钟
    /// </summary>
    /// <remarks>
    /// 判定条目过期
    /// </remarks>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 保护缓存表与主体索引的锁
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// 缓存表：键为主体与租户（<see langword="null"/> 为无租户），值为授予集合与过期时刻
    /// </summary>
    private readonly Dictionary<(GrantSubject Subject, string? Tenant), CacheEntry> _entries = [];

    /// <summary>
    /// 主体索引：主体 → 缓存了哪些租户的条目，供按主体失效
    /// </summary>
    private readonly Dictionary<GrantSubject, HashSet<string?>> _tenantsBySubject = [];

    /// <summary>
    /// 创建
    /// </summary>
    public DatabasePermissionGrantStore(IServiceProvider services, DatabaseSecurityOptions options, TimeProvider clock, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _invocation = new GrantInvocation(services, loggerFactory.CreateLogger<DatabasePermissionGrantStore>());
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>
    /// 缓存的代数
    /// </summary>
    /// <remarks>
    /// 每次失效加一（在锁内）。读取在查库之前记下它，写回缓存时不一致就丢弃：失效之前开始的读取可能读到写入提交之前的旧行（ADR-0194）
    /// </remarks>
    private long _generation;

    /// <summary>
    /// 当前的代数
    /// </summary>
    private long Generation
    {
        get
        {
            lock (_gate)
            {
                return _generation;
            }
        }
    }

    /// <summary>
    /// 当前缓存的条目数（含尚未被访问到的已过期条目）
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
    public async ValueTask<Result<IReadOnlyList<IReadOnlyCollection<string>>>> GetManyAsync(IReadOnlyList<GrantSubject> subjects, string? tenantId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subjects);
        // 调用的租户不能是每个租户的范围：在读缓存与查库之前拒绝（ADR-0197）
        _ = PermissionGrant.VisibleScopes(tenantId);
        if (subjects.Count == 0)
        {
            return Result.Success<IReadOnlyList<IReadOnlyCollection<string>>>([]);
        }

        // 空租户与无租户同义：都只有平台授予，缓存键也归一
        var tenant = string.IsNullOrEmpty(tenantId) ? null : tenantId;
        var found = new Dictionary<GrantSubject, FrozenSet<string>>();
        var missing = new HashSet<GrantSubject>();
        foreach (var subject in subjects)
        {
            if (TryGetCached(subject, tenant, out var cached))
            {
                found[subject] = cached;
            }
            else
            {
                missing.Add(subject);
            }
        }

        // 未命中的主体一次查库；在锁外查，并发未命中各自查一次，后写入的覆盖前者，结果相同。
        // 查库之前记下代数：查的过程中有失效（撤销或授予正在提交），读回的行可能是提交之前的旧值，不写回缓存
        if (missing.Count > 0)
        {
            var generation = Generation;
            var query = EntityQuery.All<PermissionGrantRecord>().Where(GrantRows.EffectiveForAny(missing, tenant)).OrderBy(static r => r.Id);
            var read = await _invocation.ReadAsync((repository, ct) => GrantRows.ListAllAsync(repository, query, ct), cancellationToken).ConfigureAwait(false);
            // 失败不进缓存：存储恢复后下一次判定立即重新查库，而不是在 TTL 内一直报不可达
            if (!read.TryGetValue(out var rows))
            {
                return read.Error!;
            }

            // IN 的笛卡尔积可能带回别的主体的行：按完整主体精确匹配，查不到行的主体是空集合
            var bySubject = rows
                .Select(static r => (Subject: new GrantSubject(r.SubjectKind, r.SubjectId), r.Permission))
                .Where(row => missing.Contains(row.Subject))
                .GroupBy(static row => row.Subject)
                .ToDictionary(static g => g.Key, static g => g.Select(static row => row.Permission).ToFrozenSet(StringComparer.Ordinal));
            foreach (var subject in missing)
            {
                var grants = bySubject.GetValueOrDefault(subject) ?? FrozenSet<string>.Empty;
                Store(subject, tenant, grants, generation);
                found[subject] = grants;
            }
        }

        return Result.Success<IReadOnlyList<IReadOnlyCollection<string>>>(subjects.Select(subject => (IReadOnlyCollection<string>)found[subject]).ToList());
    }

    /// <summary>
    /// 使这些主体的缓存失效
    /// </summary>
    /// <remarks>
    /// 清掉每个主体在所有租户下的条目；空集合什么也不做。清空全部用 <see cref="InvalidateAll"/>（ADR-0160）
    /// </remarks>
    public void Invalidate(IReadOnlyList<GrantSubject> subjects)
    {
        ArgumentNullException.ThrowIfNull(subjects);
        lock (_gate)
        {
            // 代数在锁内加一：此刻还在查库的读取都不能把结果写回来
            _generation++;
            foreach (var subject in subjects)
            {
                if (_tenantsBySubject.Remove(subject, out var tenants))
                {
                    foreach (var tenant in tenants)
                    {
                        _entries.Remove((subject, tenant));
                    }
                }
            }
        }
    }

    /// <summary>
    /// 使全部缓存失效
    /// </summary>
    public void InvalidateAll()
    {
        lock (_gate)
        {
            _generation++;
            _entries.Clear();
            _tenantsBySubject.Clear();
        }
    }

    /// <summary>
    /// 取未过期的缓存条目
    /// </summary>
    /// <remarks>
    /// 过期的条目顺手移除
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
    /// 写入缓存
    /// </summary>
    /// <remarks>
    /// 新增条目前已满就整体清空（不做逐条淘汰，容量只是防止无界增长）
    /// </remarks>
    private void Store(GrantSubject subject, string? tenant, FrozenSet<string> grants, long generation)
    {
        var expiresAt = _options.CacheTimeToLive is { } ttl ? _clock.GetUtcNow() + ttl : (DateTimeOffset?)null;
        lock (_gate)
        {
            // 查库之后有过失效：这次读回的结果可能早于那次写入的提交，丢掉，下一次判定重新查库
            if (generation != _generation)
            {
                return;
            }

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
    /// 一条缓存条目：授予集合（不可变，可直接返回给调用方）与过期时刻（<see langword="null"/> 不过期）
    /// </summary>
    private readonly record struct CacheEntry(FrozenSet<string> Grants, DateTimeOffset? ExpiresAt);
}
