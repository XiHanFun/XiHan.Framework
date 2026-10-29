// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Security.Abstractions.Permissions;

namespace XiHan.Framework.Security.InMemory;

/// <summary>
/// 有界的内存权限授予存储。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemoryPermissionGrantStore : IPermissionGrantStore
{
    /// <summary>
    /// 授予表：键为主体种类、主体 ID 与租户（<see langword="null"/> 为全局），值为权限码集合。
    /// </summary>
    private readonly Dictionary<(string Kind, string Id, string? Tenant), HashSet<string>> _grants = [];

    /// <summary>
    /// 保护授予表与计数的锁。
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// 当前授予总数。
    /// </summary>
    /// <remarks>
    /// 用于容量检查。
    /// </remarks>
    private int _count;

    /// <summary>
    /// 创建并装载选项里的授予。
    /// </summary>
    /// <remarks>
    /// 超出容量抛出 <see cref="FrameworkException"/>。
    /// </remarks>
    public InMemoryPermissionGrantStore(InMemorySecurityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Capacity = options.MaxGrants;
        foreach (var grant in options.Grants)
        {
            Grant(grant).ThrowIfFailure();
        }
    }

    /// <summary>
    /// 容量上限。
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// 当前授予数。
    /// </summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _count;
            }
        }
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyCollection<string>> GetGrantsAsync(GrantSubject subject, string? tenantId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            // 全局授予与该租户的授予取并集；没有租户时只有全局授予
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (_grants.TryGetValue((subject.Kind, subject.Id, null), out var global))
            {
                result.UnionWith(global);
            }

            if (tenantId is not null && _grants.TryGetValue((subject.Kind, subject.Id, tenantId), out var scoped))
            {
                result.UnionWith(scoped);
            }

            return ValueTask.FromResult<IReadOnlyCollection<string>>(result);
        }
    }

    /// <summary>
    /// 授予。
    /// </summary>
    /// <remarks>
    /// 已存在返回成功且不计数，超出容量返回 <see cref="SecurityErrors.CapacityExceeded"/>。
    /// </remarks>
    public Result Grant(PermissionGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);
        lock (_gate)
        {
            var key = (grant.Subject.Kind, grant.Subject.Id, grant.TenantId);
            if (!_grants.TryGetValue(key, out var set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                _grants[key] = set;
            }

            // 重复授予幂等成功且不占容量；容量只在真正新增时检查
            if (set.Contains(grant.Permission))
            {
                return Result.Success();
            }

            if (_count >= Capacity)
            {
                return Error.Unavailable(SecurityErrors.CapacityExceeded, SecurityMessages.GrantStoreCapacityExceeded, MessageArguments.Of(("capacity", Capacity)));
            }

            set.Add(grant.Permission);
            _count++;
            return Result.Success();
        }
    }

    /// <summary>
    /// 撤销。
    /// </summary>
    /// <remarks>
    /// 存在返回 <see langword="true"/>。
    /// </remarks>
    public bool Revoke(PermissionGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);
        lock (_gate)
        {
            if (_grants.TryGetValue((grant.Subject.Kind, grant.Subject.Id, grant.TenantId), out var set) && set.Remove(grant.Permission))
            {
                _count--;
                return true;
            }

            return false;
        }
    }
}
