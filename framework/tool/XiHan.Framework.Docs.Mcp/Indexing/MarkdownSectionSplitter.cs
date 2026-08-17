// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;
using XiHan.Framework.Docs.Mcp.Sources;

namespace XiHan.Framework.Docs.Mcp.Indexing;

/// <summary>
/// 把一篇 Markdown 切分为若干章节
/// </summary>
/// <remarks>
/// 按一级与二级标题切分，三级及更深标题并入所属章节以避免切得过碎。
/// 关键约束：必须跳过代码围栏内部的井号——这批文档中大量存在 bash 的注释与 C# 的 #region，
/// 无脑按行首井号切分会把代码块切成假章节。
/// </remarks>
public static class MarkdownSectionSplitter
{
    /// <summary>
    /// 单个章节正文的字符数上限，超过则按空行二次切分
    /// </summary>
    private const int MaxSectionLength = 4000;

    /// <summary>
    /// 「概述」章节的固定标题
    /// </summary>
    private const string PreambleHeading = "概述";

    /// <summary>
    /// 切分 Markdown 文本
    /// </summary>
    /// <param name="relativePath">相对仓库根的路径</param>
    /// <param name="source">来源分类</param>
    /// <param name="markdown">Markdown 原文</param>
    /// <returns>章节列表，正文全为空白时返回空集合</returns>
    public static IReadOnlyList<DocSection> Split(string relativePath, DocSourceKind source, string markdown)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        ArgumentNullException.ThrowIfNull(markdown);

        var lines = markdown.ReplaceLineEndings("\n").Split('\n');
        var startIndex = SkipFrontMatter(lines);
        var documentTitle = Path.GetFileNameWithoutExtension(relativePath);

        var blocks = new List<(string Heading, int StartLine, List<string> Body)>();
        var currentHeading = PreambleHeading;
        var currentStartLine = startIndex + 1;
        var currentBody = new List<string>();
        var fence = string.Empty;

        for (var i = startIndex; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();

            if (fence.Length > 0)
            {
                // 处于代码围栏内：只判断是否闭合，绝不识别标题
                if (trimmed.StartsWith(fence, StringComparison.Ordinal))
                {
                    fence = string.Empty;
                }

                currentBody.Add(line);
                continue;
            }

            var opening = ReadFenceMarker(trimmed);
            if (opening.Length > 0)
            {
                fence = opening;
                currentBody.Add(line);
                continue;
            }

            if (TryReadHeading(trimmed, out var level, out var heading))
            {
                if (level == 1)
                {
                    documentTitle = heading;
                    continue;
                }

                blocks.Add((currentHeading, currentStartLine, currentBody));
                currentHeading = heading;
                currentStartLine = i + 1;
                currentBody = [];
                continue;
            }

            currentBody.Add(line);
        }

        blocks.Add((currentHeading, currentStartLine, currentBody));

        var sections = new List<DocSection>();
        foreach (var block in blocks)
        {
            var content = string.Join("\n", block.Body).Trim();
            if (content.Length == 0)
            {
                continue;
            }

            AppendSections(sections, relativePath, source, documentTitle, block.Heading, content, block.StartLine, block.Body.Count);
        }

        return sections;
    }

    /// <summary>
    /// 跳过文件开头的 YAML 前置元数据块，返回正文起始行下标
    /// </summary>
    private static int SkipFrontMatter(string[] lines)
    {
        if (lines.Length == 0 || lines[0].Trim() != "---")
        {
            return 0;
        }

        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "---")
            {
                return i + 1;
            }
        }

        return 0;
    }

    /// <summary>
    /// 读取代码围栏起始标记，非围栏行返回空串
    /// </summary>
    private static string ReadFenceMarker(string trimmed)
    {
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return "```";
        }

        return trimmed.StartsWith("~~~", StringComparison.Ordinal) ? "~~~" : string.Empty;
    }

    /// <summary>
    /// 尝试把一行识别为一级或二级标题
    /// </summary>
    private static bool TryReadHeading(string trimmed, out int level, out string heading)
    {
        level = 0;
        heading = string.Empty;

        var hashCount = 0;
        while (hashCount < trimmed.Length && trimmed[hashCount] == '#')
        {
            hashCount++;
        }

        // 三级及更深标题并入所属章节，不作为切分点
        if (hashCount is not (1 or 2))
        {
            return false;
        }

        if (hashCount >= trimmed.Length || trimmed[hashCount] != ' ')
        {
            return false;
        }

        level = hashCount;
        heading = trimmed[(hashCount + 1)..].Trim();
        return heading.Length > 0;
    }

    /// <summary>
    /// 把一个章节正文追加进结果，超长时按空行二次切分
    /// </summary>
    private static void AppendSections(
        List<DocSection> sections,
        string relativePath,
        DocSourceKind source,
        string documentTitle,
        string heading,
        string content,
        int startLine,
        int lineCount)
    {
        var basePath = $"{documentTitle} > {heading}";
        var endLine = startLine + lineCount;

        if (content.Length <= MaxSectionLength)
        {
            sections.Add(new DocSection(relativePath, source, documentTitle, heading, basePath, content, startLine, endLine));
            return;
        }

        var chunks = SplitByBlankLine(content);
        for (var i = 0; i < chunks.Count; i++)
        {
            var titlePath = $"{basePath} ({i + 1}/{chunks.Count})";
            sections.Add(new DocSection(relativePath, source, documentTitle, heading, titlePath, chunks[i], startLine, endLine));
        }
    }

    /// <summary>
    /// 按空行把超长正文聚合成不超过上限的若干片段
    /// </summary>
    private static List<string> SplitByBlankLine(string content)
    {
        var paragraphs = content.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<string>();
        var builder = new StringBuilder();

        foreach (var paragraph in paragraphs)
        {
            if (builder.Length > 0 && builder.Length + paragraph.Length > MaxSectionLength)
            {
                chunks.Add(builder.ToString().Trim());
                builder.Clear();
            }

            builder.Append(paragraph).Append("\n\n");
        }

        if (builder.Length > 0)
        {
            chunks.Add(builder.ToString().Trim());
        }

        return chunks.Count > 0 ? chunks : [content];
    }
}
