// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Caching.Abstractions.Caches;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Security.Abstractions.Permissions;

namespace XiHan.Framework.Security.Database.Grants;

/// <summary>
/// 数据库授予存储（单例）
/// </summary>
/// <remarks>
/// 按主体与租户一次查询全局行加该租户的行，结果经 <see cref="ICache"/> 缓存（<see cref="GenerationCache{T}"/>，区 <see cref="CacheRegion"/>）：范围是主体、条目是租户，失效按主体换代数，所有共用缓存的实例一起失效（ADR-0229）。装了两级缓存时命中在本机一级缓存，判定不出进程。
/// 读数据库不经管道而是直接开只读工作单元（见 <see cref="GrantInvocation"/>）；数据库故障（连接、超时、事务失败）返回 <see cref="SecurityErrors.StoreUnavailable"/>（HTTP 503）并记警告日志，失败不进缓存，下一次判定重新查库——不能以空集合代替，否则会被判定为不持有（403）。
/// 缓存后端故障时判定直接查库、不写缓存，结果仍然正确；条目按 <see cref="DatabaseSecurityOptions.CacheTimeToLive"/> 过期，容量由提供 <c>caching.local</c> 的 Provider 管。<see cref="PermissionGrantRecordStore"/> 写入后对涉及的主体调用 <see cref="InvalidateAsync"/>
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class DatabasePermissionGrantStore : IPermissionGrantStore
{
    /// <summary>
    /// 授予缓存在 <see cref="ICache"/> 里的区名
    /// </summary>
    /// <remarks>
    /// 所有键以它开头；共用一个缓存后端的多个应用要用不同的键前缀（如 Redis 的 <c>KeyPrefix</c>）
    /// </remarks>
    public const string CacheRegion = "security:grants";

    /// <summary>
    /// 数据库读取
    /// </summary>
    private readonly GrantInvocation _invocation;

    /// <summary>
    /// 按主体失效的缓存区
    /// </summary>
    private readonly GenerationCache<GrantSetEntry> _cache;

    /// <summary>
    /// 创建
    /// </summary>
    public DatabasePermissionGrantStore(IServiceProvider services, DatabaseSecurityOptions options, ICache cache, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _invocation = new GrantInvocation(services, loggerFactory.CreateLogger<DatabasePermissionGrantStore>());
        _cache = new GenerationCache<GrantSetEntry>(cache, CacheRegion, options.CacheTimeToLive, loggerFactory.CreateLogger<DatabasePermissionGrantStore>());
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

        // 空租户与无租户同义：都只有平台授予，缓存条目也归一为空字符串（租户 ID 不会是空字符串）
        var tenant = string.IsNullOrEmpty(tenantId) ? null : tenantId;
        var keys = new GenerationCacheKey[subjects.Count];
        for (var index = 0; index < subjects.Count; index++)
        {
            keys[index] = new GenerationCacheKey(ScopeOf(subjects[index]), tenant ?? string.Empty);
        }

        var read = await _cache.GetOrLoadManyAsync(keys, (missing, ct) => LoadAsync(missing, tenant, ct), cancellationToken).ConfigureAwait(false);
        if (!read.TryGetValue(out var entries))
        {
            return read.Error!;
        }

        var results = new IReadOnlyCollection<string>[entries.Count];
        for (var index = 0; index < entries.Count; index++)
        {
            results[index] = entries[index].Set;
        }

        return Result.Success<IReadOnlyList<IReadOnlyCollection<string>>>(results);
    }

    /// <summary>
    /// 使这些主体的缓存失效（所有共用缓存的实例）
    /// </summary>
    /// <remarks>
    /// 主体在所有租户下的条目都作废；空集合什么也不做。清空全部用 <see cref="InvalidateAllAsync"/>（ADR-0160）。缓存后端故障时抛出：失效没有传到别的实例，调用方据此拒绝写入
    /// </remarks>
    public ValueTask InvalidateAsync(IReadOnlyList<GrantSubject> subjects, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subjects);
        if (subjects.Count == 0)
        {
            return ValueTask.CompletedTask;
        }

        var scopes = new string[subjects.Count];
        for (var index = 0; index < subjects.Count; index++)
        {
            scopes[index] = ScopeOf(subjects[index]);
        }

        return _cache.InvalidateAsync(scopes, cancellationToken);
    }

    /// <summary>
    /// 使全部缓存失效（所有共用缓存的实例）
    /// </summary>
    /// <remarks>
    /// 缓存后端故障时抛出
    /// </remarks>
    public ValueTask InvalidateAllAsync(CancellationToken cancellationToken) => _cache.InvalidateAllAsync(cancellationToken);

    /// <summary>
    /// 主体在缓存里的范围
    /// </summary>
    /// <remarks>
    /// 种类只有 <c>user</c> / <c>role</c>、不含冒号，拼上 ID 不会与别的主体相同；ID 区分大小写
    /// </remarks>
    private static string ScopeOf(GrantSubject subject) => subject.Kind + ":" + subject.Id;

    /// <summary>
    /// 一次查库取这些主体在这个租户下生效的授予
    /// </summary>
    /// <remarks>
    /// 缓存区按未命中的位置调用；结果与 <paramref name="missing"/> 顺序一一对应，查不到行的主体是空集合
    /// </remarks>
    private async ValueTask<Result<IReadOnlyList<GrantSetEntry>>> LoadAsync(IReadOnlyList<GenerationCacheKey> missing, string? tenant, CancellationToken cancellationToken)
    {
        var subjects = new List<GrantSubject>(missing.Count);
        foreach (var key in missing)
        {
            var separator = key.Scope.IndexOf(':', StringComparison.Ordinal);
            var subject = new GrantSubject(key.Scope[..separator], key.Scope[(separator + 1)..]);
            subjects.Add(subject);
        }

        var query = EntityQuery.All<PermissionGrantRecord>().Where(GrantRows.EffectiveForAny(subjects, tenant)).OrderBy(static r => r.Id);
        var read = await _invocation.ReadAsync((repository, ct) => GrantRows.ListAllAsync(repository, query, ct), cancellationToken).ConfigureAwait(false);
        if (!read.TryGetValue(out var rows))
        {
            return read.Error!;
        }

        // IN 的笛卡尔积可能带回别的主体的行：按完整主体精确匹配
        var wanted = new HashSet<GrantSubject>(subjects);
        var grouped = rows
            .Select(static r => (Subject: new GrantSubject(r.SubjectKind, r.SubjectId), r.Permission))
            .Where(row => wanted.Contains(row.Subject))
            .GroupBy(static row => row.Subject)
            .ToDictionary(static g => g.Key, static g => g.Select(static row => row.Permission).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
        var entries = new GrantSetEntry[subjects.Count];
        for (var index = 0; index < subjects.Count; index++)
        {
            entries[index] = new GrantSetEntry(grouped.GetValueOrDefault(subjects[index]) ?? []);
        }

        return Result.Success<IReadOnlyList<GrantSetEntry>>(entries);
    }
}
