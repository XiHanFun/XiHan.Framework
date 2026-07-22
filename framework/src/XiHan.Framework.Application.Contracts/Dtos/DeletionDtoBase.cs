// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Application.Contracts.Dtos;

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
