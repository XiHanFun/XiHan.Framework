#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DeletionDtoBase
// Guid:89a73d4d-92ad-4ee1-98a7-8355aaa8e1a0
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/11/16 2:43:41
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Application.Dtos;

/// <summary>
/// 删除 DTO 基类
/// </summary>
public abstract class DeletionDtoBase
{
}

/// <summary>
/// 泛型主键删除 DTO 基类
/// </summary>
/// <typeparam name="TKey"></typeparam>
public abstract class DeletionDtoBase<TKey> : DeletionDtoBase
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 主键
    /// </summary>
    public virtual TKey BasicId { get; set; } = default!;
}
