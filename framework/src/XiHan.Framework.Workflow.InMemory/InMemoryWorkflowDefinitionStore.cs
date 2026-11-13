// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Serialization;
using XiHan.Framework.Workflow.Abstractions.Storage;

namespace XiHan.Framework.Workflow.InMemory;

/// <summary>
/// 有界内存的定义存储
/// </summary>
/// <remarks>
/// 按分区（平台或租户）隔离：键是分区加 ID，查询只看给定分区。保存与返回都是深拷贝（读己之写，不泄漏引用）；容量按全部分区合计，满 → <see cref="WorkflowErrors.StoreCapacity"/>。没有事务：写入立即生效
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemoryWorkflowDefinitionStore : IWorkflowDefinitionStore
{
    /// <summary>
    /// 保护表的锁
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// 分区与 ID → 定义
    /// </summary>
    private readonly Dictionary<PartitionKey, WorkflowDefinition> _definitions = [];

    /// <summary>
    /// 容量
    /// </summary>
    private readonly int _capacity;

    /// <summary>
    /// 创建
    /// </summary>
    public InMemoryWorkflowDefinitionStore(InMemoryWorkflowOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _capacity = options.MaxDefinitions;
    }

    /// <summary>
    /// 当前条数（全部分区）
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
    public ValueTask<Result<Maybe<WorkflowDefinition>>> FindAsync(string? tenantId, string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        lock (_lock)
        {
            return ValueTask.FromResult(Result.Success(_definitions.TryGetValue(PartitionKey.In(tenantId, id), out var definition) ? Maybe.Some(WorkflowJson.Clone(definition)) : Maybe.None<WorkflowDefinition>()));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<WorkflowDefinition>>> FindManyAsync(string? tenantId, IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        lock (_lock)
        {
            IReadOnlyList<WorkflowDefinition> found = ids.Distinct(StringComparer.Ordinal)
                .Select(id => _definitions.TryGetValue(PartitionKey.In(tenantId, id), out var definition) ? definition : null)
                .OfType<WorkflowDefinition>()
                .Select(WorkflowJson.Clone)
                .ToList();
            return ValueTask.FromResult(Result.Success(found));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<Maybe<WorkflowDefinition>>> FindAsync(string? tenantId, WorkflowName name, int version, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            var definition = InPartition(tenantId).FirstOrDefault(candidate => candidate.Name == name && candidate.Version == version);
            return ValueTask.FromResult(Result.Success(definition is null ? Maybe.None<WorkflowDefinition>() : Maybe.Some(WorkflowJson.Clone(definition))));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<Maybe<WorkflowDefinition>>> FindLatestPublishedAsync(string? tenantId, WorkflowName name, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            var definition = InPartition(tenantId).Where(candidate => candidate.Name == name && candidate.Status == WorkflowDefinitionStatus.Published).OrderByDescending(static candidate => candidate.Version).FirstOrDefault();
            return ValueTask.FromResult(Result.Success(definition is null ? Maybe.None<WorkflowDefinition>() : Maybe.Some(WorkflowJson.Clone(definition))));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<int>> MaxVersionAsync(string? tenantId, WorkflowName name, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            var max = InPartition(tenantId).Where(candidate => candidate.Name == name).Select(static candidate => candidate.Version).DefaultIfEmpty(0).Max();
            return ValueTask.FromResult(Result.Success(max));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<WorkflowDefinition>>> ListAsync(string? tenantId, WorkflowName? name, WorkflowDefinitionStatus? status, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            IReadOnlyList<WorkflowDefinition> listed = InPartition(tenantId)
                .Where(candidate => (name is null || candidate.Name == name.Value) && (status is null || candidate.Status == status.Value))
                .OrderBy(static candidate => candidate.Name.Value, StringComparer.Ordinal)
                .ThenByDescending(static candidate => candidate.Version)
                .Select(WorkflowJson.Clone)
                .ToList();
            return ValueTask.FromResult(Result.Success(listed));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<WorkflowDefinitionSummary>>> ListSummariesAsync(string? tenantId, WorkflowDefinitionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        // 用到了本实现不认识的条件即失败：不读不认识的条件，也不给出放宽了的结果（ADR-0228）
        if (query.Validate(WorkflowDefinitionQuery.Revision).Error is { } unsupported)
        {
            return ValueTask.FromResult(Result.Failure<IReadOnlyList<WorkflowDefinitionSummary>>(unsupported));
        }

        lock (_lock)
        {
            IReadOnlyList<WorkflowDefinitionSummary> page = Sorted(InPartition(tenantId).Where(query.Matches), query)
                .Skip(query.Skip)
                .Take(query.Take)
                .Select(WorkflowDefinitionSummary.From)
                .ToList();
            return ValueTask.FromResult(Result.Success(page));
        }
    }

    /// <summary>
    /// 没给排序项时的排序：名字升序
    /// </summary>
    private static readonly WorkflowDefinitionSort[] _defaultSorts = [new(WorkflowDefinitionSortField.Name)];

    /// <summary>
    /// 按定义查询的排序项依次排序，再接次序键名字升序、版本降序
    /// </summary>
    /// <remarks>
    /// 名字与版本在分区里唯一，接上次序键即是全序，分页不重不漏（ADR-0102、ADR-0104）。排序项里已有的字段不再作次序键，重复的字段只按第一次出现的排，与数据库实现的 SQL 同一结果（调用方已校验重复）。文本按序数比较，状态按生命周期的序号（<see cref="WorkflowStatusOrder"/>，ADR-0103），发布时间的空值当作最小（与 <see cref="Comparer{T}.Default"/> 相同），标题按 <see cref="WorkflowSortKeys.Title"/>
    /// </remarks>
    private static IOrderedEnumerable<WorkflowDefinition> Sorted(IEnumerable<WorkflowDefinition> definitions, WorkflowDefinitionQuery query)
    {
        IOrderedEnumerable<WorkflowDefinition>? sorted = null;
        var used = new HashSet<WorkflowDefinitionSortField>();
        foreach (var sort in query.Sorts is { Count: > 0 } sorts ? sorts : _defaultSorts)
        {
            if (sort is null)
            {
                throw new ArgumentException("A workflow definition sort item is null.", nameof(query));
            }

            if (used.Add(sort.Field))
            {
                sorted = Then(definitions, sorted, sort);
            }
        }

        // 次序键：名字升序、版本降序，排序项里已有的跳过
        var ordered = sorted!;
        if (!used.Contains(WorkflowDefinitionSortField.Name))
        {
            ordered = ordered.ThenBy(static candidate => candidate.Name.Value, StringComparer.Ordinal);
        }

        return used.Contains(WorkflowDefinitionSortField.Version) ? ordered : ordered.ThenByDescending(static candidate => candidate.Version);
    }

    /// <summary>
    /// 接上一个排序项：还没有排序时开始排，否则作次级排序
    /// </summary>
    private static IOrderedEnumerable<WorkflowDefinition> Then(IEnumerable<WorkflowDefinition> definitions, IOrderedEnumerable<WorkflowDefinition>? sorted, WorkflowDefinitionSort sort) => sort.Field switch
    {
        WorkflowDefinitionSortField.Name => Order(definitions, sorted, static candidate => candidate.Name.Value, StringComparer.Ordinal, sort.Descending),
        WorkflowDefinitionSortField.Title => Order(definitions, sorted, static candidate => WorkflowSortKeys.Title(candidate.Title), StringComparer.Ordinal, sort.Descending),
        WorkflowDefinitionSortField.Version => Order(definitions, sorted, static candidate => candidate.Version, Comparer<int>.Default, sort.Descending),
        WorkflowDefinitionSortField.Status => Order(definitions, sorted, static candidate => WorkflowStatusOrder.Of(candidate.Status), Comparer<int>.Default, sort.Descending),
        WorkflowDefinitionSortField.CreatedAt => Order(definitions, sorted, static candidate => candidate.CreatedAt, Comparer<DateTimeOffset>.Default, sort.Descending),
        WorkflowDefinitionSortField.PublishedAt => Order(definitions, sorted, static candidate => candidate.PublishedAt, Comparer<DateTimeOffset?>.Default, sort.Descending),
        _ => throw new ArgumentOutOfRangeException(nameof(sort), sort.Field, $"The workflow definition sort field '{sort.Field}' is not supported."),
    };

    /// <summary>
    /// 按一个键升序或降序排：作主排序或次级排序
    /// </summary>
    private static IOrderedEnumerable<WorkflowDefinition> Order<TKey>(IEnumerable<WorkflowDefinition> definitions, IOrderedEnumerable<WorkflowDefinition>? sorted, Func<WorkflowDefinition, TKey> key, IComparer<TKey> comparer, bool descending)
        => sorted is null
            ? (descending ? definitions.OrderByDescending(key, comparer) : definitions.OrderBy(key, comparer))
            : (descending ? sorted.ThenByDescending(key, comparer) : sorted.ThenBy(key, comparer));

    /// <inheritdoc />
    public ValueTask<Result<long>> CountAsync(string? tenantId, WorkflowDefinitionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        // 用到了本实现不认识的条件即失败：不读不认识的条件，也不给出放宽了的结果（ADR-0228）
        if (query.Validate(WorkflowDefinitionQuery.Revision).Error is { } unsupported)
        {
            return ValueTask.FromResult(Result.Failure<long>(unsupported));
        }

        lock (_lock)
        {
            return ValueTask.FromResult(Result.Success(InPartition(tenantId).LongCount(query.Matches)));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result> SaveAsync(string? tenantId, IReadOnlyList<WorkflowDefinition> definitions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        foreach (var definition in definitions)
        {
            PartitionKey.EnsureOwned(tenantId, definition.TenantId, "definition", definition.Id);
        }

        lock (_lock)
        {
            // 容量按整批判定：要么全部写入，要么一条不写
            var added = definitions.Select(definition => PartitionKey.In(tenantId, definition.Id)).Distinct().Count(key => !_definitions.ContainsKey(key));
            if (_definitions.Count + added > _capacity)
            {
                return ValueTask.FromResult(Result.Failure(Error.Unavailable(WorkflowErrors.StoreCapacity, WorkflowMessages.StoreCapacity, MessageArguments.Of(("store", "definitions"), ("limit", _capacity)))));
            }

            foreach (var definition in definitions)
            {
                _definitions[PartitionKey.In(tenantId, definition.Id)] = WorkflowJson.Clone(definition);
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
                _definitions.Remove(PartitionKey.In(tenantId, id));
            }

            return ValueTask.FromResult(Result.Success());
        }
    }

    /// <summary>
    /// 一个分区里的定义（调用方持锁）
    /// </summary>
    private IEnumerable<WorkflowDefinition> InPartition(string? tenantId)
    {
        var partition = PartitionKey.Of(tenantId);
        return _definitions.Where(entry => string.Equals(entry.Key.Tenant, partition, StringComparison.Ordinal)).Select(static entry => entry.Value);
    }
}
