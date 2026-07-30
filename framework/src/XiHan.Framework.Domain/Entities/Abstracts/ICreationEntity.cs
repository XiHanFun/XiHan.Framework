// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Domain.Entities.Abstracts;

/// <summary>
/// 创建审计接口
/// 用于记录实体的创建时间
/// </summary>
public interface ICreationEntity
{
    /// <summary>
    /// 创建时间
    /// </summary>
    DateTimeOffset CreatedTime { get; set; }
}

/// <summary>
/// 创建审计接口（带创建者）
/// 用于记录实体的创建时间和创建者
/// </summary>
/// <typeparam name="TKey">用户主键类型</typeparam>
public interface ICreationEntity<TKey> : ICreationEntity
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 创建者唯一标识
    /// </summary>
    TKey? CreatedId { get; set; }

    /// <summary>
    /// 创建人
    /// </summary>
    string? CreatedBy { get; set; }
}
