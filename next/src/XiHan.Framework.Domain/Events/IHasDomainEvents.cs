// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Domain.Events;

/// <summary>
/// 收集领域事件的聚合
/// </summary>
/// <remarks>
/// 仓储写入成功后把聚合登记到工作单元；Transaction 阶段在处理器成功后、提交前把带有未派发事件的聚合交给领域事件派发器，派发器发布成功后调用 <see cref="ClearDomainEvents"/> 清空，发布失败则回滚、事件留在聚合上（ADR-0025）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public interface IHasDomainEvents
{
    /// <summary>
    /// 尚未派发的领域事件
    /// </summary>
    IReadOnlyList<IDomainEvent> DomainEvents { get; }

    /// <summary>
    /// 清空已派发的事件
    /// </summary>
    void ClearDomainEvents();
}
