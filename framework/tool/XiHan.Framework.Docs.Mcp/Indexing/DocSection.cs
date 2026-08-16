// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Docs.Mcp.Sources;

namespace XiHan.Framework.Docs.Mcp.Indexing;

/// <summary>
/// 文档中的一个章节，检索与返回的最小单位
/// </summary>
/// <param name="RelativePath">所属文件相对仓库根的路径</param>
/// <param name="Source">来源分类</param>
/// <param name="DocumentTitle">所属文档的一级标题</param>
/// <param name="Heading">本章节的二级标题，前言章节为「概述」</param>
/// <param name="TitlePath">标题路径，形如「事件总线 &gt; 本地事件还是分布式事件」</param>
/// <param name="Content">章节正文原文，不含标题行</param>
/// <param name="StartLine">起始行号，从 1 开始</param>
/// <param name="EndLine">结束行号，从 1 开始，含此行</param>
public sealed record DocSection(
    string RelativePath,
    DocSourceKind Source,
    string DocumentTitle,
    string Heading,
    string TitlePath,
    string Content,
    int StartLine,
    int EndLine);
