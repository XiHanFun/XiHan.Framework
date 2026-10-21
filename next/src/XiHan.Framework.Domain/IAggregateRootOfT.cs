// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Domain;

/// <summary>
/// 聚合根：一致性边界，领域事件的来源。
/// </summary>
/// <typeparam name="TKey">标识类型。</typeparam>
[ApiLevel(Stability.Stable, "5.0")]
public interface IAggregateRoot<out TKey> : IEntity<TKey>, IHasDomainEvents
    where TKey : notnull;
