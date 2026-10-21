// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Domain;

/// <summary>
/// 乐观并发戳：持久化子系统在新增与更新时刷新，更新时比对。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IHasConcurrencyStamp
{
    /// <summary>
    /// 并发戳。
    /// </summary>
    string ConcurrencyStamp { get; set; }
}
