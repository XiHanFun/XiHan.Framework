// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Serialization;
using XiHan.Framework.Workflow.Abstractions.Storage;

namespace XiHan.Framework.Workflow.InMemory;

/// <summary>
/// 有界内存的实例与节点实例存储
/// </summary>
/// <remarks>
/// 按分区（平台或租户）隔离：键是分区加 ID，节点实例随所属实例的分区。保存与返回都是深拷贝；实例与节点实例分别计容量（全部分区合计）；删实例级联节点实例。没有事务：写入立即生效
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemoryWorkflowInstanceStore : IWorkflowInstanceStore
{
    /// <summary>
    /// 保护表的锁
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// 分区与实例 ID → (实例, 写入序号)
    /// </summary>
    private readonly Dictionary<PartitionKey, (WorkflowInstance Instance, long Sequence)> _instances = [];

    /// <summary>
    /// 分区与节点实例 ID → (节点实例, 写入序号)
    /// </summary>
    private readonly Dictionary<PartitionKey, (WorkflowNodeInstance NodeInstance, long Sequence)> _nodeInstances = [];

    /// <summary>
    /// 分区与实例 ID → 它的节点实例 ID
    /// </summary>
    private readonly Dictionary<PartitionKey, HashSet<string>> _nodesByInstance = [];

    /// <summary>
    /// 实例容量
    /// </summary>
    private readonly int _instanceCapacity;

    /// <summary>
    /// 节点实例容量
    /// </summary>
    private readonly int _nodeCapacity;

    /// <summary>
    /// 写入序号：同一时刻写入的按先后排序
    /// </summary>
    private long _sequence;

    /// <summary>
    /// 创建
    /// </summary>
    public InMemoryWorkflowInstanceStore(InMemoryWorkflowOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _instanceCapacity = options.MaxInstances;
        _nodeCapacity = options.MaxNodeInstances;
    }

    /// <summary>
    /// 运行中实例的定义名、标题与关联 ID（按实例 ID）
    /// </summary>
    /// <remarks>
    /// 书签存储的待办查询以此与实例连接（定义名过滤；关键字匹配实例标题、定义名与关联 ID）：不在运行中、不存在或在别的分区的实例不在结果里
    /// </remarks>
    internal Dictionary<string, (WorkflowName Definition, string Title, string? CorrelationId)> RunningDefinitions(string? tenantId, IEnumerable<string> instanceIds)
    {
        lock (_lock)
        {
            var running = new Dictionary<string, (WorkflowName Definition, string Title, string? CorrelationId)>(StringComparer.Ordinal);
            foreach (var id in instanceIds)
            {
                if (_instances.TryGetValue(PartitionKey.In(tenantId, id), out var entry) && entry.Instance.Status == WorkflowInstanceStatus.Running)
                {
                    running[id] = (entry.Instance.DefinitionName, entry.Instance.Title, entry.Instance.CorrelationId);
                }
            }

            return running;
        }
    }

    /// <summary>
    /// 当前实例数（全部分区）
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _instances.Count;
            }
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<Maybe<WorkflowInstance>>> FindAsync(string? tenantId, string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        lock (_lock)
        {
            return ValueTask.FromResult(Result.Success(_instances.TryGetValue(PartitionKey.In(tenantId, id), out var entry) ? Maybe.Some(WorkflowJson.Clone(entry.Instance)) : Maybe.None<WorkflowInstance>()));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<WorkflowInstance>>> FindManyAsync(string? tenantId, IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        lock (_lock)
        {
            IReadOnlyList<WorkflowInstance> found = ids.Distinct(StringComparer.Ordinal)
                .Select(id => _instances.TryGetValue(PartitionKey.In(tenantId, id), out var entry) ? entry.Instance : null)
                .OfType<WorkflowInstance>()
                .Select(WorkflowJson.Clone)
                .ToList();
            return ValueTask.FromResult(Result.Success(found));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<WorkflowInstance>>> ListAsync(string? tenantId, WorkflowInstanceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        // 用到了本实现不认识的条件即失败：不读不认识的条件，也不给出放宽了的结果（ADR-0228）
        if (query.Validate(WorkflowInstanceQuery.Revision).Error is { } unsupported)
        {
            return ValueTask.FromResult(Result.Failure<IReadOnlyList<WorkflowInstance>>(unsupported));
        }

        lock (_lock)
        {
            IReadOnlyList<WorkflowInstance> listed = Sorted(InPartition(tenantId).Where(entry => query.Matches(entry.Instance)), query)
                .Skip(query.Skip)
                .Take(query.Take)
                .Select(static entry => WorkflowJson.Clone(entry.Instance))
                .ToList();
            return ValueTask.FromResult(Result.Success(listed));
        }
    }

    /// <summary>
    /// 没给排序项时的排序：创建时间降序
    /// </summary>
    private static readonly WorkflowInstanceSort[] _defaultSorts = [new(WorkflowInstanceSortField.CreatedAt, Descending: true)];

    /// <summary>
    /// 按实例查询的排序项依次排序，再接次序键创建时间降序、写入先后降序
    /// </summary>
    /// <remarks>
    /// 写入序号在全部分区里唯一，接上它即是全序，分页不重不漏（ADR-0102、ADR-0104）；排序项里有创建时间时不再作次序键，写入先后随它的方向（同刻的先写的在升序里排前面），没有时降序。重复的字段只按第一次出现的排，与数据库实现的 SQL 同一结果（调用方已校验重复）。文本按序数比较，状态按生命周期的序号（<see cref="WorkflowStatusOrder"/>，ADR-0103），结束时间的空值当作最小（与 <see cref="Comparer{T}.Default"/> 相同），标题按 <see cref="WorkflowSortKeys.Title"/>
    /// </remarks>
    private static IOrderedEnumerable<(WorkflowInstance Instance, long Sequence)> Sorted(IEnumerable<(WorkflowInstance Instance, long Sequence)> entries, WorkflowInstanceQuery query)
    {
        IOrderedEnumerable<(WorkflowInstance Instance, long Sequence)>? sorted = null;
        var used = new HashSet<WorkflowInstanceSortField>();
        bool? creationDescending = null;
        foreach (var sort in query.Sorts is { Count: > 0 } sorts ? sorts : _defaultSorts)
        {
            if (sort is null)
            {
                throw new ArgumentException("A workflow instance sort item is null.", nameof(query));
            }

            if (!used.Add(sort.Field))
            {
                continue;
            }

            if (sort.Field == WorkflowInstanceSortField.CreatedAt)
            {
                creationDescending = sort.Descending;
            }

            sorted = Then(entries, sorted, sort);
        }

        // 次序键：创建时间降序（排序项里已有时跳过），写入先后随排序项里创建时间的方向，没有时降序
        var ordered = creationDescending is null ? sorted!.ThenByDescending(static entry => entry.Instance.CreatedAt) : sorted!;
        return creationDescending ?? true ? ordered.ThenByDescending(static entry => entry.Sequence) : ordered.ThenBy(static entry => entry.Sequence);
    }

    /// <summary>
    /// 接上一个排序项：还没有排序时开始排，否则作次级排序
    /// </summary>
    private static IOrderedEnumerable<(WorkflowInstance Instance, long Sequence)> Then(IEnumerable<(WorkflowInstance Instance, long Sequence)> entries, IOrderedEnumerable<(WorkflowInstance Instance, long Sequence)>? sorted, WorkflowInstanceSort sort) => sort.Field switch
    {
        WorkflowInstanceSortField.CreatedAt => Order(entries, sorted, static entry => entry.Instance.CreatedAt, Comparer<DateTimeOffset>.Default, sort.Descending),
        WorkflowInstanceSortField.EndedAt => Order(entries, sorted, static entry => entry.Instance.EndedAt, Comparer<DateTimeOffset?>.Default, sort.Descending),
        WorkflowInstanceSortField.Status => Order(entries, sorted, static entry => WorkflowStatusOrder.Of(entry.Instance.Status), Comparer<int>.Default, sort.Descending),
        WorkflowInstanceSortField.DefinitionName => Order(entries, sorted, static entry => entry.Instance.DefinitionName.Value, StringComparer.Ordinal, sort.Descending),
        WorkflowInstanceSortField.Title => Order(entries, sorted, static entry => WorkflowSortKeys.Title(entry.Instance.Title), StringComparer.Ordinal, sort.Descending),
        _ => throw new ArgumentOutOfRangeException(nameof(sort), sort.Field, $"The workflow instance sort field '{sort.Field}' is not supported."),
    };

    /// <summary>
    /// 按一个键升序或降序排：作主排序或次级排序
    /// </summary>
    private static IOrderedEnumerable<(WorkflowInstance Instance, long Sequence)> Order<TKey>(IEnumerable<(WorkflowInstance Instance, long Sequence)> entries, IOrderedEnumerable<(WorkflowInstance Instance, long Sequence)>? sorted, Func<(WorkflowInstance Instance, long Sequence), TKey> key, IComparer<TKey> comparer, bool descending)
        => sorted is null
            ? (descending ? entries.OrderByDescending(key, comparer) : entries.OrderBy(key, comparer))
            : (descending ? sorted.ThenByDescending(key, comparer) : sorted.ThenBy(key, comparer));

    /// <inheritdoc />
    public ValueTask<Result<long>> CountAsync(string? tenantId, WorkflowInstanceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        // 用到了本实现不认识的条件即失败：不读不认识的条件，也不给出放宽了的结果（ADR-0228）
        if (query.Validate(WorkflowInstanceQuery.Revision).Error is { } unsupported)
        {
            return ValueTask.FromResult(Result.Failure<long>(unsupported));
        }

        lock (_lock)
        {
            return ValueTask.FromResult(Result.Success(InPartition(tenantId).LongCount(entry => query.Matches(entry.Instance))));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result> SaveAsync(string? tenantId, IReadOnlyList<WorkflowInstance> instances, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instances);
        foreach (var instance in instances)
        {
            PartitionKey.EnsureOwned(tenantId, instance.TenantId, "instance", instance.Id);
        }

        lock (_lock)
        {
            var added = instances.Select(instance => PartitionKey.In(tenantId, instance.Id)).Distinct().Count(key => !_instances.ContainsKey(key));
            if (_instances.Count + added > _instanceCapacity)
            {
                return ValueTask.FromResult(Result.Failure(Capacity("instances", _instanceCapacity)));
            }

            foreach (var instance in instances)
            {
                var key = PartitionKey.In(tenantId, instance.Id);
                var sequence = _instances.TryGetValue(key, out var existing) ? existing.Sequence : ++_sequence;
                _instances[key] = (WorkflowJson.Clone(instance), sequence);
            }

            return ValueTask.FromResult(Result.Success());
        }
    }

    /// <inheritdoc />
    public ValueTask<Result> DeleteAsync(string? tenantId, IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        lock (_lock)
        {
            foreach (var id in ids)
            {
                var key = PartitionKey.In(tenantId, id);
                _instances.Remove(key);
                if (_nodesByInstance.Remove(key, out var nodes))
                {
                    foreach (var node in nodes)
                    {
                        _nodeInstances.Remove(PartitionKey.In(tenantId, node));
                    }
                }
            }

            return ValueTask.FromResult(Result.Success());
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<Maybe<WorkflowNodeInstance>>> FindNodeInstanceAsync(string? tenantId, string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        lock (_lock)
        {
            return ValueTask.FromResult(Result.Success(_nodeInstances.TryGetValue(PartitionKey.In(tenantId, id), out var entry) ? Maybe.Some(WorkflowJson.Clone(entry.NodeInstance)) : Maybe.None<WorkflowNodeInstance>()));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<WorkflowNodeInstance>>> NodeInstancesAsync(string? tenantId, string instanceId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        lock (_lock)
        {
            IReadOnlyList<WorkflowNodeInstance> listed = _nodesByInstance.TryGetValue(PartitionKey.In(tenantId, instanceId), out var nodes)
                ? nodes.Select(id => _nodeInstances[PartitionKey.In(tenantId, id)]).OrderBy(static entry => entry.NodeInstance.StartedAt).ThenBy(static entry => entry.Sequence).Select(static entry => WorkflowJson.Clone(entry.NodeInstance)).ToList()
                : [];
            return ValueTask.FromResult(Result.Success(listed));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result> SaveNodeInstancesAsync(string? tenantId, IReadOnlyList<WorkflowNodeInstance> nodeInstances, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(nodeInstances);
        lock (_lock)
        {
            var added = nodeInstances.Select(node => PartitionKey.In(tenantId, node.Id)).Distinct().Count(key => !_nodeInstances.ContainsKey(key));
            if (_nodeInstances.Count + added > _nodeCapacity)
            {
                return ValueTask.FromResult(Result.Failure(Capacity("node-instances", _nodeCapacity)));
            }

            foreach (var nodeInstance in nodeInstances)
            {
                var key = PartitionKey.In(tenantId, nodeInstance.Id);
                var sequence = _nodeInstances.TryGetValue(key, out var existing) ? existing.Sequence : ++_sequence;
                _nodeInstances[key] = (WorkflowJson.Clone(nodeInstance), sequence);
                var owner = PartitionKey.In(tenantId, nodeInstance.InstanceId);
                if (!_nodesByInstance.TryGetValue(owner, out var nodes))
                {
                    nodes = new HashSet<string>(StringComparer.Ordinal);
                    _nodesByInstance[owner] = nodes;
                }

                nodes.Add(nodeInstance.Id);
            }

            return ValueTask.FromResult(Result.Success());
        }
    }

    /// <summary>
    /// 一个分区里的实例（调用方持锁）
    /// </summary>
    private IEnumerable<(WorkflowInstance Instance, long Sequence)> InPartition(string? tenantId)
    {
        var partition = PartitionKey.Of(tenantId);
        return _instances.Where(entry => string.Equals(entry.Key.Tenant, partition, StringComparison.Ordinal)).Select(static entry => entry.Value);
    }

    /// <summary>
    /// 容量已满
    /// </summary>
    private static Error Capacity(string store, int limit)
        => Error.Unavailable(WorkflowErrors.StoreCapacity, WorkflowMessages.StoreCapacity, MessageArguments.Of(("store", store), ("limit", limit)));
}
