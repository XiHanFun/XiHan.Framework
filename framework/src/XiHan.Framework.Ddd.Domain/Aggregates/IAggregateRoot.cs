#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IAggregateRoot
// Guid:85a10cca-e110-4207-ab0a-2c1a68bb7b4b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/20 4:24:03
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Ddd.Domain.Events;
using XiHan.Framework.Ddd.Domain.Shared.Entities;

namespace XiHan.Framework.Ddd.Domain.Aggregates;

/// <summary>
/// 聚合根接口
/// </summary>
public interface IAggregateRoot : IEntityBase, IDomainEvents
{
}

/// <summary>
/// 泛型主键聚合根接口
/// </summary>
/// <typeparam name="TKey"></typeparam>
public interface IAggregateRoot<TKey> : IAggregateRoot, IEntityBase<TKey>, IDomainEvents
    where TKey : IEquatable<TKey>
{
}
