// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Docs.Mcp.Indexing;
using XiHan.Framework.Docs.Mcp.Options;
using XiHan.Framework.Docs.Mcp.Search;
using XiHan.Framework.Docs.Mcp.Sources;

namespace XiHan.Framework.Docs.Mcp.Tests;

/// <summary>
/// 检索排序测试
/// </summary>
public class SectionScorerTests
{
    /// <summary>
    /// 覆盖率高的短章节应胜过命中数相同但查询覆盖不全的场景
    /// </summary>
    [Fact]
    public void 覆盖率优先于篇幅()
    {
        var sections = new List<DocSection>
        {
            CreateSection("docs/guide/a.md", DocSourceKind.Guide, "甲", "分布式事件发布"),
            CreateSection("docs/guide/b.md", DocSourceKind.Guide, "乙", "分布式" + new string('文', 3000))
        };

        var hits = Rank(sections, "分布式事件发布");

        Assert.Equal("docs/guide/a.md", hits[0].Section.RelativePath);
    }

    /// <summary>
    /// 标题命中的章节排在正文命中之前
    /// </summary>
    [Fact]
    public void 标题命中优先()
    {
        var sections = new List<DocSection>
        {
            CreateSection("docs/guide/a.md", DocSourceKind.Guide, "无关标题", "分布式事件"),
            CreateSection("docs/guide/b.md", DocSourceKind.Guide, "分布式事件", "无关正文")
        };

        var hits = Rank(sections, "分布式事件");

        Assert.Equal("docs/guide/b.md", hits[0].Section.RelativePath);
    }

    /// <summary>
    /// 同一文件最多返回两个章节，避免一篇文章洗版
    /// </summary>
    [Fact]
    public void 同文件最多两个章节()
    {
        var sections = new List<DocSection>
        {
            CreateSection("docs/guide/a.md", DocSourceKind.Guide, "章节一", "分布式事件"),
            CreateSection("docs/guide/a.md", DocSourceKind.Guide, "章节二", "分布式事件"),
            CreateSection("docs/guide/a.md", DocSourceKind.Guide, "章节三", "分布式事件"),
            CreateSection("docs/guide/b.md", DocSourceKind.Guide, "章节四", "分布式事件")
        };

        var hits = Rank(sections, "分布式事件");

        Assert.Equal(2, hits.Count(h => h.Section.RelativePath == "docs/guide/a.md"));
    }

    /// <summary>
    /// 指南来源的权重高于包 README
    /// </summary>
    [Fact]
    public void 指南来源权重更高()
    {
        var sections = new List<DocSection>
        {
            CreateSection("framework/src/X/README.md", DocSourceKind.PackageReadme, "标题", "分布式事件"),
            CreateSection("docs/guide/a.md", DocSourceKind.Guide, "标题", "分布式事件")
        };

        var hits = Rank(sections, "分布式事件");

        Assert.Equal(DocSourceKind.Guide, hits[0].Section.Source);
    }

    /// <summary>
    /// 来源过滤只返回指定分类
    /// </summary>
    [Fact]
    public void 来源过滤生效()
    {
        var sections = new List<DocSection>
        {
            CreateSection("docs/guide/a.md", DocSourceKind.Guide, "标题", "分布式事件"),
            CreateSection("docs/packages/b.md", DocSourceKind.Package, "标题", "分布式事件")
        };

        var hits = Rank(sections, "分布式事件", DocSourceKind.Package);

        Assert.Single(hits);
        Assert.Equal(DocSourceKind.Package, hits[0].Section.Source);
    }

    /// <summary>
    /// 零命中返回空集合
    /// </summary>
    [Fact]
    public void 零命中返回空集合()
    {
        var sections = new List<DocSection>
        {
            CreateSection("docs/guide/a.md", DocSourceKind.Guide, "标题", "正文")
        };

        Assert.Empty(Rank(sections, "完全不相干的查询内容"));
    }

    /// <summary>
    /// 用给定章节集合执行一次排序
    /// </summary>
    private static IReadOnlyList<SearchHit> Rank(List<DocSection> sections, string query, DocSourceKind? filter = null)
    {
        var index = new BigramIndex();
        for (var i = 0; i < sections.Count; i++)
        {
            index.Add(i, sections[i].TitlePath, sections[i].Content);
        }

        var options = new DocsMcpOptions();
        var terms = Tokenizer.Tokenize(query).Distinct().Select(t => new WeightedTerm(t, 1.0)).ToList();

        return new SectionScorer(options).Rank(terms, sections, index, filter, options.DefaultLimit);
    }

    /// <summary>
    /// 构造一个测试用章节
    /// </summary>
    private static DocSection CreateSection(string path, DocSourceKind source, string heading, string content)
    {
        return new DocSection(path, source, "文档", heading, $"文档 > {heading}", content, 1, 10);
    }
}
