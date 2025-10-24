#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IDeletionAudited
// Guid:8faf9286-4ba2-4079-875a-98cb1a82faea
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/13 2:56:03
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Domain.Entities.Abstracts;

/// <summary>
/// 软删除接口
/// 用于标记实体是否已被软删除
/// </summary>
public interface ISoftDelete
{
    /// <summary>
    /// 软删除标记
    /// </summary>
    bool IsDeleted { get; set; }
}

/// <summary>
/// 删除审计接口
/// 用于记录实体的删除时间
/// </summary>
public interface IDeletionEntity : ISoftDelete
{
    /// <summary>
    /// 删除时间
    /// </summary>
    DateTimeOffset? DeletionTime { get; set; }
}

/// <summary>
/// 删除审计接口（带删除者）
/// 用于记录实体的删除时间和删除者
/// </summary>
/// <typeparam name="TKey">用户主键类型</typeparam>
public interface IDeletionEntity<TKey> : IDeletionEntity
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 删除者唯一标识
    /// </summary>
    TKey? DeleterId { get; set; }

    /// <summary>
    /// 删除者
    /// </summary>
    string? Deleter { get; set; }
}
