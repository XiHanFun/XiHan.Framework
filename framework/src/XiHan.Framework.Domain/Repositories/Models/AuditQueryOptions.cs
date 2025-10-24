#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AuditQueryOptions
// Guid:2a9c68f0-2dc4-4c80-8f46-0d5b3f1e4a1d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/25 05:40:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
    public TKey? CreatorId { get; init; }

    /// <summary>
    /// 最后修改者主键
    /// </summary>
    public TKey? ModifierId { get; init; }

    /// <summary>
    /// 删除者主键
    /// </summary>
    public TKey? DeleterId { get; init; }

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
