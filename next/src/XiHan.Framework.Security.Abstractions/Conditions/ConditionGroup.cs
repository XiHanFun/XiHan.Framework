// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Conditions;

/// <summary>
/// 条件组：组内子句全部命中，这组才命中。
/// </summary>
/// <param name="Clauses">子句；没有子句的组不参与求值。</param>
/// <param name="Key">调用方给的标识（如分组号），用于说明。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record ConditionGroup(IReadOnlyList<ConditionClause> Clauses, string? Key = null);
