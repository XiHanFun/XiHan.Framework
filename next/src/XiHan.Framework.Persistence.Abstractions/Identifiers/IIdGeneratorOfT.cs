// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Identifiers;

/// <summary>
/// 标识生成器。
/// </summary>
/// <typeparam name="TKey">标识类型。</typeparam>
[ApiLevel(Stability.Stable, "5.0")]
public interface IIdGenerator<out TKey>
    where TKey : notnull
{
    /// <summary>
    /// 生成一个新标识。
    /// </summary>
    TKey NewId();
}
