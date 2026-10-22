// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authorization.Conditions;

/// <summary>
/// 条件组：组内子句全部命中，这组才命中
/// </summary>
/// <param name="Clauses">子句；没有子句的组不参与求值</param>
/// <param name="Key">调用方给的标识（如分组号），用于说明</param>
public sealed record ConditionGroup(IReadOnlyList<ConditionClause> Clauses, string? Key = null);
