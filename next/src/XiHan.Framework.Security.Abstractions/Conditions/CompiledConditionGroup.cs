// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Conditions;

/// <summary>
/// 编译后的条件组。
/// </summary>
/// <param name="Key">组标识。</param>
/// <param name="Clauses">子句（至少一个）。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record CompiledConditionGroup(string? Key, IReadOnlyList<CompiledConditionClause> Clauses);
