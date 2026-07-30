// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Domain.Repositories.Models;

/// <summary>
/// 审计查询参数
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
public sealed class AuditQueryOptions<TKey>
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 创建者主键
    /// </summary>
    public TKey? CreatedId { get; init; }

    /// <summary>
    /// 最后修改者主键
    /// </summary>
    public TKey? ModifiedId { get; init; }

    /// <summary>
    /// 删除者主键
    /// </summary>
    public TKey? DeletedId { get; init; }

    /// <summary>
    /// 创建时间起始（包含）
    /// </summary>
    public DateTimeOffset? CreatedTimeStart { get; init; }

    /// <summary>
    /// 创建时间结束（包含）
    /// </summary>
    public DateTimeOffset? CreatedTimeEnd { get; init; }

    /// <summary>
    /// 最后修改时间起始（包含）
    /// </summary>
    public DateTimeOffset? ModifiedTimeStart { get; init; }

    /// <summary>
    /// 最后修改时间结束（包含）
    /// </summary>
    public DateTimeOffset? ModifiedTimeEnd { get; init; }

    /// <summary>
    /// 删除时间起始（包含）
    /// </summary>
    public DateTimeOffset? DeletedTimeStart { get; init; }

    /// <summary>
    /// 删除时间结束（包含）
    /// </summary>
    public DateTimeOffset? DeletedTimeEnd { get; init; }

    /// <summary>
    /// 是否包含软删除实体
    /// 默认 <c>false</c> 表示仅返回未删除实体
    /// </summary>
    public bool IncludeSoftDeleted { get; init; }

    /// <summary>
    /// 是否只返回软删除实体
    /// 当 <see cref="OnlySoftDeleted"/> 为 <c>true</c> 时，<see cref="IncludeSoftDeleted"/> 将被忽略
    /// </summary>
    public bool OnlySoftDeleted { get; init; }
}
