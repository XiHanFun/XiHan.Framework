// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Docs.Mcp.Indexing;

namespace XiHan.Framework.Docs.Mcp.Tests;

/// <summary>
/// 倒排索引测试
/// </summary>
public class BigramIndexTests
{
    /// <summary>
    /// 正文中的词条可以被检索到
    /// </summary>
    [Fact]
    public void 正文词条可检索()
    {
        var index = new BigramIndex();
        index.Add(0, "标题", "分布式事件");

        var postings = index.Find("事件");

        Assert.Single(postings);
        Assert.Equal(0, postings[0].SectionId);
    }

    /// <summary>
    /// 标题中的词条带有标题标记
    /// </summary>
    [Fact]
    public void 标题词条带标记()
    {
        var index = new BigramIndex();
        index.Add(0, "分布式事件", "无关正文");

        var postings = index.Find("事件");

        Assert.True(postings[0].InTitle);
    }

    /// <summary>
    /// 同一章节内同词条只保留一条，标题命中优先
    /// </summary>
    [Fact]
    public void 同章节同词条去重且标题优先()
    {
        var index = new BigramIndex();
        index.Add(0, "分布式事件", "分布式事件分布式事件");

        var postings = index.Find("事件");

        Assert.Single(postings);
        Assert.True(postings[0].InTitle);
    }

    /// <summary>
    /// 同一词条出现在不同章节时各保留一条
    /// </summary>
    [Fact]
    public void 跨章节各保留一条()
    {
        var index = new BigramIndex();
        index.Add(0, "甲", "分布式事件");
        index.Add(1, "乙", "分布式事件");

        var postings = index.Find("事件");

        Assert.Equal(2, postings.Count);
    }

    /// <summary>
    /// 未收录的词条返回空集合而非抛出
    /// </summary>
    [Fact]
    public void 未收录词条返回空集合()
    {
        var index = new BigramIndex();
        index.Add(0, "标题", "正文");

        Assert.Empty(index.Find("不存在的词"));
    }
}
