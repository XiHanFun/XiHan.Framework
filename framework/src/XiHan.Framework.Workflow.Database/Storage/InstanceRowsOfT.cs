// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Specifications;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Serialization;
using XiHan.Framework.Workflow.Database.Entities;

namespace XiHan.Framework.Workflow.Database.Storage;

/// <summary>
/// 一个分区（平台或租户）的实例表与节点实例表的读写
/// </summary>
/// <remarks>
/// <see cref="WorkflowInstanceQuery"/> 的每个过滤项都翻译成 SQL 条件（关联 ID 集合是 <c>IN</c>，只取顶层是父实例为空，关键字对折叠后的查询列——标题与关联 ID——做包含判断）；租户分区的表由持久化按工作单元的租户过滤，查询里不写租户条件；删实例级联删节点实例
/// </remarks>
/// <typeparam name="TInstance">实例行类型</typeparam>
/// <typeparam name="TNode">节点实例行类型</typeparam>
internal sealed class InstanceRows<TInstance, TNode>(IRepository<TInstance, string> instances, IRepository<TNode, string> nodes, WriteSequence sequence)
    where TInstance : WorkflowInstanceRow, new()
    where TNode : WorkflowNodeInstanceRow, new()
{
    /// <summary>
    /// 按 ID 找实例
    /// </summary>
    public async ValueTask<Result<Maybe<WorkflowInstance>>> FindAsync(string id, CancellationToken cancellationToken)
    {
        var found = await instances.FindAsync(id, cancellationToken).ConfigureAwait(false);
        return Result.Success(found.TryGetValue(out var record) ? Maybe.Some(WorkflowJson.DeserializeInstance(record.Document)) : Maybe.None<WorkflowInstance>());
    }

    /// <summary>
    /// 按 ID 批量找实例
    /// </summary>
    public async ValueTask<Result<IReadOnlyList<WorkflowInstance>>> FindManyAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        var records = await RecordQueries.FindManyAsync(instances, ids, cancellationToken).ConfigureAwait(false);
        return Result.Success<IReadOnlyList<WorkflowInstance>>(records.Select(static record => WorkflowJson.DeserializeInstance(record.Document)).ToList());
    }

    /// <summary>
    /// 列表：按排序字段与方向，再接创建时间降序、写入序号降序与 ID 补成全序（<see cref="Sorted"/>）
    /// </summary>
    public async ValueTask<Result<IReadOnlyList<WorkflowInstance>>> ListAsync(WorkflowInstanceQuery query, CancellationToken cancellationToken)
    {
        var filtered = Sorted(Filter(query), query).Page(query.Skip, query.Limit);
        var records = await instances.ListAsync(filtered, cancellationToken).ConfigureAwait(false);
        return Result.Success<IReadOnlyList<WorkflowInstance>>(records.Select(static record => WorkflowJson.DeserializeInstance(record.Document)).ToList());
    }

    /// <summary>
    /// 计数
    /// </summary>
    public async ValueTask<Result<long>> CountAsync(WorkflowInstanceQuery query, CancellationToken cancellationToken)
        => await instances.CountAsync(Filter(query), cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// 新增或整体替换实例，返回要同步书签的行
    /// </summary>
    /// <remarks>
    /// 要同步的是新增的行与状态、定义名或关键字查询列变了的行：书签行带着所属实例的状态、定义名与由后两者算出的实例一侧关键字查询列（待办查询下推），见 <see cref="BookmarkRows{TRow, TInstance}.SyncAsync"/>
    /// </remarks>
    public async ValueTask<Result<IReadOnlyList<TInstance>>> SaveAsync(IReadOnlyList<WorkflowInstance> items, CancellationToken cancellationToken)
    {
        var records = items.Select(ToRecord).ToList();
        var replaced = new HashSet<string>(StringComparer.Ordinal);
        var changed = new List<TInstance>();
        var saved = await RecordQueries.UpsertAsync(instances, records, (source, target) =>
        {
            // 已有的行在复制之前比较：状态、定义名与关键字查询列都没变的，书签行不用动（引擎每个波次都写实例，多数写入不改状态）
            replaced.Add(target.Id);
            if (!string.Equals(source.Status, target.Status, StringComparison.Ordinal) || !string.Equals(source.DefinitionName, target.DefinitionName, StringComparison.Ordinal) || !string.Equals(source.SearchText, target.SearchText, StringComparison.Ordinal))
            {
                changed.Add(source);
            }

            Copy(source, target);
        }, cancellationToken).ConfigureAwait(false);
        if (saved.IsFailure)
        {
            return saved.Error!;
        }

        // 新增的行也同步：引擎先写实例再写书签，新实例通常还没有书签，但不依赖这个顺序
        changed.AddRange(records.Where(record => !replaced.Contains(record.Id)));
        return Result.Success<IReadOnlyList<TInstance>>(changed);
    }

    /// <summary>
    /// 删除实例，级联节点实例
    /// </summary>
    public async ValueTask<Result> DeleteAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        // 先删节点实例再删实例：同一事务内，实例删了节点实例不会残留
        foreach (var batch in RecordQueries.Batches(ids))
        {
            var query = EntityQuery.All<TNode>()
                .Where(new Specification<TNode>(record => batch.Contains(record.InstanceId)))
                .OrderBy(static record => record.Id);
            var owned = await RecordQueries.ListAllAsync(nodes, query, null, cancellationToken).ConfigureAwait(false);
            var removedNodes = await RecordQueries.RemoveManyAsync(nodes, owned, cancellationToken).ConfigureAwait(false);
            if (removedNodes.IsFailure)
            {
                return removedNodes;
            }
        }

        var rows = await RecordQueries.FindManyAsync(instances, ids, cancellationToken).ConfigureAwait(false);
        return await RecordQueries.RemoveManyAsync(instances, rows, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 按 ID 找节点实例
    /// </summary>
    public async ValueTask<Result<Maybe<WorkflowNodeInstance>>> FindNodeInstanceAsync(string id, CancellationToken cancellationToken)
    {
        var found = await nodes.FindAsync(id, cancellationToken).ConfigureAwait(false);
        return Result.Success(found.TryGetValue(out var record) ? Maybe.Some(WorkflowJson.DeserializeNodeInstance(record.Document)) : Maybe.None<WorkflowNodeInstance>());
    }

    /// <summary>
    /// 实例的全部节点实例
    /// </summary>
    /// <remarks>
    /// 开始时间升序，同刻按写入序号升序（同一实例的写入由实例锁串行化，序号即创建顺序），再按 ID 补成全序
    /// </remarks>
    public async ValueTask<Result<IReadOnlyList<WorkflowNodeInstance>>> NodeInstancesAsync(string instanceId, CancellationToken cancellationToken)
    {
        var query = EntityQuery.All<TNode>()
            .Where(new Specification<TNode>(record => record.InstanceId == instanceId))
            .OrderBy(static record => record.StartedAt)
            .OrderBy(static record => record.Sequence)
            .OrderBy(static record => record.Id);
        var records = await RecordQueries.ListAllAsync(nodes, query, null, cancellationToken).ConfigureAwait(false);
        return Result.Success<IReadOnlyList<WorkflowNodeInstance>>(records.Select(static record => WorkflowJson.DeserializeNodeInstance(record.Document)).ToList());
    }

    /// <summary>
    /// 新增或整体替换节点实例
    /// </summary>
    public ValueTask<Result> SaveNodeInstancesAsync(IReadOnlyList<WorkflowNodeInstance> items, CancellationToken cancellationToken)
        => RecordQueries.UpsertAsync(nodes, items.Select(ToRecord).ToList(), Copy, cancellationToken);

    /// <summary>
    /// 把查询的过滤项逐项翻译成条件
    /// </summary>
    /// <remarks>
    /// 单个状态与状态集合取并集做 <c>IN</c>，其余等值，时间范围按列比较
    /// </remarks>
    private static EntityQuery<TInstance> Filter(WorkflowInstanceQuery query)
    {
        var filtered = EntityQuery.All<TInstance>();
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
            filtered = filtered.Where(new Specification<TInstance>(record => record.Status == status));
        }
        else if (statuses.Count > 1)
        {
            var names = statuses.Distinct(StringComparer.Ordinal).ToList();
            filtered = filtered.Where(new Specification<TInstance>(record => names.Contains(record.Status)));
        }

        if (query.DefinitionName is { } name)
        {
            var definitionName = name.Value;
            filtered = filtered.Where(new Specification<TInstance>(record => record.DefinitionName == definitionName));
        }

        if (query.CorrelationIds is { } correlationIds)
        {
            // 空集合不匹配任何实例：翻译成恒假条件（SqlSugar 对空的 IN 列表本来就生成 1=2，这里不依赖它）
            var ids = correlationIds.Distinct(StringComparer.Ordinal).Select(static id => (string?)id).ToList();
            filtered = ids.Count == 0
                ? filtered.Where(new Specification<TInstance>(static record => record.Id == null))
                : filtered.Where(new Specification<TInstance>(record => ids.Contains(record.CorrelationId)));
        }

        if (query.ParentInstanceId is { } parentInstanceId)
        {
            filtered = filtered.Where(new Specification<TInstance>(record => record.ParentInstanceId == parentInstanceId));
        }

        if (query.TopLevelOnly)
        {
            filtered = filtered.Where(new Specification<TInstance>(static record => record.ParentInstanceId == null));
        }

        if (WorkflowKeyword.Normalize(query.Keyword) is { } keyword)
        {
            // 标题或关联 ID 包含即匹配（一列里以分隔符隔开，折叠后的关键字里没有分隔符，不跨字段）；两边都已小写折叠（EF Core 与 PostgreSQL、Oracle 的匹配区分大小写），按字面由持久化的 Contains 保证（ADR-0098）；先判非空，升级前写入、还没回填的行不匹配
            filtered = filtered.Where(new Specification<TInstance>(record => record.SearchText != null && record.SearchText.Contains(keyword)));
        }

        if (query.StarterId is { } starterId)
        {
            filtered = filtered.Where(new Specification<TInstance>(record => record.StarterId == starterId));
        }

        if (query.CreatedFrom is { } createdFrom)
        {
            filtered = filtered.Where(new Specification<TInstance>(record => record.CreatedAt >= createdFrom));
        }

        if (query.CreatedTo is { } createdTo)
        {
            filtered = filtered.Where(new Specification<TInstance>(record => record.CreatedAt < createdTo));
        }

        if (query.EndedBefore is { } endedBefore)
        {
            // 不写 EndedAt != null：未结束的行 NULL < 值 本来就不成立，而 SqlSugar 会把该比较翻译成 `<> @参数(NULL)`，永远不匹配
            filtered = filtered.Where(new Specification<TInstance>(record => record.EndedAt < endedBefore));
        }

        return filtered;
    }

    /// <summary>
    /// 没给排序项时的排序：创建时间降序
    /// </summary>
    private static readonly WorkflowInstanceSort[] _defaultSorts = [new(WorkflowInstanceSortField.CreatedAt, Descending: true)];

    /// <summary>
    /// 按实例查询的排序项依次排序，再接次序键补成全序
    /// </summary>
    /// <remarks>
    /// 排序项按列表的顺序依次进 <c>ORDER BY</c>（ADR-0104），之后是次序键创建时间降序、写入序号降序、ID 降序（ADR-0102）；排序项里有创建时间时它不再作次序键，写入序号与 ID 随它的方向，同刻的行按写入先后。文本按列的排序规则比较，状态按状态序号列（生命周期的先后，ADR-0103），结束时间的空值位置照数据库（PostgreSQL、Oracle 当作最大，SQLite、MySQL、SQL Server 当作最小）。排序项为空或字段不在白名单里抛出：调用方（引擎）已校验，走到这里是编程错误
    /// </remarks>
    private static EntityQuery<TInstance> Sorted(EntityQuery<TInstance> query, WorkflowInstanceQuery instanceQuery)
    {
        var sorted = query;
        var used = new HashSet<WorkflowInstanceSortField>();
        bool? creationDescending = null;
        foreach (var sort in instanceQuery.Sorts is { Count: > 0 } sorts ? sorts : _defaultSorts)
        {
            if (sort is null)
            {
                throw new ArgumentException("A workflow instance sort item is null.", nameof(instanceQuery));
            }

            // SQL Server 不许同一列在 ORDER BY 里出现两次：重复的字段只按第一次出现的排（调用方已校验，这里不让一条坏查询打到库上）
            if (!used.Add(sort.Field))
            {
                continue;
            }

            var descending = sort.Descending;
            if (sort.Field == WorkflowInstanceSortField.CreatedAt)
            {
                creationDescending = descending;
            }

            sorted = sort.Field switch
            {
                WorkflowInstanceSortField.CreatedAt => sorted.OrderBy(static record => record.CreatedAt, descending),
                WorkflowInstanceSortField.EndedAt => sorted.OrderBy(static record => record.EndedAt, descending),
                WorkflowInstanceSortField.Status => sorted.OrderBy(static record => record.StatusOrder, descending),
                WorkflowInstanceSortField.DefinitionName => sorted.OrderBy(static record => record.DefinitionName, descending),
                WorkflowInstanceSortField.Title => sorted.OrderBy(static record => record.Title, descending),
                _ => throw new ArgumentOutOfRangeException(nameof(instanceQuery), sort.Field, $"The workflow instance sort field '{sort.Field}' is not supported."),
            };
        }

        // 次序键：创建时间降序（排序项里已有时跳过，同样是为了 SQL Server），写入序号与 ID 随排序项里创建时间的方向，没有时降序
        if (creationDescending is null)
        {
            sorted = sorted.OrderBy(static record => record.CreatedAt, descending: true);
        }

        var tieDescending = creationDescending ?? true;
        return sorted.OrderBy(static record => record.Sequence, tieDescending).OrderBy(static record => record.Id, tieDescending);
    }

    /// <summary>
    /// 实例 → 行
    /// </summary>
    /// <remarks>
    /// 序号只对新增的行生效，替换已有行时 <see cref="Copy(TInstance, TInstance)"/> 不复制它；租户列由持久化在新增时写入
    /// </remarks>
    private TInstance ToRecord(WorkflowInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return new TInstance
        {
            Id = instance.Id,
            Sequence = sequence.Next(),
            DefinitionId = instance.DefinitionId,
            DefinitionName = instance.DefinitionName.Value,
            DefinitionVersion = instance.DefinitionVersion,
            Status = instance.Status.ToString(),
            StatusOrder = WorkflowStatusOrder.Of(instance.Status),
            CorrelationId = instance.CorrelationId,
            StarterId = instance.StarterId,
            ParentInstanceId = instance.ParentInstanceId,
            SearchText = WorkflowKeyword.Join(instance.Title, instance.CorrelationId),
            Title = WorkflowSortKeys.Title(instance.Title),
            CreatedAt = instance.CreatedAt,
            EndedAt = instance.EndedAt,
            Document = WorkflowJson.Serialize(instance),
        };
    }

    /// <summary>
    /// 把新行的内容复制到已有行上
    /// </summary>
    private static void Copy(TInstance source, TInstance target)
    {
        target.DefinitionId = source.DefinitionId;
        target.DefinitionName = source.DefinitionName;
        target.DefinitionVersion = source.DefinitionVersion;
        target.Status = source.Status;
        target.StatusOrder = source.StatusOrder;
        target.CorrelationId = source.CorrelationId;
        target.StarterId = source.StarterId;
        target.ParentInstanceId = source.ParentInstanceId;
        target.SearchText = source.SearchText;
        target.Title = source.Title;
        target.CreatedAt = source.CreatedAt;
        target.EndedAt = source.EndedAt;
        target.Document = source.Document;
    }

    /// <summary>
    /// 节点实例 → 行
    /// </summary>
    /// <remarks>
    /// 序号只对新增的行生效
    /// </remarks>
    private TNode ToRecord(WorkflowNodeInstance nodeInstance)
    {
        ArgumentNullException.ThrowIfNull(nodeInstance);
        return new TNode
        {
            Id = nodeInstance.Id,
            Sequence = sequence.Next(),
            InstanceId = nodeInstance.InstanceId,
            StartedAt = nodeInstance.StartedAt,
            Document = WorkflowJson.Serialize(nodeInstance),
        };
    }

    /// <summary>
    /// 把新行的内容复制到已有行上
    /// </summary>
    private static void Copy(TNode source, TNode target)
    {
        target.InstanceId = source.InstanceId;
        target.StartedAt = source.StartedAt;
        target.Document = source.Document;
    }
}
