// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Events.Abstracts;

namespace XiHan.Framework.Domain.Aggregates.Abstracts;

/// <summary>
/// 聚合根接口
/// </summary>
public interface IAggregateRoot : IFullAuditedEntity, IDomainEventsManager
{
}

/// <summary>
/// 泛型主键聚合根接口
/// </summary>
/// <typeparam name="TKey"></typeparam>
public interface IAggregateRoot<TKey> : IAggregateRoot, IFullAuditedEntity<TKey>
    where TKey : IEquatable<TKey>
{
}
