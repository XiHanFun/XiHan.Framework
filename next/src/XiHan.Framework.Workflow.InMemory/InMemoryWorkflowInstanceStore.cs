// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Serialization;
using XiHan.Framework.Workflow.Abstractions.Storage;

namespace XiHan.Framework.Workflow.InMemory;

/// <summary>
/// 有界内存的实例与节点实例存储。
/// </summary>
/// <remarks>
/// 保存与返回都是深拷贝；实例与节点实例分别计容量；删实例级联节点实例。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemoryWorkflowInstanceStore : IWorkflowInstanceStore
{
    /// <summary>
    /// 保护表的锁。
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// 实例 ID → (实例, 写入序号)。
    /// </summary>
    private readonly Dictionary<string, (WorkflowInstance Instance, long Sequence)> _instances = new(StringComparer.Ordinal);

    /// <summary>
    /// 节点实例 ID → (节点实例, 写入序号)。
    /// </summary>
    private readonly Dictionary<string, (WorkflowNodeInstance NodeInstance, long Sequence)> _nodeInstances = new(StringComparer.Ordinal);

    /// <summary>
    /// 实例 ID → 它的节点实例 ID。
    /// </summary>
    private readonly Dictionary<string, HashSet<string>> _nodesByInstance = new(StringComparer.Ordinal);

    /// <summary>
    /// 实例容量。
    /// </summary>
    private readonly int _instanceCapacity;

    /// <summary>
    /// 节点实例容量。
    /// </summary>
    private readonly int _nodeCapacity;

    /// <summary>
    /// 写入序号：同一时刻写入的按先后排序。
    /// </summary>
    private long _sequence;

    /// <summary>
    /// 创建。
    /// </summary>
    public InMemoryWorkflowInstanceStore(InMemoryWorkflowOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _instanceCapacity = options.MaxInstances;
        _nodeCapacity = options.MaxNodeInstances;
    }

    /// <summary>
    /// 当前实例数。
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
    public ValueTask<Result<Maybe<WorkflowInstance>>> FindAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        lock (_lock)
        {
            return ValueTask.FromResult(Result.Success(_instances.TryGetValue(id, out var entry) ? Maybe.Some(WorkflowJson.Clone(entry.Instance)) : Maybe.None<WorkflowInstance>()));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<WorkflowInstance>>> FindManyAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        lock (_lock)
        {
            IReadOnlyList<WorkflowInstance> found = ids.Distinct(StringComparer.Ordinal).Where(_instances.ContainsKey).Select(id => WorkflowJson.Clone(_instances[id].Instance)).ToList();
            return ValueTask.FromResult(Result.Success(found));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<WorkflowInstance>>> ListAsync(WorkflowInstanceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        lock (_lock)
        {
            IReadOnlyList<WorkflowInstance> listed = _instances.Values
                .Where(entry => query.Matches(entry.Instance))
                .OrderByDescending(static entry => entry.Instance.CreatedAt)
                .ThenByDescending(static entry => entry.Sequence)
                .Skip(query.Skip)
                .Take(query.Limit)
                .Select(static entry => WorkflowJson.Clone(entry.Instance))
                .ToList();
            return ValueTask.FromResult(Result.Success(listed));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<long>> CountAsync(WorkflowInstanceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        lock (_lock)
        {
            return ValueTask.FromResult(Result.Success(_instances.Values.LongCount(entry => query.Matches(entry.Instance))));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result> SaveAsync(IReadOnlyList<WorkflowInstance> instances, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instances);
        lock (_lock)
        {
            var added = instances.Select(static instance => instance.Id).Distinct(StringComparer.Ordinal).Count(id => !_instances.ContainsKey(id));
            if (_instances.Count + added > _instanceCapacity)
            {
                return ValueTask.FromResult(Result.Failure(Capacity("instances", _instanceCapacity)));
            }

            foreach (var instance in instances)
            {
                var sequence = _instances.TryGetValue(instance.Id, out var existing) ? existing.Sequence : ++_sequence;
                _instances[instance.Id] = (WorkflowJson.Clone(instance), sequence);
            }

            return ValueTask.FromResult(Result.Success());
        }
    }

    /// <inheritdoc />
    public ValueTask<Result> DeleteAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        lock (_lock)
        {
            foreach (var id in ids)
            {
                _instances.Remove(id);
                if (_nodesByInstance.Remove(id, out var nodes))
                {
                    foreach (var node in nodes)
                    {
                        _nodeInstances.Remove(node);
                    }
                }
            }

            return ValueTask.FromResult(Result.Success());
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<Maybe<WorkflowNodeInstance>>> FindNodeInstanceAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        lock (_lock)
        {
            return ValueTask.FromResult(Result.Success(_nodeInstances.TryGetValue(id, out var entry) ? Maybe.Some(WorkflowJson.Clone(entry.NodeInstance)) : Maybe.None<WorkflowNodeInstance>()));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<WorkflowNodeInstance>>> NodeInstancesAsync(string instanceId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        lock (_lock)
        {
            IReadOnlyList<WorkflowNodeInstance> listed = _nodesByInstance.TryGetValue(instanceId, out var nodes)
                ? nodes.Select(id => _nodeInstances[id]).OrderBy(static entry => entry.NodeInstance.StartedAt).ThenBy(static entry => entry.Sequence).Select(static entry => WorkflowJson.Clone(entry.NodeInstance)).ToList()
                : [];
            return ValueTask.FromResult(Result.Success(listed));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result> SaveNodeInstancesAsync(IReadOnlyList<WorkflowNodeInstance> nodeInstances, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(nodeInstances);
        lock (_lock)
        {
            var added = nodeInstances.Select(static node => node.Id).Distinct(StringComparer.Ordinal).Count(id => !_nodeInstances.ContainsKey(id));
            if (_nodeInstances.Count + added > _nodeCapacity)
            {
                return ValueTask.FromResult(Result.Failure(Capacity("node-instances", _nodeCapacity)));
            }

            foreach (var nodeInstance in nodeInstances)
            {
                var sequence = _nodeInstances.TryGetValue(nodeInstance.Id, out var existing) ? existing.Sequence : ++_sequence;
                _nodeInstances[nodeInstance.Id] = (WorkflowJson.Clone(nodeInstance), sequence);
                if (!_nodesByInstance.TryGetValue(nodeInstance.InstanceId, out var nodes))
                {
                    nodes = new HashSet<string>(StringComparer.Ordinal);
                    _nodesByInstance[nodeInstance.InstanceId] = nodes;
                }

                nodes.Add(nodeInstance.Id);
            }

            return ValueTask.FromResult(Result.Success());
        }
    }

    /// <summary>
    /// 容量已满。
    /// </summary>
    private static Error Capacity(string store, int limit)
        => Error.Unavailable(WorkflowErrors.StoreCapacity, WorkflowMessages.StoreCapacity, MessageArguments.Of(("store", store), ("limit", limit)));
}
