// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Serialization;
using XiHan.Framework.Workflow.Abstractions.Storage;

namespace XiHan.Framework.Workflow.Abstractions.Definitions;

/// <summary>
/// 定义目录的默认实现。
/// </summary>
/// <remarks>
/// 生命周期规则在这里，存储只管读写。作用域服务。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class WorkflowDefinitionCatalog : IWorkflowDefinitionCatalog
{
    /// <summary>
    /// 存储。
    /// </summary>
    private readonly IWorkflowDefinitionStore _store;

    /// <summary>
    /// 校验。
    /// </summary>
    private readonly WorkflowDefinitionValidator _validator;

    /// <summary>
    /// 书签存储：定时启动的书签。
    /// </summary>
    private readonly IWorkflowBookmarkStore _bookmarks;

    /// <summary>
    /// 活动登记表：描述符。
    /// </summary>
    private readonly ActivityRegistry _registry;

    /// <summary>
    /// 时钟。
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 本次调用的上下文访问器。
    /// </summary>
    private readonly IInvocationAccessor? _invocation;

    /// <summary>
    /// 创建。
    /// </summary>
    public WorkflowDefinitionCatalog(IWorkflowDefinitionStore store, IWorkflowBookmarkStore bookmarks, ActivityRegistry registry, WorkflowDefinitionValidator validator, TimeProvider clock, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _bookmarks = bookmarks ?? throw new ArgumentNullException(nameof(bookmarks));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _invocation = services.GetService<IInvocationAccessor>();
    }

    /// <inheritdoc />
    public async ValueTask<Result<WorkflowDefinition>> CreateAsync(WorkflowDefinition definition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        // 版本号在定义所属租户的名字空间里编号：别的租户的同名定义不占号
        var tenantId = definition.TenantId ?? CurrentTenantId();
        var max = await _store.MaxVersionAsync(definition.Name, tenantId, cancellationToken).ConfigureAwait(false);
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
        var saved = await _store.SaveAsync([created], cancellationToken).ConfigureAwait(false);
        return saved.IsSuccess ? created : saved.Error!;
    }

    /// <inheritdoc />
    public async ValueTask<Result<WorkflowDefinition>> UpdateAsync(WorkflowDefinition definition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var found = await _store.FindAsync(definition.Id, cancellationToken).ConfigureAwait(false);
        if (!found.TryGetValue(out var maybe))
        {
            return found.Error!;
        }

        if (!maybe.TryGetValue(out var stored) || !WorkflowDefinitionNamespace.IsWritable(stored, CurrentTenantId()))
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
        var saved = await _store.SaveAsync([updated], cancellationToken).ConfigureAwait(false);
        return saved.IsSuccess ? updated : saved.Error!;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<Result<WorkflowDefinition>>> PublishAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        var results = await TransitionAsync(ids, definition =>
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
            var saved = await _bookmarks.SaveAsync(timers, cancellationToken).ConfigureAwait(false);
            if (saved.IsFailure)
            {
                return results.Select(result => result.IsSuccess ? Result.Failure<WorkflowDefinition>(saved.Error!) : result).ToList();
            }
        }

        return results;
    }

    /// <inheritdoc />
    public async ValueTask<Result<WorkflowDefinition>> NewVersionAsync(WorkflowName name, CancellationToken cancellationToken)
    {
        // 只在当前租户的名字空间里找最新版本，新草稿留在同一名字空间：不拿别的租户的定义为底
        var tenantId = CurrentTenantId();
        var max = await _store.MaxVersionAsync(name, tenantId, cancellationToken).ConfigureAwait(false);
        if (!max.TryGetValue(out var version))
        {
            return max.Error!;
        }

        var found = version == 0 ? Result.Success(Maybe.None<WorkflowDefinition>()) : await _store.FindAsync(name, version, tenantId, cancellationToken).ConfigureAwait(false);
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
        var saved = await _store.SaveAsync([draft], cancellationToken).ConfigureAwait(false);
        return saved.IsSuccess ? draft : saved.Error!;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<Result<WorkflowDefinition>>> DisableAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        var results = await TransitionAsync(ids, static definition => definition.Status == WorkflowDefinitionStatus.Published
            ? definition with { Status = WorkflowDefinitionStatus.Disabled }
            : StatusConflict(definition), cancellationToken).ConfigureAwait(false);
        await RemoveTimersAsync(results, cancellationToken).ConfigureAwait(false);
        return results;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<Result<WorkflowDefinition>>> ArchiveAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        var results = await TransitionAsync(ids, static definition => definition with { Status = WorkflowDefinitionStatus.Archived }, cancellationToken).ConfigureAwait(false);
        await RemoveTimersAsync(results, cancellationToken).ConfigureAwait(false);
        return results;
    }

    /// <inheritdoc />
    public IReadOnlyList<ActivityDescriptor> ActivityTypes() => _registry.Activities.Select(static activity => activity.Descriptor).OrderBy(static descriptor => descriptor.Type, StringComparer.Ordinal).ToList();

    /// <summary>
    /// 不再发布的定义不再定时启动：删除它们的定义级书签（书签的实例 ID = 定义 ID）。
    /// </summary>
    private async ValueTask RemoveTimersAsync(IReadOnlyList<Result<WorkflowDefinition>> results, CancellationToken cancellationToken)
    {
        var ids = results.Where(static result => result.IsSuccess).Select(static result => result.ValueOrThrow().Id).ToList();
        if (ids.Count > 0)
        {
            await _bookmarks.DeleteByInstanceAsync(ids, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<Result>> DeleteAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var found = await _store.FindManyAsync(ids, cancellationToken).ConfigureAwait(false);
        if (!found.TryGetValue(out var definitions))
        {
            return ids.Select(_ => Result.Failure(found.Error!)).ToList();
        }

        var tenantId = CurrentTenantId();
        var byId = definitions.Where(definition => WorkflowDefinitionNamespace.IsWritable(definition, tenantId)).ToDictionary(static definition => definition.Id, StringComparer.Ordinal);
        var results = new Result[ids.Count];
        var deletable = new List<string>();
        for (var index = 0; index < ids.Count; index++)
        {
            if (!byId.TryGetValue(ids[index], out var definition))
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
            await _bookmarks.DeleteByInstanceAsync(deletable, cancellationToken).ConfigureAwait(false);
            var deleted = await _store.DeleteAsync(deletable, cancellationToken).ConfigureAwait(false);
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

        return results;
    }

    /// <inheritdoc />
    public async ValueTask<Result<Maybe<WorkflowDefinition>>> FindAsync(string id, CancellationToken cancellationToken)
    {
        var found = await _store.FindAsync(id, cancellationToken).ConfigureAwait(false);
        if (!found.TryGetValue(out var maybe))
        {
            return found.Error!;
        }

        // 别的租户的定义按不存在处理
        return maybe.TryGetValue(out var definition) && WorkflowDefinitionNamespace.IsVisible(definition, CurrentTenantId()) ? Maybe.Some(definition) : Maybe.None<WorkflowDefinition>();
    }

    /// <inheritdoc />
    public ValueTask<Result<Maybe<WorkflowDefinition>>> FindPublishedAsync(WorkflowName name, int? version, CancellationToken cancellationToken)
        => WorkflowDefinitionNamespace.FindPublishedAsync(_store, name, version, CurrentTenantId(), cancellationToken);

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<WorkflowDefinition>>> ListAsync(WorkflowName? name, WorkflowDefinitionStatus? status, CancellationToken cancellationToken)
    {
        var listed = await _store.ListAsync(name, status, cancellationToken).ConfigureAwait(false);
        if (!listed.TryGetValue(out var definitions))
        {
            return listed.Error!;
        }

        var tenantId = CurrentTenantId();
        return tenantId is null ? Result.Success(definitions) : definitions.Where(definition => WorkflowDefinitionNamespace.IsVisible(definition, tenantId)).ToList();
    }

    /// <summary>
    /// 批量状态迁移：逐项算出新定义，成功的一次保存。
    /// </summary>
    private async ValueTask<IReadOnlyList<Result<WorkflowDefinition>>> TransitionAsync(IReadOnlyList<string> ids, Func<WorkflowDefinition, Result<WorkflowDefinition>> transition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var found = await _store.FindManyAsync(ids, cancellationToken).ConfigureAwait(false);
        if (!found.TryGetValue(out var definitions))
        {
            return ids.Select(_ => Result.Failure<WorkflowDefinition>(found.Error!)).ToList();
        }

        // 改不动的（别的租户的、有租户调用方眼里的全局定义）按不存在处理
        var tenantId = CurrentTenantId();
        var byId = definitions.Where(definition => WorkflowDefinitionNamespace.IsWritable(definition, tenantId)).ToDictionary(static definition => definition.Id, StringComparer.Ordinal);
        var results = new Result<WorkflowDefinition>[ids.Count];
        var changed = new List<WorkflowDefinition>();
        for (var index = 0; index < ids.Count; index++)
        {
            if (!byId.TryGetValue(ids[index], out var definition))
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
            var saved = await _store.SaveAsync(changed, cancellationToken).ConfigureAwait(false);
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
    /// 当前调用的租户。
    /// </summary>
    private string? CurrentTenantId()
        => _invocation is not null && _invocation.Current.TryGetValue(out var context) ? context.GetSecurity().Tenant?.Id : null;

    /// <summary>
    /// 定义不存在。
    /// </summary>
    private static Error NotFound(string id) => Error.NotFound(WorkflowErrors.DefinitionNotFound, WorkflowMessages.DefinitionNotFound, MessageArguments.Of(("name", id)));

    /// <summary>
    /// 定义状态不允许。
    /// </summary>
    private static Error StatusConflict(WorkflowDefinition definition)
        => Error.Conflict(WorkflowErrors.DefinitionStatus, WorkflowMessages.DefinitionStatus, MessageArguments.Of(("id", definition.Id), ("status", definition.Status.ToString())));
}
