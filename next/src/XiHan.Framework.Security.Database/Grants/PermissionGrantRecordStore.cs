// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Specifications;
using XiHan.Framework.Persistence.Abstractions.Identifiers;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Security.Abstractions.Permissions;

namespace XiHan.Framework.Security.Database.Grants;

/// <summary>
/// 授予的批量读写。
/// </summary>
/// <remarks>
/// 作用域服务，须在调用的 Transaction 阶段内使用，走调用方的工作单元。一次查询取回已有行，再用仓储的批量方法一次写入，写入后让 <see cref="DatabasePermissionGrantStore"/> 的缓存按主体失效。写入在调用方的事务提交后才对别的调用可见；失效与提交之间的窗口由缓存的存活时间收敛。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class PermissionGrantRecordStore
{
    /// <summary>
    /// 仓储。
    /// </summary>
    private readonly IRepository<PermissionGrantRecord, Guid> _repository;

    /// <summary>
    /// ID 生成器。
    /// </summary>
    private readonly IIdGenerator<Guid> _ids;

    /// <summary>
    /// 单例授予存储。
    /// </summary>
    /// <remarks>
    /// 写入后按主体失效其缓存。
    /// </remarks>
    private readonly DatabasePermissionGrantStore _grants;

    /// <summary>
    /// 创建。
    /// </summary>
    public PermissionGrantRecordStore(IRepository<PermissionGrantRecord, Guid> repository, IIdGenerator<Guid> ids, DatabasePermissionGrantStore grants)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _ids = ids ?? throw new ArgumentNullException(nameof(ids));
        _grants = grants ?? throw new ArgumentNullException(nameof(grants));
    }

    /// <summary>
    /// 授予，按顺序逐项返回。
    /// </summary>
    /// <remarks>
    /// 已存在的返回成功且不重复写，同一批里重复的项同样成功（只写一行，结果相同）。租户专属授予的 <see cref="PermissionGrant.TenantId"/> 不能是空白（全局用 <see langword="null"/>），否则是编程错误。
    /// </remarks>
    public async ValueTask<IReadOnlyList<PermissionGrantResult>> GrantAsync(IReadOnlyList<PermissionGrant> grants, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(grants);
        if (grants.Count == 0)
        {
            return [];
        }

        foreach (var grant in grants)
        {
            ValidateTenant(grant);
        }

        var existing = await GrantRows.FindManyAsync(_repository, grants, cancellationToken).ConfigureAwait(false);
        // 已有的项不写、记为成功（槽位 -1）；一批里第一次出现且没有的项新增一行，之后重复出现的指向同一行的结果
        var additions = new List<PermissionGrantRecord>();
        var slotOf = new Dictionary<PermissionGrant, int>();
        var slots = new int[grants.Count];
        for (var index = 0; index < grants.Count; index++)
        {
            var grant = grants[index];
            if (existing.ContainsKey(grant))
            {
                slots[index] = -1;
                continue;
            }

            if (!slotOf.TryGetValue(grant, out var slot))
            {
                additions.Add(new PermissionGrantRecord
                {
                    Id = _ids.NewId(),
                    SubjectKind = grant.Subject.Kind,
                    SubjectId = grant.Subject.Id,
                    Permission = grant.Permission,
                    TenantId = PermissionGrantRecord.TenantColumnOf(grant.TenantId),
                });
                slot = additions.Count - 1;
                slotOf[grant] = slot;
            }

            slots[index] = slot;
        }

        var added = additions.Count == 0 ? [] : await _repository.AddRangeAsync(additions, cancellationToken).ConfigureAwait(false);
        InvalidateSubjects(additions);
        var results = new PermissionGrantResult[grants.Count];
        for (var index = 0; index < grants.Count; index++)
        {
            results[index] = new PermissionGrantResult(grants[index], slots[index] < 0 ? Result.Success() : added[slots[index]]);
        }

        return results;
    }

    /// <summary>
    /// 撤销，按顺序逐项返回：不存在 → <see cref="SecurityErrors.GrantNotFound"/>，同一批里同一授予第二次同样不存在。
    /// </summary>
    public async ValueTask<IReadOnlyList<PermissionGrantResult>> RevokeAsync(IReadOnlyList<PermissionGrant> grants, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(grants);
        if (grants.Count == 0)
        {
            return [];
        }

        foreach (var grant in grants)
        {
            ValidateTenant(grant);
        }

        var existing = await GrantRows.FindManyAsync(_repository, grants, cancellationToken).ConfigureAwait(false);
        var removals = new List<PermissionGrantRecord>();
        var slots = new int[grants.Count];
        var seen = new HashSet<PermissionGrant>();
        for (var index = 0; index < grants.Count; index++)
        {
            // 第一次出现且存在的授予进入批量删除；重复出现或不存在的直接记为 NotFound
            if (seen.Add(grants[index]) && existing.TryGetValue(grants[index], out var row))
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
        InvalidateSubjects(removals);
        var results = new PermissionGrantResult[grants.Count];
        for (var index = 0; index < grants.Count; index++)
        {
            var grant = grants[index];
            Result outcome = slots[index] >= 0
                ? removed[slots[index]]
                : Error.NotFound(SecurityErrors.GrantNotFound, SecurityMessages.GrantNotFound, MessageArguments.Of(("subject", grant.Subject.ToString()), ("permission", grant.Permission), ("tenant", grant.TenantId ?? PermissionGrant.All)));
            results[index] = new PermissionGrantResult(grant, outcome);
        }

        return results;
    }

    /// <summary>
    /// 列出授予行。
    /// </summary>
    /// <remarks>
    /// <paramref name="subject"/> 为 <see langword="null"/> 不按主体过滤，<paramref name="tenantId"/> 为 <see langword="null"/> 不按租户过滤（否则只列该租户专属的行）；按主体种类、主体 ID、权限码、租户排序，最多 <see cref="EntityQuery.DefaultMaxTake"/> 条一页。
    /// </remarks>
    public Task<IReadOnlyList<PermissionGrantRecord>> ListAsync(GrantSubject? subject, string? tenantId, int skip, int take, CancellationToken cancellationToken)
    {
        var query = EntityQuery.All<PermissionGrantRecord>()
            .OrderBy(static r => r.SubjectKind)
            .OrderBy(static r => r.SubjectId)
            .OrderBy(static r => r.Permission)
            .OrderBy(static r => r.TenantId)
            .Page(skip, take);
        if (subject is { } target)
        {
            var kind = target.Kind;
            var id = target.Id;
            query = query.Where(new Specification<PermissionGrantRecord>(r => r.SubjectKind == kind && r.SubjectId == id));
        }

        if (tenantId is not null)
        {
            var tenant = tenantId;
            query = query.Where(new Specification<PermissionGrantRecord>(r => r.TenantId == tenant));
        }

        return _repository.ListAsync(query, cancellationToken);
    }

    /// <summary>
    /// 租户专属授予的租户不能是空白：空字符串是全局行在列里的表示，契约里全局用 <see langword="null"/>。
    /// </summary>
    private static void ValidateTenant(PermissionGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);
        if (grant.TenantId is not null && string.IsNullOrWhiteSpace(grant.TenantId))
        {
            throw new ArgumentException($"The tenant of a grant must not be blank; use null for a global grant ('{grant.Subject}', '{grant.Permission}').", nameof(grant));
        }
    }

    /// <summary>
    /// 让写入涉及的每个主体的缓存失效（每个主体一次）。
    /// </summary>
    private void InvalidateSubjects(List<PermissionGrantRecord> rows)
    {
        var subjects = new HashSet<GrantSubject>();
        foreach (var row in rows)
        {
            var subject = new GrantSubject(row.SubjectKind, row.SubjectId);
            if (subjects.Add(subject))
            {
                _grants.Invalidate(subject);
            }
        }
    }
}
