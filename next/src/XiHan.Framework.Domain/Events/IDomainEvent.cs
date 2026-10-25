// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Domain.Events;

/// <summary>
/// 领域事件：聚合内已经发生的事实。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IDomainEvent
{
    /// <summary>
    /// 发生时间（UTC）。
    /// </summary>
    DateTimeOffset OccurredAt { get; }
}
