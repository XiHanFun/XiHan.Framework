// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Domain.Entities;

/// <summary>
/// 乐观并发戳：持久化子系统在新增与更新时刷新，更新时比对
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IHasConcurrencyStamp
{
    /// <summary>
    /// 并发戳
    /// </summary>
    /// <remarks>
    /// 已持久化的实体一定有值，所以不可空；新建实体在新增前以 <see cref="string.Empty"/> 占位，新增时由持久化生成（ADR-0145）
    /// </remarks>
    string ConcurrencyStamp { get; set; }
}
