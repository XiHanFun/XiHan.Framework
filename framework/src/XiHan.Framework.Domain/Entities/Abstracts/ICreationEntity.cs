#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ICreationAudited
// Guid:abc12345-1234-1234-1234-123456789abc
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/12 16:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Domain.Entities.Abstracts;

/// <summary>
/// 创建审计接口
/// 用于记录实体的创建时间
/// </summary>
public interface ICreationEntity
{
    /// <summary>
    /// 创建时间
    /// </summary>
    DateTimeOffset CreationTime { get; set; }
}

/// <summary>
/// 创建审计接口（带创建者）
/// 用于记录实体的创建时间和创建者
/// </summary>
/// <typeparam name="TUserKey">用户主键类型</typeparam>
public interface ICreationEntity<TUserKey> : ICreationEntity
    where TUserKey : IEquatable<TUserKey>
{
    /// <summary>
    /// 创建者ID
    /// </summary>
    TUserKey? CreatorId { get; set; }
}
