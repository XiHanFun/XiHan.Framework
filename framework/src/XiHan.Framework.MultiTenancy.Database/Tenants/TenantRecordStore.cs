// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.MultiTenancy.Abstractions.Sharding;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Database.Tenants;

/// <summary>
/// 租户目录的批量读写
/// </summary>
/// <remarks>
/// 作用域服务，须在调用的 Transaction 阶段内使用，用调用方的工作单元。一次查询取回已有行，再用仓储的批量方法一次写入。写入后立即让 <see cref="DatabaseTenantStore"/> 与分片解析器（装配了才有）丢弃对应租户的缓存。
/// 写入在事务提交后才对别的调用可见，所以失效两次（ADR-0194）：写入时一次，作用域释放时（调用已提交或回滚）再一次；缓存按代数丢弃失效之前开始的读取，提交之前读回的旧租户不会留在缓存里
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class TenantRecordStore : ITenantAdministration, IDisposable
{
    /// <summary>
    /// 本作用域里写过的租户：作用域释放时（提交或回滚之后）再失效一次
    /// </summary>
    private readonly HashSet<string> _written = new(StringComparer.Ordinal);

    /// <summary>
    /// 仓储（调用方作用域里的）
    /// </summary>
    private readonly IRepository<TenantRecord, string> _repository;

    /// <summary>
    /// 单例目录
    /// </summary>
    /// <remarks>
    /// 写入后失效其缓存
    /// </remarks>
    private readonly DatabaseTenantStore _catalog;

    /// <summary>
    /// 分片解析器
    /// </summary>
    /// <remarks>
    /// 没有装配分片能力时为 <see langword="null"/>
    /// </remarks>
    private readonly ITenantShardResolver? _shards;

    /// <summary>
    /// 创建
    /// </summary>
    /// <remarks>
    /// <paramref name="shards"/> 是可选依赖，容器里没有时为 <see langword="null"/>
    /// </remarks>
    public TenantRecordStore(IRepository<TenantRecord, string> repository, DatabaseTenantStore catalog, ITenantShardResolver? shards = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _shards = shards;
    }

    /// <summary>
    /// 新增或整体覆盖，按顺序逐项返回
    /// </summary>
    /// <remarks>
    /// 同一租户在同一批里后者覆盖前者（同一行只写一次，各项结果相同）
    /// </remarks>
    public async ValueTask<IReadOnlyList<Result>> UpsertAsync(IReadOnlyList<TenantInfo> tenants, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenants);
        if (tenants.Count == 0)
        {
            return [];
        }

        var ids = tenants.Select(static t => t.Id).Distinct(StringComparer.Ordinal).ToList();
        var existing = (await _repository.FindManyAsync(ids, cancellationToken).ConfigureAwait(false)).ToDictionary(static r => r.Id, StringComparer.Ordinal);
        // 同一租户在一批里只对应一行：已有的更新、没有的新增；后出现的项整份覆盖同一行，各项结果指向同一行的结果
        var rows = new Dictionary<string, int>(StringComparer.Ordinal);
        var updates = new List<TenantRecord>();
        var additions = new List<TenantRecord>();
        var slots = new int[tenants.Count];
        for (var index = 0; index < tenants.Count; index++)
        {
            var tenant = tenants[index];
            if (rows.TryGetValue(tenant.Id, out var slot))
            {
                TenantRecordMapping.Apply(slot >= 0 ? updates[slot] : additions[~slot], tenant);
                slots[index] = slot;
                continue;
            }

            if (existing.TryGetValue(tenant.Id, out var found))
            {
                TenantRecordMapping.Apply(found, tenant);
                updates.Add(found);
                slot = updates.Count - 1;
            }
            else
            {
                additions.Add(TenantRecordMapping.ToRecord(tenant));
                slot = ~(additions.Count - 1);
            }

            rows[tenant.Id] = slot;
            slots[index] = slot;
        }

        var updated = updates.Count == 0 ? [] : await _repository.UpdateRangeAsync(updates, cancellationToken).ConfigureAwait(false);
        var added = additions.Count == 0 ? [] : await _repository.AddRangeAsync(additions, cancellationToken).ConfigureAwait(false);
        var results = new Result[tenants.Count];
        for (var index = 0; index < tenants.Count; index++)
        {
            var slot = slots[index];
            results[index] = slot >= 0 ? updated[slot] : added[~slot];
        }

        Invalidate(ids);
        return results;
    }

    /// <summary>
    /// 删除，按顺序逐项返回
    /// </summary>
    /// <remarks>
    /// 不存在 → <see cref="MultiTenancyErrors.NotFound"/>，同一批里同一 ID 第二次同样不存在。ID 不合法是编程错误，整批在触库前抛出
    /// </remarks>
    public async ValueTask<IReadOnlyList<Result>> RemoveAsync(IReadOnlyList<string> tenantIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantIds);
        if (tenantIds.Count == 0)
        {
            return [];
        }

        var ids = tenantIds.Select(TenantIdentifier.Validate).Distinct(StringComparer.Ordinal).ToList();
        var existing = (await _repository.FindManyAsync(ids, cancellationToken).ConfigureAwait(false)).ToDictionary(static r => r.Id, StringComparer.Ordinal);
        var removals = new List<TenantRecord>();
        var slots = new int[tenantIds.Count];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < tenantIds.Count; index++)
        {
            // 第一次出现且存在的 ID 进入批量删除；重复出现或不存在的直接记为 NotFound
            if (seen.Add(tenantIds[index]) && existing.TryGetValue(tenantIds[index], out var row))
            {
                removals.Add(row);
                slots[index] = removals.Count - 1;
            }
            else
            {
                slots[index] = -1;
            }
        }

        var removed = removals.Count == 0 ? [] : await _repository.RemoveRangeAsync(removals, cancellationToken).ConfigureAwait(false);
        var results = new Result[tenantIds.Count];
        for (var index = 0; index < tenantIds.Count; index++)
        {
            Result outcome = slots[index] >= 0
                ? removed[slots[index]]
                : Error.NotFound(MultiTenancyErrors.NotFound, MultiTenancyMessages.TenantNotFound, MessageArguments.Of(("tenantId", tenantIds[index])));
            results[index] = outcome;
        }

        Invalidate(ids);
        return results;
    }

    /// <summary>
    /// 让目录与分片解析器丢弃这些租户的缓存
    /// </summary>
    private void Invalidate(IReadOnlyList<string> tenantIds)
    {
        _written.UnionWith(tenantIds);
        _catalog.Invalidate(tenantIds);
        _shards?.Invalidate(tenantIds);
    }

    /// <summary>
    /// 作用域释放：再让写过的租户失效一次
    /// </summary>
    /// <remarks>
    /// 管道里的调用先提交、后释放作用域：此后的读取看到的是提交之后的行。可以重复调用
    /// </remarks>
    public void Dispose()
    {
        if (_written.Count == 0)
        {
            return;
        }

        string[] written = [.. _written];
        _written.Clear();
        _catalog.Invalidate(written);
        _shards?.Invalidate(written);
    }
}
