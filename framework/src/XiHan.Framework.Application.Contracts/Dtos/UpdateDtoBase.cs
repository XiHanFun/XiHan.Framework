// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Application.Contracts.Dtos;

/// <summary>
/// 更新 DTO 基类
/// </summary>
public abstract class UpdateDtoBase
{
}

/// <summary>
/// 泛型主键更新 DTO 基类
/// </summary>
public abstract class UpdateDtoBase<TKey> : UpdateDtoBase
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 主键
    /// </summary>
    public virtual TKey BasicId { get; set; } = default!;
}
