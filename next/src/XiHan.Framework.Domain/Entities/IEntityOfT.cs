// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Domain.Entities;

/// <summary>
/// 有标识的实体。
/// </summary>
/// <typeparam name="TKey">标识类型。</typeparam>
[ApiLevel(Stability.Stable, "5.0")]
public interface IEntity<out TKey>
    where TKey : notnull
{
    /// <summary>
    /// 标识。
    /// </summary>
    TKey Id { get; }
}
