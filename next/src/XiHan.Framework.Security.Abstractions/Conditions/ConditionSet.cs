// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Conditions;

/// <summary>
/// 条件集。
/// </summary>
/// <remarks>
/// 任一组命中，条件集就命中。没有任何非空组的条件集是"无条件"，求值恒为命中；需要拒绝空条件集的场景（如访问策略）由调用方在保存时拦下。
/// </remarks>
/// <param name="Groups">条件组。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record ConditionSet(IReadOnlyList<ConditionGroup> Groups)
{
    /// <summary>
    /// 无条件。
    /// </summary>
    public static ConditionSet Empty { get; } = new([]);
}
