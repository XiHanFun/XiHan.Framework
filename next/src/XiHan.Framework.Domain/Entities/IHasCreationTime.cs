// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Domain.Entities;

/// <summary>
/// 创建时间：持久化子系统在新增时写入。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IHasCreationTime
{
    /// <summary>
    /// 创建时间（UTC）。
    /// </summary>
    DateTimeOffset CreatedAt { get; set; }
}
