// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Domain;

/// <summary>
/// 软删除：持久化子系统把删除改为打标，并默认过滤已删除实体。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface ISoftDeletable
{
    /// <summary>
    /// 是否已删除。
    /// </summary>
    bool IsDeleted { get; set; }

    /// <summary>
    /// 删除时间（UTC）。
    /// </summary>
    DateTimeOffset? DeletedAt { get; set; }
}
