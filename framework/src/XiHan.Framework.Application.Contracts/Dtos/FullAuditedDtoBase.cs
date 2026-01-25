#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:FullAuditedDtoBase
// Guid:c8263288-f140-4aef-88c0-26744042e7d2
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/11/16 2:36:35
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Application.Contracts.Dtos;

/// <summary>
/// 审计 DTO 基类
/// </summary>
public abstract class FullAuditedDtoBase
{
    /// <summary>
    /// 创建时间
    /// </summary>
    public virtual DateTimeOffset[]? CreatedTime { get; set; }

    /// <summary>
    /// 修改时间
    /// </summary>
    public virtual DateTimeOffset[]? ModifiedTime { get; set; }

    /// <summary>
    /// 软删除标记
    /// </summary>
    public virtual bool IsDeleted { get; set; }

    /// <summary>
    /// 删除时间
    /// </summary>
    public virtual DateTimeOffset[]? DeletedTime { get; set; }
}

/// <summary>
/// 泛型主键审计 DTO 基类
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
public abstract class FullAuditedDtoBase<TKey> : FullAuditedDtoBase
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 主键
    /// </summary>
    public virtual TKey BasicId { get; set; } = default!;

    /// <summary>
    /// 创建者唯一标识
    /// </summary>
    public virtual TKey? CreatedId { get; set; }

    /// <summary>
    /// 创建人
    /// </summary>
    public virtual string? CreatedBy { get; set; }

    /// <summary>
    /// 修改者唯一标识
    /// </summary>
    public virtual TKey? ModifiedId { get; set; }

    /// <summary>
    /// 修改人
    /// </summary>
    public virtual string? ModifiedBy { get; set; }

    /// <summary>
    /// 删除者唯一标识
    /// </summary>
    public virtual TKey? DeletedId { get; set; }

    /// <summary>
    /// 删除者
    /// </summary>
    public virtual string? DeletedBy { get; set; }
}
