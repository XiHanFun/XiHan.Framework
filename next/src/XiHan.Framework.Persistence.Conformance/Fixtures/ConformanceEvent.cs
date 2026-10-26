// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Events;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Conformance.Fixtures;

/// <summary>
/// 套件领域事件：带名字，由 <see cref="ConformanceOrder.Raise"/> 记录。
/// </summary>
/// <param name="Name">事件名。</param>
/// <param name="OccurredAt">发生时间。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record ConformanceEvent(string Name, DateTimeOffset OccurredAt) : IDomainEvent;
