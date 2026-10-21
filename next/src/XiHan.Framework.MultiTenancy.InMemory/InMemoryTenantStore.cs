// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.InMemory;

/// <summary>
/// 有界的内存租户存储。ID 区分大小写。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemoryTenantStore : ITenantStore
{
    private readonly ConcurrentDictionary<string, TenantInfo> _tenants = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    /// <summary>
    /// 创建存储并装载选项里的租户；超出容量抛出 <see cref="FrameworkException"/>。
    /// </summary>
    public InMemoryTenantStore(InMemoryMultiTenancyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Capacity = options.MaxTenants;
        foreach (var tenant in options.Tenants)
        {
            Set(tenant).ThrowIfFailure();
        }
    }

    /// <summary>
    /// 容量上限。
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// 当前租户数。
    /// </summary>
    public int Count => _tenants.Count;

    /// <inheritdoc />
    public ValueTask<Maybe<TenantInfo>> FindAsync(string tenantId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantId);
        return ValueTask.FromResult(_tenants.TryGetValue(tenantId, out var tenant) ? Maybe.Some(tenant) : default);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<TenantInfo>> ListAsync(CancellationToken cancellationToken)
        => ValueTask.FromResult<IReadOnlyList<TenantInfo>>(_tenants.Values.OrderBy(static t => t.Id, StringComparer.Ordinal).ToList());

    /// <summary>
    /// 新增或替换；新增超出容量返回 <see cref="MultiTenancyErrors.CapacityExceeded"/>。
    /// </summary>
    public Result Set(TenantInfo tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        lock (_gate)
        {
            if (!_tenants.ContainsKey(tenant.Id) && _tenants.Count >= Capacity)
            {
                return Error.Unavailable(MultiTenancyErrors.CapacityExceeded, $"内存租户存储已达容量上限 {Capacity}。");
            }

            _tenants[tenant.Id] = tenant;
            return Result.Success();
        }
    }

    /// <summary>
    /// 删除；存在返回 <see langword="true"/>。
    /// </summary>
    public bool Remove(string tenantId)
    {
        ArgumentNullException.ThrowIfNull(tenantId);
        return _tenants.TryRemove(tenantId, out _);
    }
}
