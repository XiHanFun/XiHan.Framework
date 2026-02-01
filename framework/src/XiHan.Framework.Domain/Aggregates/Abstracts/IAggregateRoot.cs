#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IAggregateRoot
// Guid:85a10cca-e110-4207-ab0a-2c1a68bb7b4b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/02/20 04:24:03
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
