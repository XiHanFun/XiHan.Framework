// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Events;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Conformance.Fixtures;

/// <summary>
/// 套件派发器：记录每批交来的事件名与租户并清空聚合的缓冲；设置 <see cref="Failure"/> 后改为返回该失败且不清空，用于验证回滚。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class RecordingDomainEventDispatcher : IDomainEventDispatcher
{
    /// <summary>
    /// 已派发的批次：每批的事件名（按聚合与记录顺序）与租户。
    /// </summary>
    private readonly List<(IReadOnlyList<string> Events, string? TenantId)> _batches = [];

    /// <summary>
    /// 已派发的批次。
    /// </summary>
    public IReadOnlyList<(IReadOnlyList<string> Events, string? TenantId)> Batches => _batches;

    /// <summary>
    /// 设置后每次派发都返回该失败。
    /// </summary>
    public Error? Failure { get; set; }

    /// <inheritdoc />
    public ValueTask<Result> DispatchAsync(IReadOnlyList<IHasDomainEvents> aggregates, string? tenantId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(aggregates);
        if (Failure is { } failure)
        {
            return ValueTask.FromResult<Result>(failure);
        }

        var names = aggregates.SelectMany(static a => a.DomainEvents).Select(static e => e is ConformanceEvent named ? named.Name : e.GetType().Name).ToList();
        _batches.Add((names, tenantId));
        foreach (var aggregate in aggregates)
        {
            aggregate.ClearDomainEvents();
        }

        return ValueTask.FromResult(Result.Success());
    }
}
