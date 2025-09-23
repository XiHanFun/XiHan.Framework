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
public abstract class ModificationEntityBase : IModificationEntity
{
    /// <summary>
    /// 构造函数
    /// </summary>
    protected ModificationEntityBase()
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
public abstract class ModificationEntityBase<TKey> : ModificationEntityBase, IModificationEntity<TKey>
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    protected ModificationEntityBase() : base()
    {
    }

    /// <summary>
    /// 修改者Id
    /// </summary>
    public virtual TKey? ModifierId { get; set; }

    /// <summary>
    /// 修改人
    /// </summary>
    public virtual string? Modifier { get; set; }
}
