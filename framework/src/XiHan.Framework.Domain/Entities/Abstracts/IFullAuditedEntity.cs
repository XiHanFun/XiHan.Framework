#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IFullAudited
// Guid:jkl12345-1234-1234-1234-123456789jkl
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/12 16:36:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Domain.Entities.Abstracts;

/// <summary>
/// 完整审计接口
/// 包含创建、修改、删除的所有审计信息
/// </summary>
public interface IFullAuditedEntity : IEntityBase, ICreationEntity, IModificationEntity, IDeletionEntity
{
}

/// <summary>
/// 完整审计接口（带用户）
/// 包含创建、修改、删除的所有审计信息和对应的用户ID
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
/// <typeparam name="TUserKey">用户主键类型</typeparam>
public interface IFullAuditedEntity<TKey, TUserKey> : IEntityBase<TKey>, ICreationEntity<TUserKey>, IModificationEntity<TUserKey>, IDeletionEntity<TUserKey>
    where TKey : IEquatable<TKey>
    where TUserKey : IEquatable<TUserKey>
{
}
