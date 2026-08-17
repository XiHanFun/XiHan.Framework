// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Docs.Mcp.Search;

/// <summary>
/// 带权重的查询词条
/// </summary>
/// <param name="Term">词条</param>
/// <param name="Weight">权重，用户原始查询词为 1.0，同义词扩展出的为 0.5</param>
public readonly record struct WeightedTerm(string Term, double Weight);
