#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DtoBase
// Guid:7f821e80-1dfc-4ca3-8861-1f5b84c6bce6
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/11/16 02:24:42
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Application.Contracts.Dtos;

/// <summary>
/// DTO 基类
/// </summary>
public abstract class DtoBase
{
}

/// <summary>
/// 泛型主键 DTO 基类
/// </summary>
/// <typeparam name="TKey"></typeparam>
public abstract class DtoBase<TKey> : DtoBase
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 主键
    /// </summary>
    public virtual TKey BasicId { get; set; } = default!;
}
