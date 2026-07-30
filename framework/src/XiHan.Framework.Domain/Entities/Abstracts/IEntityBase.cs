// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Domain.Entities.Abstracts;

/// <summary>
/// 实体基类接口
/// </summary>
public interface IEntityBase
{
    /// <summary>
    /// 版本控制标识，用于处理并发
    /// </summary>
    long RowVersion { get; set; }
}

/// <summary>
/// 泛型主键实体基类接口
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
public interface IEntityBase<TKey> : IEntityBase
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 主键
    /// </summary>
    TKey BasicId { get; }

    /// <summary>
    /// 检查实体是否为临时实体（尚未持久化）
    /// </summary>
    /// <returns>如果是临时实体返回 true，否则返回 false</returns>
    bool IsTransient();
}
