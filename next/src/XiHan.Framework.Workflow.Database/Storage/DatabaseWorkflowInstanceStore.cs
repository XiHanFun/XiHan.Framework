// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Specifications;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Serialization;
using XiHan.Framework.Workflow.Abstractions.Storage;
using XiHan.Framework.Workflow.Database.Entities;

namespace XiHan.Framework.Workflow.Database.Storage;

/// <summary>
/// 数据库实例与节点实例存储（单例）：每个操作在新作用域里经完整管道执行；<see cref="WorkflowInstanceQuery"/> 的每个过滤项都翻译成 SQL 条件；删实例级联删节点实例。管道失败 → <see cref="WorkflowErrors.StoreUnavailable"/>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class DatabaseWorkflowInstanceStore : IWorkflowInstanceStore
{
    /// <summary>
    /// 管道执行。
    /// </summary>
    private readonly StoreInvocation _invocation;

    /// <summary>
    /// 写入序号。
    /// </summary>
    private readonly WriteSequence _sequence;

    /// <summary>
    /// 创建。
    /// </summary>
    public DatabaseWorkflowInstanceStore(IServiceProvider services, App app, TimeProvider clock)
    {
        _invocation = new StoreInvocation(services, app);
        _sequence = new WriteSequence(clock);
    }

    /// <inheritdoc />
    public async ValueTask<Result<Maybe<WorkflowInstance>>> FindAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        return StoreInvocation.OrUnavailable(await _invocation.RunAsync(async ctx =>
        {
            var found = await StoreInvocation.Repository<WorkflowInstanceRecord>(ctx).FindAsync(id, ctx.CancellationToken).ConfigureAwait(false);
            return Result.Success(found.TryGetValue(out var record) ? Maybe.Some(WorkflowJson.DeserializeInstance(record.Document)) : Maybe.None<WorkflowInstance>());
        }, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<WorkflowInstance>>> FindManyAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return StoreInvocation.OrUnavailable(await _invocation.RunAsync(async ctx =>
        {
            var records = await RecordQueries.FindManyAsync(StoreInvocation.Repository<WorkflowInstanceRecord>(ctx), ids, ctx.CancellationToken).ConfigureAwait(false);
            return Result.Success<IReadOnlyList<WorkflowInstance>>(records.Select(static record => WorkflowJson.DeserializeInstance(record.Document)).ToList());
        }, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<WorkflowInstance>>> ListAsync(WorkflowInstanceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        // 创建时间降序，同刻按写入序号降序，再按 ID 补成全序
        var filtered = Filter(query)
            .OrderBy(static record => record.CreatedAt, descending: true)
            .OrderBy(static record => record.Sequence, descending: true)
            .OrderBy(static record => record.Id, descending: true)
            .Page(query.Skip, query.Limit);
        return StoreInvocation.OrUnavailable(await _invocation.RunAsync(async ctx =>
        {
            var records = await StoreInvocation.Repository<WorkflowInstanceRecord>(ctx).ListAsync(filtered, ctx.CancellationToken).ConfigureAwait(false);
            return Result.Success<IReadOnlyList<WorkflowInstance>>(records.Select(static record => WorkflowJson.DeserializeInstance(record.Document)).ToList());
        }, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result<long>> CountAsync(WorkflowInstanceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var filtered = Filter(query);
        return StoreInvocation.OrUnavailable(await _invocation.RunAsync(async ctx =>
            Result.Success(await StoreInvocation.Repository<WorkflowInstanceRecord>(ctx).CountAsync(filtered, ctx.CancellationToken).ConfigureAwait(false)), cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result> SaveAsync(IReadOnlyList<WorkflowInstance> instances, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instances);
        var rows = instances.Select(ToRecord).ToList();
        return StoreInvocation.OrUnavailable(await _invocation.RunAsync(
            ctx => RecordQueries.UpsertAsync(StoreInvocation.Repository<WorkflowInstanceRecord>(ctx), rows, Copy, ctx.CancellationToken),
            cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result> DeleteAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return StoreInvocation.OrUnavailable(await _invocation.RunAsync(async ctx =>
        {
            var instances = StoreInvocation.Repository<WorkflowInstanceRecord>(ctx);
            var nodes = StoreInvocation.Repository<WorkflowNodeInstanceRecord>(ctx);
            // 先删节点实例再删实例：同一事务内，实例删了节点实例不会残留
            foreach (var batch in RecordQueries.Batches(ids))
            {
                var query = EntityQuery.All<WorkflowNodeInstanceRecord>()
                    .Where(new Specification<WorkflowNodeInstanceRecord>(record => batch.Contains(record.InstanceId)))
                    .OrderBy(static record => record.Id);
                var owned = await RecordQueries.ListAllAsync(nodes, query, null, ctx.CancellationToken).ConfigureAwait(false);
                var removedNodes = await RecordQueries.RemoveManyAsync(nodes, owned, ctx.CancellationToken).ConfigureAwait(false);
                if (removedNodes.IsFailure)
                {
                    return removedNodes;
                }
            }

            var rows = await RecordQueries.FindManyAsync(instances, ids, ctx.CancellationToken).ConfigureAwait(false);
            return await RecordQueries.RemoveManyAsync(instances, rows, ctx.CancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result<Maybe<WorkflowNodeInstance>>> FindNodeInstanceAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        return StoreInvocation.OrUnavailable(await _invocation.RunAsync(async ctx =>
        {
            var found = await StoreInvocation.Repository<WorkflowNodeInstanceRecord>(ctx).FindAsync(id, ctx.CancellationToken).ConfigureAwait(false);
            return Result.Success(found.TryGetValue(out var record) ? Maybe.Some(WorkflowJson.DeserializeNodeInstance(record.Document)) : Maybe.None<WorkflowNodeInstance>());
        }, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<WorkflowNodeInstance>>> NodeInstancesAsync(string instanceId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        // 开始时间升序，同刻按写入序号升序（同一实例的写入由实例锁串行化，序号即创建顺序），再按 ID 补成全序
        var query = EntityQuery.All<WorkflowNodeInstanceRecord>()
            .Where(new Specification<WorkflowNodeInstanceRecord>(record => record.InstanceId == instanceId))
            .OrderBy(static record => record.StartedAt)
            .OrderBy(static record => record.Sequence)
            .OrderBy(static record => record.Id);
        return StoreInvocation.OrUnavailable(await _invocation.RunAsync(async ctx =>
        {
            var records = await RecordQueries.ListAllAsync(StoreInvocation.Repository<WorkflowNodeInstanceRecord>(ctx), query, null, ctx.CancellationToken).ConfigureAwait(false);
            return Result.Success<IReadOnlyList<WorkflowNodeInstance>>(records.Select(static record => WorkflowJson.DeserializeNodeInstance(record.Document)).ToList());
        }, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result> SaveNodeInstancesAsync(IReadOnlyList<WorkflowNodeInstance> nodeInstances, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(nodeInstances);
        var rows = nodeInstances.Select(ToRecord).ToList();
        return StoreInvocation.OrUnavailable(await _invocation.RunAsync(
            ctx => RecordQueries.UpsertAsync(StoreInvocation.Repository<WorkflowNodeInstanceRecord>(ctx), rows, Copy, ctx.CancellationToken),
            cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// 把查询的过滤项逐项翻译成条件：单个状态与状态集合取并集做 <c>IN</c>，其余等值，时间范围按列比较。
    /// </summary>
    private static EntityQuery<WorkflowInstanceRecord> Filter(WorkflowInstanceQuery query)
    {
        var filtered = EntityQuery.All<WorkflowInstanceRecord>();
        var statuses = new List<string>();
        if (query.Status is { } single)
        {
            statuses.Add(single.ToString());
        }

        if (query.Statuses is { Count: > 0 } set)
        {
            statuses.AddRange(set.Select(static status => status.ToString()));
        }

        if (statuses.Count == 1)
        {
            var status = statuses[0];
            filtered = filtered.Where(new Specification<WorkflowInstanceRecord>(record => record.Status == status));
        }
        else if (statuses.Count > 1)
        {
            var names = statuses.Distinct(StringComparer.Ordinal).ToList();
            filtered = filtered.Where(new Specification<WorkflowInstanceRecord>(record => names.Contains(record.Status)));
        }

        if (query.DefinitionName is { } name)
        {
            var definitionName = name.Value;
            filtered = filtered.Where(new Specification<WorkflowInstanceRecord>(record => record.DefinitionName == definitionName));
        }

        if (query.CorrelationId is { } correlationId)
        {
            filtered = filtered.Where(new Specification<WorkflowInstanceRecord>(record => record.CorrelationId == correlationId));
        }

        if (query.ParentInstanceId is { } parentInstanceId)
        {
            filtered = filtered.Where(new Specification<WorkflowInstanceRecord>(record => record.ParentInstanceId == parentInstanceId));
        }

        if (query.StarterId is { } starterId)
        {
            filtered = filtered.Where(new Specification<WorkflowInstanceRecord>(record => record.StarterId == starterId));
        }

        if (query.TenantId is { } tenantId)
        {
            filtered = filtered.Where(new Specification<WorkflowInstanceRecord>(record => record.TenantId == tenantId));
        }

        if (query.CreatedFrom is { } createdFrom)
        {
            filtered = filtered.Where(new Specification<WorkflowInstanceRecord>(record => record.CreatedAt >= createdFrom));
        }

        if (query.CreatedTo is { } createdTo)
        {
            filtered = filtered.Where(new Specification<WorkflowInstanceRecord>(record => record.CreatedAt < createdTo));
        }

        if (query.EndedBefore is { } endedBefore)
        {
            // 不写 EndedAt != null：未结束的行 NULL < 值 本来就不成立，而 SqlSugar 会把该比较翻译成 `<> @参数(NULL)`，永远不匹配
            filtered = filtered.Where(new Specification<WorkflowInstanceRecord>(record => record.EndedAt < endedBefore));
        }

        return filtered;
    }

    /// <summary>
    /// 实例 → 行；序号只对新增的行生效，替换已有行时 <see cref="Copy(WorkflowInstanceRecord, WorkflowInstanceRecord)"/> 不复制它。
    /// </summary>
    private WorkflowInstanceRecord ToRecord(WorkflowInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return new WorkflowInstanceRecord
        {
            Id = instance.Id,
            Sequence = _sequence.Next(),
            DefinitionId = instance.DefinitionId,
            DefinitionName = instance.DefinitionName.Value,
            DefinitionVersion = instance.DefinitionVersion,
            Status = instance.Status.ToString(),
            CorrelationId = instance.CorrelationId,
            StarterId = instance.StarterId,
            ParentInstanceId = instance.ParentInstanceId,
            TenantId = instance.TenantId,
            CreatedAt = instance.CreatedAt,
            EndedAt = instance.EndedAt,
            Document = WorkflowJson.Serialize(instance),
        };
    }

    /// <summary>
    /// 把新行的内容复制到已有行上。
    /// </summary>
    private static void Copy(WorkflowInstanceRecord source, WorkflowInstanceRecord target)
    {
        target.DefinitionId = source.DefinitionId;
        target.DefinitionName = source.DefinitionName;
        target.DefinitionVersion = source.DefinitionVersion;
        target.Status = source.Status;
        target.CorrelationId = source.CorrelationId;
        target.StarterId = source.StarterId;
        target.ParentInstanceId = source.ParentInstanceId;
        target.TenantId = source.TenantId;
        target.CreatedAt = source.CreatedAt;
        target.EndedAt = source.EndedAt;
        target.Document = source.Document;
    }

    /// <summary>
    /// 节点实例 → 行；序号只对新增的行生效。
    /// </summary>
    private WorkflowNodeInstanceRecord ToRecord(WorkflowNodeInstance nodeInstance)
    {
        ArgumentNullException.ThrowIfNull(nodeInstance);
        return new WorkflowNodeInstanceRecord
        {
            Id = nodeInstance.Id,
            Sequence = _sequence.Next(),
            InstanceId = nodeInstance.InstanceId,
            StartedAt = nodeInstance.StartedAt,
            Document = WorkflowJson.Serialize(nodeInstance),
        };
    }

    /// <summary>
    /// 把新行的内容复制到已有行上。
    /// </summary>
    private static void Copy(WorkflowNodeInstanceRecord source, WorkflowNodeInstanceRecord target)
    {
        target.InstanceId = source.InstanceId;
        target.StartedAt = source.StartedAt;
        target.Document = source.Document;
    }
}
