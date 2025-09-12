#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ModificationEntityBase
// Guid:0d428316-97ce-4820-926b-f07813a3a305
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/13 3:07:51
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Entities.Abstracts;

namespace XiHan.Framework.Domain.Entities;

/// <summary>
/// 修改审计实体基类
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
public abstract class ModificationEntityBase<TKey> : CreationEntityBase<TKey>, IModificationEntity
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    protected ModificationEntityBase()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="id">实体主键</param>
    protected ModificationEntityBase(TKey id) : base(id)
    {
    }

    /// <summary>
    /// 修改时间
    /// </summary>
    public virtual DateTimeOffset? ModificationTime { get; set; }
}

/// <summary>
/// 修改审计实体基类（带修改者）
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
/// <typeparam name="TUserKey">用户主键类型</typeparam>
public abstract class ModificationEntityBase<TKey, TUserKey> : CreationEntityBase<TKey, TUserKey>, IModificationEntity<TUserKey>
    where TKey : IEquatable<TKey>
    where TUserKey : IEquatable<TUserKey>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    protected ModificationEntityBase()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="id">实体主键</param>
    protected ModificationEntityBase(TKey id) : base(id)
    {
    }

    /// <summary>
    /// 修改时间
    /// </summary>
    public virtual DateTimeOffset? ModificationTime { get; set; }

    /// <summary>
    /// 修改者ID
    /// </summary>
    public virtual TUserKey? ModifierId { get; set; }
}
