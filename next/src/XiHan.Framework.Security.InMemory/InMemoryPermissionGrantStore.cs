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

    /// <summary>
    /// 当前持有授予的主体键数。
    /// </summary>
    /// <remarks>
    /// 键是主体种类、主体 ID 与租户的组合，只计至少还有一条授予的键，不会超过 <see cref="Count"/>。
    /// </remarks>
    internal int SubjectCount
    {
        get
        {
            lock (_gate)
            {
                return _grants.Count;
            }
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 进程内的表，读取不会失败，总是返回成功。
    /// </remarks>
    public ValueTask<Result<IReadOnlyCollection<string>>> GetGrantsAsync(GrantSubject subject, string? tenantId, CancellationToken cancellationToken)
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

            return ValueTask.FromResult(Result.Success<IReadOnlyCollection<string>>(result));
        }
    }

    /// <summary>
    /// 授予。
    /// </summary>
    /// <remarks>
    /// 已存在返回成功且不计数，超出容量返回 <see cref="SecurityErrors.CapacityExceeded"/>，被拒绝的授予不留下任何条目。
    /// </remarks>
    public Result Grant(PermissionGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);
        lock (_gate)
        {
            var key = (grant.Subject.Kind, grant.Subject.Id, grant.TenantId);
            var exists = _grants.TryGetValue(key, out var set);

            // 重复授予幂等成功且不占容量；容量只在真正新增时检查
            if (exists && set!.Contains(grant.Permission))
            {
                return Result.Success();
            }

            // 先查容量再建主体的集合：满了以后新主体的授予被拒绝时，授予表里也不能多出一个空集合
            if (_count >= Capacity)
            {
                return Error.Unavailable(SecurityErrors.CapacityExceeded, SecurityMessages.GrantStoreCapacityExceeded, MessageArguments.Of(("capacity", Capacity)));
            }

            if (!exists)
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                _grants[key] = set;
            }

            set!.Add(grant.Permission);
            _count++;
            return Result.Success();
        }
    }

    /// <summary>
    /// 撤销。
    /// </summary>
    /// <remarks>
    /// 存在返回 <see langword="true"/>；主体在该租户下的最后一条授予被撤销时连同它的键一起移除。
    /// </remarks>
    public bool Revoke(PermissionGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);
        lock (_gate)
        {
            var key = (grant.Subject.Kind, grant.Subject.Id, grant.TenantId);
            if (!_grants.TryGetValue(key, out var set) || !set.Remove(grant.Permission))
            {
                return false;
            }

            _count--;
            // 空集合不留在表里：授予与撤销反复进行的主体（会话、临时委托）不让授予表只增不减
            if (set.Count == 0)
            {
                _grants.Remove(key);
            }

            return true;
        }
    }
}
