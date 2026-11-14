// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Caching.Abstractions;
using XiHan.Framework.Domain.Specifications;
using XiHan.Framework.Persistence.Abstractions.Identifiers;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Security.Abstractions.Permissions;

namespace XiHan.Framework.Security.Database.Grants;

/// <summary>
/// 授予的批量读写
/// </summary>
/// <remarks>
/// 作用域服务，须在调用的 Transaction 阶段内使用，走调用方的工作单元。一次查询取回已有行，再用仓储的批量方法一次写入，写入后让 <see cref="DatabasePermissionGrantStore"/> 的缓存按主体失效（所有共用缓存的实例，ADR-0229）。
/// 写入在调用方的事务提交后才对别的调用可见，所以失效两次（ADR-0194）：写入时一次，作用域释放时（调用已提交或回滚）再一次。缓存按代数丢弃失效之前开始的读取，提交之前读回的旧行不会留在缓存里。
/// 写入时的失效遇到缓存后端故障就抛出（携带 <c>CACHING_UNAVAILABLE_001</c>），调用失败、事务回滚：撤销传不到别的实例时不报成功。释放时的失效遇到故障只记错误日志，已提交的写入在 <see cref="DatabaseSecurityOptions.CacheTimeToLive"/> 内对所有实例生效
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class PermissionGrantRecordStore : IPermissionGrantAdministration, IAsyncDisposable, IDisposable
{
    /// <summary>
    /// 本作用域里写过的主体：作用域释放时（提交或回滚之后）再失效一次
    /// </summary>
    private readonly HashSet<GrantSubject> _written = [];

    /// <summary>
    /// 仓储
    /// </summary>
    private readonly IRepository<PermissionGrantRecord, Guid> _repository;

    /// <summary>
    /// ID 生成器
    /// </summary>
    private readonly IIdGenerator<Guid> _ids;

    /// <summary>
    /// 单例授予存储
    /// </summary>
    /// <remarks>
    /// 写入后按主体失效其缓存
    /// </remarks>
    private readonly DatabasePermissionGrantStore _grants;

    /// <summary>
    /// 日志
    /// </summary>
    /// <remarks>
    /// 记释放时失效失败
    /// </remarks>
    private readonly ILogger _logger;

    /// <summary>
    /// 创建
    /// </summary>
    public PermissionGrantRecordStore(IRepository<PermissionGrantRecord, Guid> repository, IIdGenerator<Guid> ids, DatabasePermissionGrantStore grants, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _ids = ids ?? throw new ArgumentNullException(nameof(ids));
        _grants = grants ?? throw new ArgumentNullException(nameof(grants));
        _logger = loggerFactory.CreateLogger<PermissionGrantRecordStore>();
    }

    /// <inheritdoc />
    /// <remarks>
    /// 已存在的返回成功且不重复写，同一批里重复的项同样成功（只写一行，结果相同）。租户专属授予的 <see cref="PermissionGrant.TenantId"/> 不能是空白（全局用 <see langword="null"/>），否则是编程错误
    /// </remarks>
    public async ValueTask<IReadOnlyList<Result>> GrantAsync(IReadOnlyList<PermissionGrant> grants, CancellationToken cancellationToken)
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
        await InvalidateSubjectsAsync(additions, cancellationToken).ConfigureAwait(false);
        var results = new Result[grants.Count];
        for (var index = 0; index < grants.Count; index++)
        {
            results[index] = slots[index] < 0 ? Result.Success() : added[slots[index]];
        }

        return results;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<Result>> RevokeAsync(IReadOnlyList<PermissionGrant> grants, CancellationToken cancellationToken)
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
        await InvalidateSubjectsAsync(removals, cancellationToken).ConfigureAwait(false);
        var results = new Result[grants.Count];
        for (var index = 0; index < grants.Count; index++)
        {
            var grant = grants[index];
            Result outcome = slots[index] >= 0
                ? removed[slots[index]]
                : (grant.TenantId is { } scope
                    ? Error.NotFound(SecurityErrors.GrantNotFound, SecurityMessages.GrantNotFound, MessageArguments.Of(("subject", grant.Subject.ToString()), ("permission", grant.Permission), ("tenant", scope)))
                    : Error.NotFound(SecurityErrors.GrantNotFound, SecurityMessages.PlatformGrantNotFound, MessageArguments.Of(("subject", grant.Subject.ToString()), ("permission", grant.Permission))));
            results[index] = outcome;
        }

        return results;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 过滤、排序、分页与计数都在数据库里执行。平台授予在列里是空字符串、每个租户的授予是 <c>*</c>，所以「只要平台」按空字符串查，序数顺序里平台在前、每个租户其次（ADR-0197）
    /// </remarks>
    public async ValueTask<Result<PermissionGrantPage>> ListAsync(PermissionGrantQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Validate(PermissionGrantQuery.Revision) is { IsFailure: true } invalid)
        {
            return invalid.Error!;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var filter = EntityQuery.All<PermissionGrantRecord>();
        if (query.Subject is { } target)
        {
            var kind = target.Kind;
            var id = target.Id;
            filter = filter.Where(new Specification<PermissionGrantRecord>(r => r.SubjectKind == kind && r.SubjectId == id));
        }

        if (!query.Tenant.IsAny)
        {
            // 契约的 null 范围是平台，落库为空字符串（PermissionGrantRecord.PlatformTenantId）
            var tenant = PermissionGrantRecord.TenantColumnOf(query.Tenant.TenantId);
            filter = filter.Where(new Specification<PermissionGrantRecord>(r => r.TenantId == tenant));
        }

        var total = await _repository.CountAsync(filter, cancellationToken).ConfigureAwait(false);
        var rows = await _repository.ListAsync(
            filter
                .OrderBy(static r => r.SubjectKind)
                .OrderBy(static r => r.SubjectId)
                .OrderBy(static r => r.Permission)
                .OrderBy(static r => r.TenantId)
                .Page(query.Skip, query.Take),
            cancellationToken).ConfigureAwait(false);
        return new PermissionGrantPage { Items = [.. rows.Select(static r => r.ToGrant())], Total = total };
    }

    /// <summary>
    /// 租户专属授予的租户不能是空白：空字符串是全局行在列里的表示，契约里全局用 <see langword="null"/>
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
    /// 让写入涉及的每个主体的缓存失效（每个主体一次）
    /// </summary>
    /// <remarks>
    /// 缓存后端故障时抛出：写入还没提交，调用失败即回滚
    /// </remarks>
    private ValueTask InvalidateSubjectsAsync(List<PermissionGrantRecord> rows, CancellationToken cancellationToken)
    {
        var subjects = new HashSet<GrantSubject>();
        foreach (var row in rows)
        {
            subjects.Add(new GrantSubject(row.SubjectKind, row.SubjectId));
        }

        // 先记下再失效：失效抛出时调用已失败，释放时的第二次失效对回滚无害
        _written.UnionWith(subjects);
        return _grants.InvalidateAsync([.. subjects], cancellationToken);
    }

    /// <summary>
    /// 作用域释放：再让写过的主体失效一次
    /// </summary>
    /// <remarks>
    /// 管道里的调用先提交、后释放作用域：此后的读取看到的是提交之后的行。可以重复调用（经接口与具体类型各解析一次时容器会释放两次）。缓存后端故障只记错误日志、不抛出：写入已经提交，作用域释放不能失败
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        if (_written.Count == 0)
        {
            return;
        }

        GrantSubject[] written = [.. _written];
        _written.Clear();
        try
        {
            // 释放时没有调用方的取消令牌，也不应被取消：这一次失效决定已提交的写入多快对别的实例可见
            await _grants.InvalidateAsync(written, CancellationToken.None).ConfigureAwait(false);
        }
        catch (FrameworkException exception) when (exception.Code == CachingErrors.StoreUnavailable)
        {
            LogInvalidationFailed(_logger, written.Length, exception);
        }
    }

    /// <summary>
    /// 同步释放：等待 <see cref="DisposeAsync"/> 完成
    /// </summary>
    /// <remarks>
    /// 容器同步释放作用域时走这里；内存缓存同步完成，跨进程缓存会阻塞到失效写完，应优先异步释放作用域
    /// </remarks>
    public void Dispose()
    {
        var pending = DisposeAsync();
        if (!pending.IsCompletedSuccessfully)
        {
            pending.AsTask().GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// 释放时失效失败
    /// </summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Invalidating the cached grants of {Count} subjects after the write failed because the cache is unavailable; other instances see the change once their cached entries expire.")]
    private static partial void LogInvalidationFailed(ILogger logger, int count, Exception exception);
}
