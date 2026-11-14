// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Caching.Abstractions.Caches;
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
/// 按 ID 读 <see cref="TenantRecord"/>，结果经 <see cref="ICache"/> 缓存（<see cref="GenerationCache{T}"/>，区 <see cref="CacheRegion"/>，不存在也缓存）：失效按租户换代数，所有共用缓存的实例一起失效（ADR-0229）。装了两级缓存时命中在本机一级缓存，租户解析不出进程。<see cref="ListAsync"/> 按 ID 一页一页读、不走缓存。
/// 读取直接开工作单元（见 <see cref="CatalogInvocation"/>），数据库故障以 <see cref="MultiTenancyErrors.StoreUnavailable"/> 返回，不伪装成不存在，也不进缓存；缓存后端故障时直接查库、不写缓存。写入经 <see cref="TenantRecordStore"/>，它写入时与作用域释放时各失效一次（ADR-0194）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class DatabaseTenantStore : ITenantStore
{
    /// <summary>
    /// 租户目录缓存在 <see cref="ICache"/> 里的区名
    /// </summary>
    /// <remarks>
    /// 所有键以它开头；共用一个缓存后端的多个应用要用不同的键前缀（如 Redis 的 <c>KeyPrefix</c>）
    /// </remarks>
    public const string CacheRegion = "multitenancy:tenants";

    /// <summary>
    /// 开作用域与工作单元读仓储的方式
    /// </summary>
    private readonly CatalogInvocation _catalog;

    /// <summary>
    /// 按租户失效的缓存区
    /// </summary>
    private readonly GenerationCache<TenantCacheValue> _cache;

    /// <summary>
    /// 创建
    /// </summary>
    public DatabaseTenantStore(IServiceProvider services, DatabaseMultiTenancyOptions options, ICache cache, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _catalog = new CatalogInvocation(services, loggerFactory.CreateLogger<CatalogInvocation>());
        _cache = new GenerationCache<TenantCacheValue>(cache, CacheRegion, options.CacheTimeToLive, loggerFactory.CreateLogger<DatabaseTenantStore>());
    }

    /// <inheritdoc />
    public async ValueTask<Result<Maybe<TenantInfo>>> FindAsync(string tenantId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantId);
        // 先校验文法再查缓存：不合法的 ID 不可能有行，也不让它占缓存
        if (!TenantIdentifier.IsValid(tenantId))
        {
            return Maybe.None<TenantInfo>();
        }

        var read = await _cache.GetOrLoadManyAsync([GenerationCacheKey.ForScope(tenantId)], LoadAsync, cancellationToken).ConfigureAwait(false);
        if (!read.TryGetValue(out var values))
        {
            return read.Error!;
        }

        return values[0].Tenant is { } tenant ? Maybe.Some(tenant) : Maybe.None<TenantInfo>();
    }

    /// <inheritdoc />
    /// <remarks>
    /// 续页令牌是下一页的起始偏移，对调用方不透明；按 ID 排序分页，期间有租户增删时相邻两页可能错开一条。
    /// 不多取一条判断有没有下一页：每页条数可以取到仓储的单次上限，恰好读完时最后会多一个空页
    /// </remarks>
    public async ValueTask<Result<TenantPage>> ListAsync(TenantQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var valid = query.Validate(TenantQuery.Revision);
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
    /// 丢弃这些租户的缓存（所有共用缓存的实例）
    /// </summary>
    /// <remarks>
    /// 含「不存在」的条目；空集合什么也不做。清全部用 <see cref="InvalidateAllAsync"/>（ADR-0160）。缓存后端故障时抛出：失效没有传到别的实例，调用方据此拒绝写入
    /// </remarks>
    public ValueTask InvalidateAsync(IReadOnlyList<string> tenantIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantIds);
        foreach (var tenantId in tenantIds)
        {
            ArgumentNullException.ThrowIfNull(tenantId, nameof(tenantIds));
        }

        return _cache.InvalidateAsync(tenantIds, cancellationToken);
    }

    /// <summary>
    /// 丢弃全部缓存（所有共用缓存的实例）
    /// </summary>
    /// <remarks>
    /// 缓存后端故障时抛出
    /// </remarks>
    public ValueTask InvalidateAllAsync(CancellationToken cancellationToken) => _cache.InvalidateAllAsync(cancellationToken);

    /// <summary>
    /// 按 ID 一次查库
    /// </summary>
    /// <remarks>
    /// 缓存区按未命中的位置调用；结果与 <paramref name="missing"/> 顺序一一对应，查不到的为不存在
    /// </remarks>
    private async ValueTask<Result<IReadOnlyList<TenantCacheValue>>> LoadAsync(IReadOnlyList<GenerationCacheKey> missing, CancellationToken cancellationToken)
    {
        string[] ids = [.. missing.Select(static key => key.Scope)];
        var read = await _catalog.ReadAsync((repository, ct) => new ValueTask<IReadOnlyList<TenantRecord>>(repository.FindManyAsync(ids, ct)), cancellationToken).ConfigureAwait(false);
        // 失败不进缓存：存储恢复后下一次解析立即重新查库，而不是在存活时间内一直报不可达
        if (!read.TryGetValue(out var records))
        {
            return read.Error!;
        }

        // 数据库的排序规则可能不区分大小写：读回的行按序数核对 ID，迁移之前留下的大写 ID 不被当作它的小写形式（ADR-0196）
        var byId = new Dictionary<string, TenantRecord>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            byId.TryAdd(record.Id, record);
        }

        var values = new TenantCacheValue[ids.Length];
        for (var index = 0; index < ids.Length; index++)
        {
            values[index] = new TenantCacheValue(byId.TryGetValue(ids[index], out var record) ? TenantRecordMapping.ToTenantInfo(record) : null);
        }

        return Result.Success<IReadOnlyList<TenantCacheValue>>(values);
    }
}
