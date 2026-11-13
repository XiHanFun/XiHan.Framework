// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.InMemory;

/// <summary>
/// 有界的内存租户存储
/// </summary>
/// <remarks>
/// ID 区分大小写。同时是健康项 <see cref="InMemoryMultiTenancyFeature.HealthName"/> 的贡献者：租户数达到容量即 Degraded
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class InMemoryTenantStore : ITenantStore, IHealthContributor
{
    /// <summary>
    /// 按 ID 索引的租户
    /// </summary>
    /// <remarks>
    /// 读取无锁
    /// </remarks>
    private readonly ConcurrentDictionary<string, TenantInfo> _tenants = new(StringComparer.Ordinal);

    /// <summary>
    /// 串行化新增与替换，使容量检查与写入成为一个原子步骤
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// 已删除的租户 ID（墓碑）
    /// </summary>
    /// <remarks>
    /// 只在 <see cref="_gate"/> 内读写。不能再新建，计入容量，所以有界（ADR-0225）
    /// </remarks>
    private readonly HashSet<string> _retired = new(StringComparer.Ordinal);

    /// <summary>
    /// 创建存储并装载选项里的租户
    /// </summary>
    /// <remarks>
    /// 超出容量抛出 <see cref="FrameworkException"/>
    /// </remarks>
    public InMemoryTenantStore(InMemoryMultiTenancyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Capacity = options.MaxTenants;
        foreach (var seeded in Upsert(options.Tenants))
        {
            seeded.ThrowIfFailure();
        }
    }

    /// <summary>
    /// 容量上限
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// 当前租户数
    /// </summary>
    /// <remarks>
    /// 不含墓碑
    /// </remarks>
    public int Count => _tenants.Count;

    /// <summary>
    /// 已占用的容量：租户加墓碑
    /// </summary>
    private int Occupied
    {
        get
        {
            lock (_gate)
            {
                return _tenants.Count + _retired.Count;
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ContributorStatus> Check()
    {
        // 租户数达到容量即 Degraded：再登记新租户会被拒绝
        return
        [
            Occupied >= Capacity
                ? new ContributorStatus(InMemoryMultiTenancyFeature.HealthName, LifecycleState.Degraded)
                {
                    Error = Error.Unavailable(MultiTenancyErrors.CapacityExceeded, MultiTenancyMessages.CapacityExceeded, MessageArguments.Of(("capacity", Capacity))),
                }
                : new ContributorStatus(InMemoryMultiTenancyFeature.HealthName, LifecycleState.Ready),
        ];
    }

    /// <inheritdoc />
    /// <remarks>
    /// 进程内的表，读取不会失败
    /// </remarks>
    public ValueTask<Result<Maybe<TenantInfo>>> FindAsync(string tenantId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantId);
        return ValueTask.FromResult(Result.Success(_tenants.TryGetValue(tenantId, out var tenant) ? Maybe.Some(tenant) : default));
    }

    /// <inheritdoc />
    /// <remarks>
    /// 续页令牌是上一页最后一个租户的 ID：从它之后（序数）接着列，期间增删的租户不会让后面的页重复或遗漏已有的租户
    /// </remarks>
    public ValueTask<Result<TenantPage>> ListAsync(TenantQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var valid = query.Validate();
        if (valid.IsFailure)
        {
            return ValueTask.FromResult(Result.Failure<TenantPage>(valid.Error!));
        }

        if (query.ContinuationToken is { } token && !TenantIdentifier.IsValid(token))
        {
            return ValueTask.FromResult(Result.Failure<TenantPage>(Error.Validation(MultiTenancyErrors.QueryInvalid, MultiTenancyMessages.QueryTokenInvalid)));
        }

        // 多取一条判断后面还有没有：恰好读完时不给令牌，调用方少一次空页往返
        var items = _tenants.Values
            .Where(t => (query.ContinuationToken is null || string.CompareOrdinal(t.Id, query.ContinuationToken) > 0) && (!query.EnabledOnly || t.IsEnabled))
            .OrderBy(static t => t.Id, StringComparer.Ordinal)
            .Take(query.PageSize + 1)
            .ToList();
        var more = items.Count > query.PageSize;
        if (more)
        {
            items.RemoveAt(items.Count - 1);
        }

        return ValueTask.FromResult(Result.Success(new TenantPage { Items = items, ContinuationToken = more ? items[^1].Id : null }));
    }

    /// <summary>
    /// 新增或替换一批，按顺序逐项返回
    /// </summary>
    /// <remarks>
    /// 新增超出容量返回 <see cref="MultiTenancyErrors.CapacityExceeded"/>，删除过的 ID 返回 <see cref="MultiTenancyErrors.IdRetired"/>；经 <see cref="ITenantAdministration"/> 调用
    /// </remarks>
    internal IReadOnlyList<Result> Upsert(IReadOnlyList<TenantInfo> tenants)
    {
        var results = new Result[tenants.Count];
        // 一次加锁处理整批：容量检查与写入必须在同一把锁内，否则并发新增可能越过上限；替换已有租户不占新容量
        lock (_gate)
        {
            for (var index = 0; index < tenants.Count; index++)
            {
                var tenant = tenants[index];
                // 墓碑：按租户 ID 归属的数据不随删除清理，复用这个 ID 的新租户会接手它们（ADR-0225）
                if (_retired.Contains(tenant.Id))
                {
                    results[index] = Error.Conflict(MultiTenancyErrors.IdRetired, MultiTenancyMessages.TenantIdRetired, MessageArguments.Of(("tenantId", tenant.Id)));
                    continue;
                }

                // 墓碑同样占容量：删了再建不能绕过上限
                if (!_tenants.ContainsKey(tenant.Id) && _tenants.Count + _retired.Count >= Capacity)
                {
                    results[index] = Error.Unavailable(MultiTenancyErrors.CapacityExceeded, MultiTenancyMessages.CapacityExceeded, MessageArguments.Of(("capacity", Capacity)));
                    continue;
                }

                _tenants[tenant.Id] = tenant;
                results[index] = Result.Success();
            }
        }

        return results;
    }

    /// <summary>
    /// 删除一批，按顺序逐项返回
    /// </summary>
    /// <remarks>
    /// 不存在返回 <see cref="MultiTenancyErrors.NotFound"/>；删除的 ID 留作墓碑。经 <see cref="ITenantAdministration"/> 调用
    /// </remarks>
    internal IReadOnlyList<Result> Remove(IReadOnlyList<string> tenantIds)
    {
        var results = new Result[tenantIds.Count];
        lock (_gate)
        {
            for (var index = 0; index < tenantIds.Count; index++)
            {
                if (_tenants.TryRemove(tenantIds[index], out _))
                {
                    _retired.Add(tenantIds[index]);
                    results[index] = Result.Success();
                }
                else
                {
                    results[index] = Error.NotFound(MultiTenancyErrors.NotFound, MultiTenancyMessages.TenantNotFound, MessageArguments.Of(("tenantId", tenantIds[index])));
                }
            }
        }

        return results;
    }
}
