// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Docs.Mcp.Indexing;

namespace XiHan.Framework.Docs.Mcp.Search;

/// <summary>
/// 一条检索结果
/// </summary>
/// <param name="Section">命中的章节</param>
/// <param name="Score">得分，供调用方判断可信度</param>
public sealed record SearchHit(DocSection Section, double Score);
