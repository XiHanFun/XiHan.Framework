// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Conditions;

/// <summary>
/// 条件求值器。
/// </summary>
/// <remarks>
/// 组内且、组间或；子句无法判定按不命中计且取反不翻转，待定让所在组待定。条件集任一组命中即命中；否则任一组待定即待定；否则不命中。无条件（空集）恒为命中。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IConditionEvaluator
{
    /// <summary>
    /// 求值。
    /// </summary>
    /// <param name="conditions">编译后的条件集。</param>
    /// <param name="source">属性来源（只取被引用到的属性）。</param>
    /// <param name="explain">是否输出每组每个子句的说明；为否时短路求值、不分配说明。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    ValueTask<ConditionEvaluationResult> EvaluateAsync(CompiledConditionSet conditions, IConditionAttributeSource source, bool explain, CancellationToken cancellationToken);
}
