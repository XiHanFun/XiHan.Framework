// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Serialization;
using XiHan.Framework.Workflow.Abstractions.Storage;

namespace XiHan.Framework.Workflow.InMemory;

/// <summary>
/// 有界内存的定义存储。
/// </summary>
/// <remarks>
/// 保存与返回都是深拷贝（读己之写，不泄漏引用）；容量满 → <see cref="WorkflowErrors.StoreCapacity"/>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemoryWorkflowDefinitionStore : IWorkflowDefinitionStore
{
    /// <summary>
    /// 保护表的锁。
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// ID → 定义。
    /// </summary>
    private readonly Dictionary<string, WorkflowDefinition> _definitions = new(StringComparer.Ordinal);

    /// <summary>
    /// 容量。
    /// </summary>
    private readonly int _capacity;

    /// <summary>
    /// 创建。
    /// </summary>
    public InMemoryWorkflowDefinitionStore(InMemoryWorkflowOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _capacity = options.MaxDefinitions;
    }

    /// <summary>
    /// 当前条数。
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _definitions.Count;
            }
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<Maybe<WorkflowDefinition>>> FindAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        lock (_lock)
        {
            return ValueTask.FromResult(Result.Success(_definitions.TryGetValue(id, out var definition) ? Maybe.Some(WorkflowJson.Clone(definition)) : Maybe.None<WorkflowDefinition>()));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<WorkflowDefinition>>> FindManyAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        lock (_lock)
        {
            IReadOnlyList<WorkflowDefinition> found = ids.Distinct(StringComparer.Ordinal).Where(_definitions.ContainsKey).Select(id => WorkflowJson.Clone(_definitions[id])).ToList();
            return ValueTask.FromResult(Result.Success(found));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<Maybe<WorkflowDefinition>>> FindAsync(WorkflowName name, int version, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            var definition = _definitions.Values.FirstOrDefault(candidate => candidate.Name == name && candidate.Version == version);
            return ValueTask.FromResult(Result.Success(definition is null ? Maybe.None<WorkflowDefinition>() : Maybe.Some(WorkflowJson.Clone(definition))));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<Maybe<WorkflowDefinition>>> FindLatestPublishedAsync(WorkflowName name, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            var definition = _definitions.Values.Where(candidate => candidate.Name == name && candidate.Status == WorkflowDefinitionStatus.Published).OrderByDescending(static candidate => candidate.Version).FirstOrDefault();
            return ValueTask.FromResult(Result.Success(definition is null ? Maybe.None<WorkflowDefinition>() : Maybe.Some(WorkflowJson.Clone(definition))));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<int>> MaxVersionAsync(WorkflowName name, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            var max = _definitions.Values.Where(candidate => candidate.Name == name).Select(static candidate => candidate.Version).DefaultIfEmpty(0).Max();
            return ValueTask.FromResult(Result.Success(max));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<WorkflowDefinition>>> ListAsync(WorkflowName? name, WorkflowDefinitionStatus? status, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            IReadOnlyList<WorkflowDefinition> listed = _definitions.Values
                .Where(candidate => (name is null || candidate.Name == name.Value) && (status is null || candidate.Status == status.Value))
                .OrderBy(static candidate => candidate.Name.Value, StringComparer.Ordinal)
                .ThenByDescending(static candidate => candidate.Version)
                .Select(WorkflowJson.Clone)
                .ToList();
            return ValueTask.FromResult(Result.Success(listed));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result> SaveAsync(IReadOnlyList<WorkflowDefinition> definitions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        lock (_lock)
        {
            // 容量按整批判定：要么全部写入，要么一条不写
            var added = definitions.Select(static definition => definition.Id).Distinct(StringComparer.Ordinal).Count(id => !_definitions.ContainsKey(id));
            if (_definitions.Count + added > _capacity)
            {
                return ValueTask.FromResult(Result.Failure(Error.Unavailable(WorkflowErrors.StoreCapacity, WorkflowMessages.StoreCapacity, MessageArguments.Of(("store", "definitions"), ("limit", _capacity)))));
            }

            foreach (var definition in definitions)
            {
                _definitions[definition.Id] = WorkflowJson.Clone(definition);
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
                _definitions.Remove(id);
            }

            return ValueTask.FromResult(Result.Success());
        }
    }
}
