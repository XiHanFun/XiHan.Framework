#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IFullAuditedAggregateRoot
// Guid:a0b1e3ff-cfc0-42ff-9def-d102b162f0bb
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/24 6:55:43
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Entities.Abstracts;

namespace XiHan.Framework.Domain.Aggregates.Abstracts;

/// <summary>
/// 完整审计聚合根接口
/// </summary>
public interface IFullAuditedAggregateRoot<TKey> : IAggregateRoot<TKey>, IFullAuditedEntity<TKey>
    where TKey : IEquatable<TKey>
{
}
