// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Domain.Events;

/// <summary>
/// 收集领域事件的聚合。持久化子系统在提交后取出并派发，然后清空。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IHasDomainEvents
{
    /// <summary>
    /// 尚未派发的领域事件。
    /// </summary>
    IReadOnlyList<IDomainEvent> DomainEvents { get; }

    /// <summary>
    /// 清空已派发的事件。
    /// </summary>
    void ClearDomainEvents();
}
