// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
