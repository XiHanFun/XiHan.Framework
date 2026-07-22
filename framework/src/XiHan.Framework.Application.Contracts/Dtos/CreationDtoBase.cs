// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Application.Contracts.Dtos;

/// <summary>
/// 创建 DTO 基类
/// </summary>
public abstract class CreationDtoBase
{
}

/// <summary>
/// 泛型主键创建 DTO 基类
/// </summary>
public abstract class CreationDtoBase<TKey> : CreationDtoBase
    where TKey : IEquatable<TKey>
{
}
