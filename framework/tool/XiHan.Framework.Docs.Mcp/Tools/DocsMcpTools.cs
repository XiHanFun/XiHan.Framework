// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;
using XiHan.Framework.Docs.Mcp.Indexing;
using XiHan.Framework.Docs.Mcp.Options;
using XiHan.Framework.Docs.Mcp.Search;
using XiHan.Framework.Docs.Mcp.Sources;

namespace XiHan.Framework.Docs.Mcp.Tools;

/// <summary>
/// 曦寒框架文档的三个 MCP 工具
/// </summary>
/// <param name="index">索引门面</param>
/// <param name="locator">文档来源定位器</param>
/// <param name="expander">同义词扩展器</param>
/// <param name="scorer">排序器</param>
/// <param name="gate">相关性截断</param>
/// <param name="options">可调参数</param>
/// <remarks>
/// 工具数量刻意压到三个：工具越多，模型越容易选错或漏用。
/// 来源区分用参数表达，而不是拆成 search_guide / search_packages 之类的多个工具。
/// 全部返回 Markdown 文本而非 JSON，因为调用方是语言模型，同等信息量下 Markdown 的 token 消耗更低。
/// </remarks>
[McpServerToolType]
public sealed class DocsMcpTools(
    DocIndex index,
    DocSourceLocator locator,
    SynonymExpander expander,
    SectionScorer scorer,
    RelevanceGate gate,
    DocsMcpOptions options)
{
    /// <summary>
    /// 检索曦寒框架文档，返回最相关的章节原文
    /// </summary>
    /// <param name="query">自然语言问题或关键词</param>
    /// <param name="source">来源过滤</param>
    /// <param name="limit">返回条数</param>
    /// <returns>带出处的章节原文</returns>
    [McpServerTool(Name = "search_docs")]
    [Description("检索曦寒框架（XiHan.Framework）的文档，返回最相关的章节原文与出处。用它回答框架的用法、配置、API 与设计原理问题，不要凭记忆作答。")]
    public string SearchDocs(
        [Description("自然语言问题或关键词，例如「分布式事件什么时候发出去」")] string query,
        [Description("来源过滤：guide 使用指南、packages 包文档、readme 包自述、root 全局文档、all 全部。默认 all")] string? source = null,
        [Description("返回的章节数，默认 5，最大 15")] int limit = 5)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return "查询串为空，请给出要检索的问题或关键词。";
            }

            var snapshot = index.EnsureFresh();

            var (filter, notice) = ParseSource(source);
            var effectiveLimit = Math.Clamp(limit <= 0 ? options.DefaultLimit : limit, 1, options.MaxLimit);
            var terms = expander.Expand(query);
            var hits = scorer.Rank(terms, snapshot.Sections, snapshot.Index, filter, effectiveLimit);

            // 命中不为空不等于相关：中文 bigram 总能在高频片段上蹭到几段文字，
            // 所以还要问一句「这个查询里的词，语料到底认不认识」，不认识就走同一条显式否认分支。
            if (hits.Count == 0 || !gate.IsAboutIndexedDocs(query, snapshot.Index, snapshot.Sections.Count))
            {
                return BuildEmptyResult(snapshot, query, notice);
            }

            var builder = new StringBuilder();
            if (notice.Length > 0)
            {
                builder.AppendLine(notice).AppendLine();
            }

            builder.AppendLine($"检索「{query}」共命中 {hits.Count} 个章节：").AppendLine();

            foreach (var hit in hits)
            {
                builder
                    .AppendLine($"## {hit.Section.TitlePath}")
                    .AppendLine($"- 出处：`{hit.Section.RelativePath}` 第 {hit.Section.StartLine}-{hit.Section.EndLine} 行")
                    .AppendLine($"- 来源：{DescribeSource(hit.Section.Source)}；得分：{hit.Score:F2}")
                    .AppendLine()
                    .AppendLine(hit.Section.Content)
                    .AppendLine();
            }

            return builder.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            return $"检索时发生错误：{ex.Message}";
        }
    }

    /// <summary>
    /// 读取一篇文档的全文或指定章节
    /// </summary>
    /// <param name="path">相对仓库根的文档路径</param>
    /// <param name="section">章节标题，为空则返回全文</param>
    /// <returns>文档原文</returns>
    [McpServerTool(Name = "read_doc")]
    [Description("读取曦寒框架的一篇文档。先用 search_docs 找到路径，需要更多上下文时再用本工具。")]
    public string ReadDoc(
        [Description("相对仓库根的路径，例如 docs/guide/event-bus.md")] string path,
        [Description("章节标题，只返回该节。留空返回全文；全文过长时会改为返回章节目录")] string? section = null)
    {
        try
        {
            var snapshot = index.EnsureFresh();

            if (!locator.TryResolveDocumentPath(path, out var absolutePath))
            {
                return $"拒绝访问 `{path}`：路径必须是仓库根内的相对路径。";
            }

            // 仅包含性校验是不够的：通过之后 `.git/config`、任意源码、任意 appsettings 都会被原样读出，
            // 而本工具的契约是「读一篇曦寒框架的文档」。所以再加一道白名单，只放行枚举出来的文档。
            // 白名单里的条目都是真实枚举出来的文件，符号链接是否解析也就不再有意义。
            //
            // 比对的是**解析后的绝对路径**而不是使用者原串：拿原串比的话，`./docs/x.md`、
            // `docs//x.md`、Windows 上大小写不同的写法都能过包含性校验却匹配不上白名单，
            // 白白退化成「未找到」；大小写的判定也必须与包含性校验共用同一条 PathComparison，
            // 否则两道关卡对同一个路径会给出互相矛盾的答案。
            var indexed = snapshot.Files.FirstOrDefault(
                f => f.AbsolutePath.Equals(absolutePath, DocSourceLocator.PathComparison));

            if (indexed is null)
            {
                return BuildPathSuggestion(snapshot, path);
            }

            if (!File.Exists(absolutePath))
            {
                return BuildPathSuggestion(snapshot, path);
            }

            // 往下一律用白名单里的规范相对路径，不再碰使用者原串
            var sections = snapshot.Sections.Where(s => s.RelativePath == indexed.RelativePath).ToList();

            if (!string.IsNullOrWhiteSpace(section))
            {
                var matched = sections.Where(s => s.Heading.Contains(section, StringComparison.OrdinalIgnoreCase)).ToList();
                if (matched.Count == 0)
                {
                    var available = string.Join("、", sections.Select(s => s.Heading).Distinct());
                    return $"文档 `{path}` 中未找到章节「{section}」。可用章节：{available}";
                }

                var builder = new StringBuilder();
                foreach (var item in matched)
                {
                    builder
                        .AppendLine($"## {item.TitlePath}")
                        .AppendLine($"- 出处：`{item.RelativePath}` 第 {item.StartLine}-{item.EndLine} 行")
                        .AppendLine()
                        .AppendLine(item.Content)
                        .AppendLine();
                }

                return builder.ToString().TrimEnd();
            }

            var content = File.ReadAllText(absolutePath);
            if (content.Length <= options.MaxWholeDocumentLength)
            {
                return $"# `{path}`\n\n{content}";
            }

            var headings = string.Join("\n", sections.Select(s => $"- {s.Heading}"));
            return $"""
                文档 `{path}` 共 {content.Length} 个字符，超过单次返回上限。
                请用 section 参数指定要读的章节。可用章节：

                {headings}
                """;
        }
        catch (Exception ex)
        {
            return $"读取文档时发生错误：{ex.Message}";
        }
    }

    /// <summary>
    /// 列出全部被索引的文档
    /// </summary>
    /// <param name="source">来源过滤</param>
    /// <param name="includeSections">是否展开章节标题</param>
    /// <returns>文档清单</returns>
    [McpServerTool(Name = "list_docs")]
    [Description("列出曦寒框架全部文档，用于建立整体地图。默认只列标题与摘要；includeSections 为 true 时展开章节标题，输出会大幅变长。")]
    public string ListDocs(
        [Description("来源过滤：guide、packages、readme、root、all。默认 all")] string? source = null,
        [Description("是否展开每篇的章节标题，默认 false")] bool includeSections = false)
    {
        try
        {
            var snapshot = index.EnsureFresh();

            var (filter, notice) = ParseSource(source);
            var files = filter is null ? snapshot.Files : snapshot.Files.Where(f => f.Source == filter).ToList();

            // 按路径建一次索引再进循环：逐篇过滤全表是 O(文件数 × 章节数)，
            // 当前语料 163 篇 × 1720 个章节 ≈ 28 万次字符串比较，白付一次。
            // ToLookup 保序，缺键返回空集合，输出与逐篇 Where 完全一致。
            var sectionsByPath = snapshot.Sections.ToLookup(s => s.RelativePath, StringComparer.Ordinal);

            var builder = new StringBuilder();
            if (notice.Length > 0)
            {
                builder.AppendLine(notice).AppendLine();
            }

            builder.AppendLine($"共 {files.Count} 篇文档：").AppendLine();

            foreach (var file in files)
            {
                var sections = sectionsByPath[file.RelativePath];
                var title = sections.FirstOrDefault()?.DocumentTitle ?? file.RelativePath;

                builder.AppendLine($"- `{file.RelativePath}` — {title}");

                var summary = BuildSummary(sections);
                if (summary.Length > 0)
                {
                    builder.AppendLine($"  {summary}");
                }

                if (!includeSections)
                {
                    continue;
                }

                foreach (var heading in sections.Select(s => s.Heading).Distinct())
                {
                    builder.AppendLine($"  - {heading}");
                }
            }

            return builder.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            return $"列出文档时发生错误：{ex.Message}";
        }
    }

    /// <summary>
    /// 解析来源参数，无法识别时按全部处理并附提示
    /// </summary>
    private static (DocSourceKind? Filter, string Notice) ParseSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source) || source.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return (null, string.Empty);
        }

        return source.ToLowerInvariant() switch
        {
            "guide" => (DocSourceKind.Guide, string.Empty),
            "packages" or "package" => (DocSourceKind.Package, string.Empty),
            "readme" => (DocSourceKind.PackageReadme, string.Empty),
            "root" => (DocSourceKind.Root, string.Empty),
            _ => (null, $"提示：无法识别的 source 取值「{source}」，已按 all 处理。可用取值为 guide、packages、readme、root、all。")
        };
    }

    /// <summary>
    /// 描述来源分类，便于模型判断内容性质
    /// </summary>
    private static string DescribeSource(DocSourceKind source)
    {
        return source switch
        {
            DocSourceKind.Guide => "使用指南",
            DocSourceKind.Package => "包文档",
            DocSourceKind.PackageReadme => "包自述",
            DocSourceKind.Root => "全局文档",
            _ => "未知"
        };
    }

    /// <summary>
    /// 从概述章节取一句话摘要
    /// </summary>
    private static string BuildSummary(IEnumerable<DocSection> sections)
    {
        var preamble = sections.FirstOrDefault(s => s.Heading == "概述");
        if (preamble is null)
        {
            return string.Empty;
        }

        foreach (var line in preamble.Content.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('>') || trimmed.StartsWith('-') || trimmed.StartsWith('|') || trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                continue;
            }

            var cleaned = trimmed.Replace("**", string.Empty).Replace("`", string.Empty);
            return cleaned.Length <= 80 ? cleaned : cleaned[..80];
        }

        return string.Empty;
    }

    /// <summary>
    /// 构造零命中时的回复，明确告知文档中没有，避免模型自行编造
    /// </summary>
    private static string BuildEmptyResult(IndexSnapshot snapshot, string query, string notice)
    {
        var candidates = string.Join("\n", snapshot.Files.Take(10).Select(f => $"- `{f.RelativePath}`"));

        return $"""
            {notice}
            未找到与「{query}」相关的文档内容。曦寒框架的文档中没有涵盖这个主题，请不要基于猜测作答。

            可以换个关键词再试，或用 list_docs 查看全部文档。部分文档：
            {candidates}
            """.Trim();
    }

    /// <summary>
    /// 构造路径不在索引内时的候选建议
    /// </summary>
    private static string BuildPathSuggestion(IndexSnapshot snapshot, string path)
    {
        var target = Path.GetFileNameWithoutExtension(path);
        var suggestions = snapshot.Files
            .OrderByDescending(f => CountCommonCharacters(Path.GetFileNameWithoutExtension(f.RelativePath), target))
            .Take(3)
            .Select(f => $"- `{f.RelativePath}`");

        return $"""
            未找到文档 `{path}`。你可能是指：
            {string.Join("\n", suggestions)}
            """;
    }

    /// <summary>
    /// 统计两个文件名的共同字符数，用于粗略推荐相近路径
    /// </summary>
    private static int CountCommonCharacters(string left, string right)
    {
        var pool = right.ToLowerInvariant().ToList();
        var count = 0;

        foreach (var ch in left.ToLowerInvariant())
        {
            if (pool.Remove(ch))
            {
                count++;
            }
        }

        return count;
    }
}
