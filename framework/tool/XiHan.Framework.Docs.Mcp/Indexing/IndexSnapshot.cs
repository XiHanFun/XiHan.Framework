// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Docs.Mcp.Sources;

namespace XiHan.Framework.Docs.Mcp.Indexing;

/// <summary>
/// 一次重建产出的索引快照，三者互相自洽且整体不可变
/// </summary>
/// <param name="Sections">全部章节，下标与倒排索引中的 SectionId 对应</param>
/// <param name="Index">倒排索引</param>
/// <param name="Files">被索引的文件列表</param>
/// <remarks>
/// 章节列表与倒排索引必须成对取用，不能分两次读：倒排索引里的 SectionId 只是章节列表的下标，
/// 一旦另一次重建插在两次读取之间，落在范围内的 SectionId 会解析到另一个章节——
/// 输出的是一段真实正文，挂着的却是错误的文件路径与行号。
/// 这正是本工具存在的意义所要消灭的那类伪造出处，而且失败是静默的，
/// 所以用一个快照对象把三者绑在一起，让这种读法在类型层面就写不出来。
/// </remarks>
public sealed record IndexSnapshot(
    IReadOnlyList<DocSection> Sections,
    BigramIndex Index,
    IReadOnlyList<DocFile> Files);
