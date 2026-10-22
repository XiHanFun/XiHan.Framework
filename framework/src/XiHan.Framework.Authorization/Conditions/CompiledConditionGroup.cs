// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authorization.Conditions;

/// <summary>
/// 编译后的条件组
/// </summary>
/// <param name="Key">组标识</param>
/// <param name="Clauses">子句（至少一个）</param>
public sealed record CompiledConditionGroup(string? Key, IReadOnlyList<CompiledConditionClause> Clauses);
