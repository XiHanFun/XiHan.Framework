// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Serialization;
using XiHan.Framework.Workflow.Abstractions.Storage;

namespace XiHan.Framework.Workflow.Abstractions.Definitions;

/// <summary>
/// 定义目录的默认实现
/// </summary>
/// <remarks>
/// 生命周期规则在这里，存储只管读写。作用域服务：每个操作在调用方的事务里执行（没有可加入的事务时自己开一次调用），只读写调用方租户的分区——平台调用只看得到、改得动平台的定义，租户只看得到、改得动本租户的。改写已有定义的操作先取定义 ID 的锁再读出、核对与写回，锁由执行范围持有到作用域结束（RFC-0021「目录」，ADR-0326）：调用方事务提交之前，别的调用、定义级定时书签的恢复与数据库实现的查询列回填都改不了这份定义
/// </remarks>
internal sealed class WorkflowDefinitionCatalog : IWorkflowDefinitionCatalog
{
    /// <summary>
    /// 存储
    /// </summary>
    private readonly IWorkflowDefinitionStore _store;

    /// <summary>
    /// 校验
    /// </summary>
    private readonly WorkflowDefinitionValidator _validator;

    /// <summary>
    /// 书签存储（定时启动书签）
    /// </summary>
    private readonly IWorkflowBookmarkStore _bookmarks;

    /// <summary>
    /// 活动登记表
    /// </summary>
    private readonly ActivityRegistry _registry;

    /// <summary>
    /// 时钟
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 执行范围：事务的加入或自开、调用方租户
    /// </summary>
    private readonly WorkflowScope _scope;

    /// <summary>
    /// 创建（作用域服务）
    /// </summary>
    public WorkflowDefinitionCatalog(IWorkflowDefinitionStore store, IWorkflowBookmarkStore bookmarks, ActivityRegistry registry, WorkflowDefinitionValidator validator, TimeProvider clock, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _bookmarks = bookmarks ?? throw new ArgumentNullException(nameof(bookmarks));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _scope = services.GetRequiredService<WorkflowScope>();
    }

    /// <summary>
    /// 调用方的分区：调用的租户，空为平台
    /// </summary>
    private string? Tenant => _scope.TenantId;

    /// <inheritdoc />
    public ValueTask<Result<WorkflowDefinition>> CreateAsync(WorkflowDefinition definition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return _scope.RunAsync<IWorkflowDefinitionCatalog, WorkflowDefinition>(true, "workflow.definition.create", async () =>
        {
            // 定义只能建在调用方自己的分区里：写了别的租户（平台写租户、租户写平台或别的租户）按不存在处理，不替调用方改写
            var tenantId = Tenant;
            if (definition.TenantId is not null && !string.Equals(definition.TenantId, tenantId, StringComparison.Ordinal))
            {
                return Error.NotFound(WorkflowErrors.DefinitionNotFound, WorkflowMessages.DefinitionNotFound, MessageArguments.Of(("name", definition.Name.Value)));
            }

            // 版本号在分区里编号：别的分区的同名定义不占号
            var max = await _store.MaxVersionAsync(tenantId, definition.Name, cancellationToken).ConfigureAwait(false);
            if (!max.TryGetValue(out var version))
            {
                return max.Error!;
            }

            var created = definition with
            {
                Id = WorkflowIdentifiers.NewId(),
                Version = version + 1,
                Status = WorkflowDefinitionStatus.Draft,
                CreatedAt = _clock.GetUtcNow(),
                PublishedAt = null,
                TenantId = tenantId,
            };
            var saved = await _store.SaveAsync(tenantId, [created], cancellationToken).ConfigureAwait(false);
            return saved.IsSuccess ? created : saved.Error!;
        }, catalog => catalog.CreateAsync(definition, cancellationToken), cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<Result<WorkflowDefinition>> UpdateAsync(WorkflowDefinition definition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return _scope.RunAsync<IWorkflowDefinitionCatalog, WorkflowDefinition>(true, "workflow.definition.update", async () =>
        {
            var locking = await LockAsync([definition.Id], cancellationToken).ConfigureAwait(false);
            if (!locking.TryGetValue(out var locked))
            {
                return locking.Error!;
            }

            await using var held = locked.ConfigureAwait(false);
            if (locked.Failures.TryGetValue(definition.Id, out var failure))
            {
                return failure;
            }

            if (!locked.Definitions.TryGetValue(definition.Id, out var stored))
            {
                return NotFound(definition.Id);
            }

            if (stored.Status != WorkflowDefinitionStatus.Draft)
            {
                return StatusConflict(stored);
            }

            var updated = definition with
            {
                Name = stored.Name,
                Version = stored.Version,
                Status = WorkflowDefinitionStatus.Draft,
                CreatedAt = stored.CreatedAt,
                PublishedAt = null,
                TenantId = stored.TenantId,
            };
            var saved = await _store.SaveAsync(Tenant, [updated], cancellationToken).ConfigureAwait(false);
            return saved.IsSuccess ? updated : saved.Error!;
        }, catalog => catalog.UpdateAsync(definition, cancellationToken), cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Result<WorkflowDefinition>>> PublishAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return _scope.RunBatchAsync<IWorkflowDefinitionCatalog, WorkflowDefinition>(ids.Count, "workflow.definition.publish", async () =>
        {
            var locking = await LockAsync(ids, cancellationToken).ConfigureAwait(false);
            if (!locking.TryGetValue(out var locked))
            {
                return ids.Select(_ => Result.Failure<WorkflowDefinition>(locking.Error!)).ToList();
            }

            await using var held = locked.ConfigureAwait(false);
            var results = await TransitionAsync(ids, locked, definition =>
            {
                if (definition.Status != WorkflowDefinitionStatus.Draft)
                {
                    return StatusConflict(definition);
                }

                var validated = _validator.Validate(definition);
                return validated.IsSuccess
                    ? definition with { Status = WorkflowDefinitionStatus.Published, PublishedAt = _clock.GetUtcNow() }
                    : validated.Error!;
            }, cancellationToken).ConfigureAwait(false);

            // 定时启动：发布成功的定义各排一个定义级定时书签（没有 Start.Cron 的没有）
            var timers = new List<WorkflowBookmark>();
            foreach (var result in results)
            {
                if (result.TryGetValue(out var published) && WorkflowEngine.DefinitionTimerBookmark(published, _clock.GetUtcNow()) is { } timer)
                {
                    timers.Add(timer);
                }
            }

            if (timers.Count > 0)
            {
                var saved = await _bookmarks.SaveAsync(Tenant, timers, cancellationToken).ConfigureAwait(false);
                if (saved.IsFailure)
                {
                    return results.Select(result => result.IsSuccess ? Result.Failure<WorkflowDefinition>(saved.Error!) : result).ToList();
                }
            }

            return results;
        }, catalog => catalog.PublishAsync(ids, cancellationToken), cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<Result<WorkflowDefinition>> NewVersionAsync(WorkflowName name, CancellationToken cancellationToken)
        => _scope.RunAsync<IWorkflowDefinitionCatalog, WorkflowDefinition>(true, "workflow.definition.new-version", async () =>
        {
            // 只在调用方的分区里找最新版本，新草稿留在同一分区：不拿别的分区的定义为底
            var tenantId = Tenant;
            var max = await _store.MaxVersionAsync(tenantId, name, cancellationToken).ConfigureAwait(false);
            if (!max.TryGetValue(out var version))
            {
                return max.Error!;
            }

            var found = version == 0 ? Result.Success(Maybe.None<WorkflowDefinition>()) : await _store.FindAsync(tenantId, name, version, cancellationToken).ConfigureAwait(false);
            if (!found.TryGetValue(out var maybe))
            {
                return found.Error!;
            }

            if (!maybe.TryGetValue(out var stored))
            {
                return Error.NotFound(WorkflowErrors.DefinitionNotFound, WorkflowMessages.DefinitionNotFound, MessageArguments.Of(("name", name.Value)));
            }

            var latest = WorkflowJson.Clone(stored);
            var draft = latest with
            {
                Id = WorkflowIdentifiers.NewId(),
                Version = version + 1,
                Status = WorkflowDefinitionStatus.Draft,
                CreatedAt = _clock.GetUtcNow(),
                PublishedAt = null,
            };
            var saved = await _store.SaveAsync(tenantId, [draft], cancellationToken).ConfigureAwait(false);
            return saved.IsSuccess ? draft : saved.Error!;
        }, catalog => catalog.NewVersionAsync(name, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Result<WorkflowDefinition>>> DisableAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return _scope.RunBatchAsync<IWorkflowDefinitionCatalog, WorkflowDefinition>(ids.Count, "workflow.definition.disable", async () =>
        {
            var locking = await LockAsync(ids, cancellationToken).ConfigureAwait(false);
            if (!locking.TryGetValue(out var locked))
            {
                return ids.Select(_ => Result.Failure<WorkflowDefinition>(locking.Error!)).ToList();
            }

            await using var held = locked.ConfigureAwait(false);
            var results = await TransitionAsync(ids, locked, static definition => definition.Status == WorkflowDefinitionStatus.Published
                ? definition with { Status = WorkflowDefinitionStatus.Disabled }
                : StatusConflict(definition), cancellationToken).ConfigureAwait(false);
            return await RemoveTimersAsync(results, cancellationToken).ConfigureAwait(false);
        }, catalog => catalog.DisableAsync(ids, cancellationToken), cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Result<WorkflowDefinition>>> ArchiveAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return _scope.RunBatchAsync<IWorkflowDefinitionCatalog, WorkflowDefinition>(ids.Count, "workflow.definition.archive", async () =>
        {
            var locking = await LockAsync(ids, cancellationToken).ConfigureAwait(false);
            if (!locking.TryGetValue(out var locked))
            {
                return ids.Select(_ => Result.Failure<WorkflowDefinition>(locking.Error!)).ToList();
            }

            await using var held = locked.ConfigureAwait(false);
            var results = await TransitionAsync(ids, locked, static definition => definition with { Status = WorkflowDefinitionStatus.Archived }, cancellationToken).ConfigureAwait(false);
            return await RemoveTimersAsync(results, cancellationToken).ConfigureAwait(false);
        }, catalog => catalog.ArchiveAsync(ids, cancellationToken), cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyList<ActivityDescriptor> ActivityTypes() => _registry.Activities.Select(static activity => activity.Descriptor.Localize()).OrderBy(static descriptor => descriptor.Type, StringComparer.Ordinal).ToList();

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Result>> DeleteAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return _scope.RunBatchAsync<IWorkflowDefinitionCatalog>(ids.Count, "workflow.definition.delete", async () =>
        {
            var locking = await LockAsync(ids, cancellationToken).ConfigureAwait(false);
            if (!locking.TryGetValue(out var locked))
            {
                return ids.Select(_ => Result.Failure(locking.Error!)).ToList();
            }

            await using var held = locked.ConfigureAwait(false);
            var results = new Result[ids.Count];
            var deletable = new List<string>();
            for (var index = 0; index < ids.Count; index++)
            {
                if (locked.Failures.TryGetValue(ids[index], out var failure))
                {
                    results[index] = failure;
                }
                else if (!locked.Definitions.TryGetValue(ids[index], out var definition))
                {
                    results[index] = NotFound(ids[index]);
                }
                else if (definition.Status != WorkflowDefinitionStatus.Draft)
                {
                    results[index] = StatusConflict(definition);
                }
                else
                {
                    deletable.Add(definition.Id);
                    results[index] = Result.Success();
                }
            }

            if (deletable.Count > 0)
            {
                var timers = await _bookmarks.DeleteByInstanceAsync(Tenant, deletable, cancellationToken).ConfigureAwait(false);
                var deleted = timers.IsSuccess ? await _store.DeleteAsync(Tenant, deletable, cancellationToken).ConfigureAwait(false) : timers;
                if (deleted.IsFailure)
                {
                    for (var index = 0; index < ids.Count; index++)
                    {
                        if (results[index].IsSuccess)
                        {
                            results[index] = deleted;
                        }
                    }
                }
            }

            return (IReadOnlyList<Result>)results;
        }, catalog => catalog.DeleteAsync(ids, cancellationToken), cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<Result<Maybe<WorkflowDefinition>>> FindAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        return _scope.RunAsync<IWorkflowDefinitionCatalog, Maybe<WorkflowDefinition>>(false, "workflow.definition.find", () => _store.FindAsync(Tenant, id, cancellationToken), catalog => catalog.FindAsync(id, cancellationToken), cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<Result<Maybe<WorkflowDefinition>>> FindPublishedAsync(WorkflowName name, int? version, CancellationToken cancellationToken)
        => _scope.RunAsync<IWorkflowDefinitionCatalog, Maybe<WorkflowDefinition>>(false, "workflow.definition.find-published", () => WorkflowDefinitionNamespace.FindPublishedAsync(_store, Tenant, name, version, cancellationToken), catalog => catalog.FindPublishedAsync(name, version, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<WorkflowDefinitionPage>> QueryAsync(WorkflowDefinitionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        // 分页越界是调用方可以改正的输入（查询常由请求绑定）：以校验错误返回而不是抛出，也不进存储、不开事务；上限与持久化的查询条数上限一致
        if (query.Skip < 0 || query.Take is < 1 or > WorkflowDefinitionQuery.MaxTake)
        {
            return ValueTask.FromResult<Result<WorkflowDefinitionPage>>(Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.DefinitionQueryPageInvalid, MessageArguments.Of(("skip", query.Skip), ("take", query.Take), ("max", WorkflowDefinitionQuery.MaxTake))));
        }

        // 排序项只能是白名单里不重复的字段、不超过上限（越界来自请求）：同样是可以改正的输入，存储不必再猜怎么排
        if (query.SortsError() is { } sortsError)
        {
            return ValueTask.FromResult<Result<WorkflowDefinitionPage>>(sortsError);
        }

        return _scope.RunAsync<IWorkflowDefinitionCatalog, WorkflowDefinitionPage>(false, "workflow.definition.query", async () =>
        {
            // 这一页与总数在同一次调用里各读一次（数据库实现在同一事务里），条件相同，只是总数不看分页
            var listed = await _store.ListSummariesAsync(Tenant, query, cancellationToken).ConfigureAwait(false);
            if (!listed.TryGetValue(out var items))
            {
                return listed.Error!;
            }

            var counted = await _store.CountAsync(Tenant, query, cancellationToken).ConfigureAwait(false);
            if (!counted.TryGetValue(out var total))
            {
                return counted.Error!;
            }

            return new WorkflowDefinitionPage(items, total);
        }, catalog => catalog.QueryAsync(query, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// 删掉停用或归档成功的定义的定时启动书签
    /// </summary>
    /// <remarks>
    /// 删除失败时成功的项都改报那个错误：调用方据此回滚，定义不会停了用却还在按时启动
    /// </remarks>
    private async ValueTask<IReadOnlyList<Result<WorkflowDefinition>>> RemoveTimersAsync(IReadOnlyList<Result<WorkflowDefinition>> results, CancellationToken cancellationToken)
    {
        var ids = results.Where(static result => result.IsSuccess).Select(static result => result.ValueOrThrow().Id).ToList();
        if (ids.Count == 0)
        {
            return results;
        }

        var removed = await _bookmarks.DeleteByInstanceAsync(Tenant, ids, cancellationToken).ConfigureAwait(false);
        return removed.IsSuccess ? results : results.Select(result => result.IsSuccess ? Result.Failure<WorkflowDefinition>(removed.Error!) : result).ToList();
    }

    /// <summary>
    /// 批量状态迁移：按锁内重读的状态逐项迁移，成功的一次写回
    /// </summary>
    /// <remarks>
    /// <paramref name="transition"/> 给出新状态或错误。拿不到锁的项以取锁的错误失败，看不到的项（不存在或在别的分区）按不存在处理
    /// </remarks>
    private async ValueTask<IReadOnlyList<Result<WorkflowDefinition>>> TransitionAsync(IReadOnlyList<string> ids, LockedDefinitions locked, Func<WorkflowDefinition, Result<WorkflowDefinition>> transition, CancellationToken cancellationToken)
    {
        var results = new Result<WorkflowDefinition>[ids.Count];
        var changed = new List<WorkflowDefinition>();
        for (var index = 0; index < ids.Count; index++)
        {
            if (locked.Failures.TryGetValue(ids[index], out var failure))
            {
                results[index] = failure;
                continue;
            }

            if (!locked.Definitions.TryGetValue(ids[index], out var definition))
            {
                results[index] = NotFound(ids[index]);
                continue;
            }

            results[index] = transition(definition);
            if (results[index].TryGetValue(out var next))
            {
                changed.Add(next);
            }
        }

        if (changed.Count > 0)
        {
            var saved = await _store.SaveAsync(Tenant, changed, cancellationToken).ConfigureAwait(false);
            if (saved.IsFailure)
            {
                for (var index = 0; index < ids.Count; index++)
                {
                    if (results[index].IsSuccess)
                    {
                        results[index] = saved.Error!;
                    }
                }
            }
        }

        return results;
    }

    /// <summary>
    /// 取这些定义的锁，并在锁内重读它们
    /// </summary>
    /// <remarks>
    /// 改写已有的定义之前调用（RFC-0021「目录」，ADR-0326）。先读一次，只为调用方分区里看得到的定义取锁：看不到的 ID 照旧按不存在处理，不为它们等锁或留下租约行。按 ID 的序数次序逐个取、同一 ID 只取一次，两个批量操作不会交叉等对方；拿不到的记进 <see cref="LockedDefinitions.Failures"/>，别的照常。锁由执行范围持有到作用域结束，返回值释放时只把它们标为空闲。读失败时已取得的锁照样标为空闲，返回读的错误
    /// </remarks>
    /// <param name="ids">要改写的定义 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    private async ValueTask<Result<LockedDefinitions>> LockAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        // 存储只返回调用方分区的定义：别的分区的按不存在处理
        var found = await _store.FindManyAsync(Tenant, ids, cancellationToken).ConfigureAwait(false);
        if (!found.TryGetValue(out var visible))
        {
            return found.Error!;
        }

        var locked = new LockedDefinitions();
        try
        {
            var keys = visible.Select(static definition => definition.Id).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
            foreach (var key in keys)
            {
                var lease = await _scope.LockAsync(key, cancellationToken).ConfigureAwait(false);
                if (lease.TryGetValue(out var handle))
                {
                    locked.Leases.Add(handle);
                }
                else
                {
                    locked.Failures[key] = lease.Error!;
                }
            }

            // 锁内重读：第一次读到拿到锁之间别的调用可能已经改过或删掉了它，核对与写回都按锁内读到的
            var held = keys.Where(key => !locked.Failures.ContainsKey(key)).ToList();
            if (held.Count > 0)
            {
                var reread = await _store.FindManyAsync(Tenant, held, cancellationToken).ConfigureAwait(false);
                if (!reread.TryGetValue(out var current))
                {
                    await locked.DisposeAsync().ConfigureAwait(false);
                    return reread.Error!;
                }

                foreach (var definition in current)
                {
                    locked.Definitions[definition.Id] = definition;
                }
            }

            return locked;
        }
        catch
        {
            // 取锁或重读抛出（取消、作用域已结束）：已取得的锁标为空闲，异常原样抛出
            await locked.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// 定义不存在（或不在调用方的分区里）
    /// </summary>
    private static Error NotFound(string id) => Error.NotFound(WorkflowErrors.DefinitionNotFound, WorkflowMessages.DefinitionNotFound, MessageArguments.Of(("name", id)));

    /// <summary>
    /// 定义的状态不允许该操作
    /// </summary>
    private static Error StatusConflict(WorkflowDefinition definition)
        => WorkflowStatusConflicts.Of(definition);

    /// <summary>
    /// 一次改写取得的定义锁与锁内重读到的定义
    /// </summary>
    /// <remarks>
    /// 释放时把这次占用的锁标为空闲：锁本身由执行范围持有到作用域结束（调用方事务提交或回滚之后），同一作用域里之后的操作可以复用
    /// </remarks>
    private sealed class LockedDefinitions : IAsyncDisposable
    {
        /// <summary>
        /// 锁内重读到的定义：ID → 定义
        /// </summary>
        public Dictionary<string, WorkflowDefinition> Definitions { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// 拿不到锁的定义：ID → 取锁的错误
        /// </summary>
        public Dictionary<string, Error> Failures { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// 这次占用的锁
        /// </summary>
        public List<IAsyncDisposable> Leases { get; } = [];

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            foreach (var lease in Leases)
            {
                await lease.DisposeAsync().ConfigureAwait(false);
            }

            Leases.Clear();
        }
    }
}
