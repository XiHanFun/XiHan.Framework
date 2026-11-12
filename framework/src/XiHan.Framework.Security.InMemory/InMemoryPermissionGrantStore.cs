// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Security.Abstractions.Permissions;

namespace XiHan.Framework.Security.InMemory;

/// <summary>
/// 有界的内存权限授予存储
/// </summary>
/// <remarks>
/// 同时实现授予管理 <see cref="IPermissionGrantAdministration"/>：写入立即对判定可见
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemoryPermissionGrantStore : IPermissionGrantStore, IPermissionGrantAdministration
{
    /// <summary>
    /// 授予表：键为主体种类、主体 ID 与范围，值为权限码集合
    /// </summary>
    /// <remarks>
    /// 范围 <see langword="null"/> 为平台，<c>*</c> 为每个租户，其余为租户 ID
    /// </remarks>
    private readonly Dictionary<(string Kind, string Id, string? Tenant), HashSet<string>> _grants = [];

    /// <summary>
    /// 保护授予表与计数的锁
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// 当前授予总数
    /// </summary>
    /// <remarks>
    /// 用于容量检查
    /// </remarks>
    private int _count;

    /// <summary>
    /// 创建并装载选项里的授予
    /// </summary>
    /// <remarks>
    /// 超出容量抛出 <see cref="FrameworkException"/>
    /// </remarks>
    public InMemoryPermissionGrantStore(InMemorySecurityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Capacity = options.MaxGrants;
        lock (_gate)
        {
            foreach (var grant in options.Grants)
            {
                GrantLocked(grant).ThrowIfFailure();
            }
        }
    }

    /// <summary>
    /// 容量上限
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// 当前授予数
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
    /// 当前持有授予的主体键数
    /// </summary>
    /// <remarks>
    /// 键是主体种类、主体 ID 与租户的组合，只计至少还有一条授予的键，不会超过 <see cref="Count"/>
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
    /// 进程内的表，读取不会失败，总是返回成功
    /// </remarks>
    public ValueTask<Result<IReadOnlyList<IReadOnlyCollection<string>>>> GetManyAsync(IReadOnlyList<GrantSubject> subjects, string? tenantId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subjects);
        var scopes = PermissionGrant.VisibleScopes(tenantId);
        var results = new IReadOnlyCollection<string>[subjects.Count];
        // 一次加锁读完整批：同一批里各主体看到的是同一时刻的表
        lock (_gate)
        {
            for (var i = 0; i < subjects.Count; i++)
            {
                // 这次调用看得到的范围取并集：平台调用只有平台授予，租户调用是每个租户的授予加该租户的授予（ADR-0197）
                var subject = subjects[i];
                var result = new HashSet<string>(StringComparer.Ordinal);
                foreach (var scope in scopes)
                {
                    if (_grants.TryGetValue((subject.Kind, subject.Id, scope), out var granted))
                    {
                        result.UnionWith(granted);
                    }
                }

                results[i] = result;
            }
        }

        return ValueTask.FromResult(Result.Success<IReadOnlyList<IReadOnlyCollection<string>>>(results));
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Result>> GrantAsync(IReadOnlyList<PermissionGrant> grants, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(grants);
        Validate(grants);
        cancellationToken.ThrowIfCancellationRequested();
        var results = new Result[grants.Count];
        // 一次加锁处理整批（批量优先第 2 条）：逐项施加，后一项看见前一项的效果
        lock (_gate)
        {
            for (var index = 0; index < grants.Count; index++)
            {
                results[index] = GrantLocked(grants[index]);
            }
        }

        return ValueTask.FromResult<IReadOnlyList<Result>>(results);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Result>> RevokeAsync(IReadOnlyList<PermissionGrant> grants, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(grants);
        Validate(grants);
        cancellationToken.ThrowIfCancellationRequested();
        var results = new Result[grants.Count];
        // 一次加锁处理整批：同一批里同一授予第二次撤销看见第一次的效果，报告不存在
        lock (_gate)
        {
            for (var index = 0; index < grants.Count; index++)
            {
                var grant = grants[index];
                results[index] = RevokeLocked(grant)
                    ? Result.Success()
                    : (grant.TenantId is { } scope
                    ? Error.NotFound(SecurityErrors.GrantNotFound, SecurityMessages.GrantNotFound, MessageArguments.Of(("subject", grant.Subject.ToString()), ("permission", grant.Permission), ("tenant", scope)))
                    : Error.NotFound(SecurityErrors.GrantNotFound, SecurityMessages.PlatformGrantNotFound, MessageArguments.Of(("subject", grant.Subject.ToString()), ("permission", grant.Permission))));
            }
        }

        return ValueTask.FromResult<IReadOnlyList<Result>>(results);
    }

    /// <inheritdoc />
    public ValueTask<Result<PermissionGrantPage>> ListAsync(PermissionGrantQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Validate() is { IsFailure: true } invalid)
        {
            return ValueTask.FromResult(Result.Failure<PermissionGrantPage>(invalid.Error!));
        }

        cancellationToken.ThrowIfCancellationRequested();
        List<PermissionGrant> matched;
        lock (_gate)
        {
            // 锁内只做筛选与展开，排序与分页在锁外：表可能很大，持锁时间只与匹配数成正比
            matched = [];
            foreach (var (key, permissions) in _grants)
            {
                if (query.Subject is { } subject && (!string.Equals(subject.Kind, key.Kind, StringComparison.Ordinal) || !string.Equals(subject.Id, key.Id, StringComparison.Ordinal)))
                {
                    continue;
                }

                if (!query.Tenant.Matches(key.Tenant))
                {
                    continue;
                }

                foreach (var permission in permissions)
                {
                    matched.Add(new PermissionGrant(new GrantSubject(key.Kind, key.Id), permission, key.Tenant));
                }
            }
        }

        // 序数顺序与数据库实现一致：种类、主体 ID、权限码，平台（null）在前、每个租户（*）其次，然后按租户 ID
        var page = matched
            .OrderBy(static g => g.Subject.Kind, StringComparer.Ordinal)
            .ThenBy(static g => g.Subject.Id, StringComparer.Ordinal)
            .ThenBy(static g => g.Permission, StringComparer.Ordinal)
            .ThenBy(static g => g.TenantId ?? string.Empty, StringComparer.Ordinal)
            .Skip(query.Skip)
            .Take(query.Take)
            .ToList();
        return ValueTask.FromResult(Result.Success(new PermissionGrantPage { Items = page, Total = matched.Count }));
    }

    /// <summary>
    /// 授予一条，调用方持有锁
    /// </summary>
    /// <remarks>
    /// 已存在返回成功且不计数，超出容量返回 <see cref="SecurityErrors.CapacityExceeded"/>，被拒绝的授予不留下任何条目
    /// </remarks>
    private Result GrantLocked(PermissionGrant grant)
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

    /// <summary>
    /// 撤销一条，调用方持有锁
    /// </summary>
    /// <remarks>
    /// 存在返回 <see langword="true"/>；主体在该租户下的最后一条授予被撤销时连同它的键一起移除
    /// </remarks>
    private bool RevokeLocked(PermissionGrant grant)
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

    /// <summary>
    /// 批量写入前逐项核对：空项与空白租户是编程错误，在改动任何条目之前抛出
    /// </summary>
    private static void Validate(IReadOnlyList<PermissionGrant> grants)
    {
        foreach (var grant in grants)
        {
            ArgumentNullException.ThrowIfNull(grant, nameof(grants));
            if (grant.TenantId is not null && string.IsNullOrWhiteSpace(grant.TenantId))
            {
                throw new ArgumentException($"The tenant of a grant must not be blank; use null for a platform grant ('{grant.Subject}', '{grant.Permission}').", nameof(grants));
            }
        }
    }
}
