// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authorization.Conditions;

/// <summary>
/// 条件求值器
/// </summary>
/// <remarks>
/// 组内且、组间或；子句「无法判定」按不命中计且取反不翻转，「待定」让所在组待定。
/// 条件集：任一组命中即命中；否则任一组待定即待定；否则不命中。无条件（空集）恒为命中。
/// </remarks>
public interface IConditionEvaluator
{
    /// <summary>
    /// 求值
    /// </summary>
    /// <param name="set">编译后的条件集</param>
    /// <param name="source">属性来源（只取被引用到的属性）</param>
    /// <param name="explain">是否输出每组每个子句的说明；为否时短路求值、不分配说明</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>求值结果</returns>
    ValueTask<ConditionEvaluationResult> EvaluateAsync(
        CompiledConditionSet set,
        IConditionAttributeSource source,
        bool explain = false,
        CancellationToken cancellationToken = default);
}
