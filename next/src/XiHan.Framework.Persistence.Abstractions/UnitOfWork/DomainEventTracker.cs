// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Events;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.UnitOfWork;

/// <summary>
/// 工作单元里写过的聚合。
/// </summary>
/// <remarks>
/// 仓储在新增 / 更新 / 删除成功后登记实体，实现 <see cref="IHasDomainEvents"/> 的按首次写入顺序记一次（按引用去重）；Transaction 阶段提交前取出带有未派发事件的聚合交给 <see cref="IDomainEventDispatcher"/>。工作单元是作用域服务，不需要并发保护。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class DomainEventTracker
{
    /// <summary>
    /// 已登记的聚合，按首次写入顺序。
    /// </summary>
    private readonly List<IHasDomainEvents> _aggregates = [];

    /// <summary>
    /// 已登记的聚合引用，用于去重。
    /// </summary>
    private readonly HashSet<IHasDomainEvents> _seen = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// 已登记的聚合数。
    /// </summary>
    public int Count => _aggregates.Count;

    /// <summary>
    /// 登记一个写过的实体。
    /// </summary>
    /// <remarks>
    /// 不收集领域事件的实体与已登记的同一实例都忽略。
    /// </remarks>
    public void Track(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (entity is IHasDomainEvents aggregate && _seen.Add(aggregate))
        {
            _aggregates.Add(aggregate);
        }
    }

    /// <summary>
    /// 带有未派发事件的聚合，按首次写入顺序。
    /// </summary>
    public IReadOnlyList<IHasDomainEvents> Pending() => _aggregates.Where(static a => a.DomainEvents.Count > 0).ToList();

    /// <summary>
    /// 清空登记（提交或回滚后）。
    /// </summary>
    public void Clear()
    {
        _aggregates.Clear();
        _seen.Clear();
    }
}
