// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Conditions;

/// <summary>
/// 条件子句：一个属性、一个运算符、一个条件值，可整体取反。
/// </summary>
/// <param name="AttributeName">属性名（登记名或别名，不区分大小写）。</param>
/// <param name="Operator">运算符。</param>
/// <param name="Value">条件值原文：标量直接写；集合写 JSON 数组或以逗号（含全角逗号）、换行分隔；区间写 <c>a,b</c>、<c>[a,b]</c> 或 <c>{"min":a,"max":b}</c>；为空 / 不为空可留空。</param>
/// <param name="IsNegated">是否对运算结果取反（只翻转命中与不命中，无法判定与待定不翻转）。</param>
/// <param name="Key">调用方给的标识（如条件行主键），用于说明与错误定位。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record ConditionClause(string AttributeName, ConditionOperator Operator, string? Value, bool IsNegated = false, string? Key = null);
