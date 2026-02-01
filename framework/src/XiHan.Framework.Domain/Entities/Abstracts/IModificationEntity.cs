#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IModificationEntity
// Guid:def12345-1234-1234-1234-123456789def
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/12 16:32:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Domain.Entities.Abstracts;

/// <summary>
/// 修改审计接口
/// 用于记录实体的最后修改时间
/// </summary>
public interface IModificationEntity
{
    /// <summary>
    /// 修改时间
    /// </summary>
    DateTimeOffset? ModifiedTime { get; set; }
}

/// <summary>
/// 修改审计接口（带修改者）
/// 用于记录实体的最后修改时间和修改者
/// </summary>
/// <typeparam name="TKey">用户主键类型</typeparam>
public interface IModificationEntity<TKey> : IModificationEntity
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 修改者唯一标识
    /// </summary>
    TKey? ModifiedId { get; set; }

    /// <summary>
    /// 修改人
    /// </summary>
    string? ModifiedBy { get; set; }
}
