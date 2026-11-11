// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.MultiTenancy.Abstractions.Sharding;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.InMemory;

/// <summary>
/// 内存租户表的管理
/// </summary>
/// <remarks>
/// 写进 <see cref="InMemoryTenantStore"/> 后让分片解析器丢弃这些租户的缓存。单独一个类型而不是由存储实现：分片解析器读存储，存储再依赖解析器会成环（ADR-0160）
/// </remarks>
internal sealed class InMemoryTenantAdministration(InMemoryTenantStore store, ITenantShardResolver? shards) : ITenantAdministration
{
    /// <summary>
    /// 租户表
    /// </summary>
    private readonly InMemoryTenantStore _store = store ?? throw new ArgumentNullException(nameof(store));

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Result>> UpsertAsync(IReadOnlyList<TenantInfo> tenants, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenants);
        foreach (var tenant in tenants)
        {
            ArgumentNullException.ThrowIfNull(tenant, nameof(tenants));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var results = _store.Upsert(tenants);
        shards?.Invalidate([.. tenants.Select(static t => t.Id)]);
        return ValueTask.FromResult(results);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Result>> RemoveAsync(IReadOnlyList<string> tenantIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantIds);
        foreach (var tenantId in tenantIds)
        {
            ArgumentNullException.ThrowIfNull(tenantId, nameof(tenantIds));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var results = _store.Remove(tenantIds);
        shards?.Invalidate(tenantIds);
        return ValueTask.FromResult(results);
    }
}
