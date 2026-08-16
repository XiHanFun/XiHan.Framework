// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Docs.Mcp.Indexing;
using XiHan.Framework.Docs.Mcp.Sources;

namespace XiHan.Framework.Docs.Mcp.Tests;

/// <summary>
/// Markdown 章节切片器测试
/// </summary>
public class MarkdownSectionSplitterTests
{
    /// <summary>
    /// 按二级标题切分，标题路径拼接一级标题
    /// </summary>
    [Fact]
    public void 按二级标题切分()
    {
        const string Markdown = """
            # 事件总线

            前言内容。

            ## 本地事件

            本地事件正文。

            ## 分布式事件

            分布式事件正文。
            """;

        var sections = MarkdownSectionSplitter.Split("docs/guide/event-bus.md", DocSourceKind.Guide, Markdown);

        Assert.Equal(3, sections.Count);
        Assert.Equal("概述", sections[0].Heading);
        Assert.Equal("本地事件", sections[1].Heading);
        Assert.Equal("事件总线 > 本地事件", sections[1].TitlePath);
        Assert.Equal("分布式事件", sections[2].Heading);
    }

    /// <summary>
    /// 代码围栏内部的井号不得被当作标题
    /// </summary>
    [Fact]
    public void 代码围栏内的井号不切分()
    {
        const string Markdown = """
            # 安装

            ## 安装与启用

            ```bash
            # 安装这个包
            ## 假章节
            dotnet add package XiHan.Framework.EventBus
            ```

            ```csharp
            #region 注册
            services.AddXiHanEventBus();
            #endregion
            ```

            正文结束。
            """;

        var sections = MarkdownSectionSplitter.Split("docs/packages/eventbus.md", DocSourceKind.Package, Markdown);

        Assert.Single(sections);
        Assert.Equal("安装与启用", sections[0].Heading);
        // 围栏内的一级井号一旦被当成标题，文档标题会被污染成「安装这个包」
        Assert.Equal("安装", sections[0].DocumentTitle);
        // 围栏内容必须原样保留，不能被当成标题行吞掉
        Assert.Contains("# 安装这个包", sections[0].Content);
        Assert.Contains("dotnet add package", sections[0].Content);
        Assert.Contains("#region", sections[0].Content);
    }

    /// <summary>
    /// 波浪线围栏同样生效，且两种围栏标记互不闭合
    /// </summary>
    [Fact]
    public void 波浪线围栏与反引号围栏互不闭合()
    {
        const string Markdown = """
            # 围栏

            ## 围栏用法

            ~~~bash
            # 波浪围栏内的井号
            ```
            ## 波浪围栏内的假章节
            ~~~

            ```text
            ~~~
            ## 反引号围栏内的假章节
            ```

            正文结束。
            """;

        var sections = MarkdownSectionSplitter.Split("docs/packages/x.md", DocSourceKind.Package, Markdown);

        Assert.Single(sections);
        Assert.Equal("围栏用法", sections[0].Heading);
        Assert.Equal("围栏", sections[0].DocumentTitle);
        Assert.Contains("# 波浪围栏内的井号", sections[0].Content);
        Assert.Contains("## 波浪围栏内的假章节", sections[0].Content);
        Assert.Contains("## 反引号围栏内的假章节", sections[0].Content);
    }

    /// <summary>
    /// 三级及更深标题并入所属二级章节，不单独成章
    /// </summary>
    [Fact]
    public void 三级标题并入二级章节()
    {
        const string Markdown = """
            # 事件总线

            ## 工作原理

            ### 发布路径

            发布路径正文。

            ### 订阅路径

            订阅路径正文。
            """;

        var sections = MarkdownSectionSplitter.Split("docs/packages/eventbus.md", DocSourceKind.Package, Markdown);

        Assert.Single(sections);
        Assert.Equal("工作原理", sections[0].Heading);
        Assert.Contains("发布路径正文", sections[0].Content);
        Assert.Contains("订阅路径正文", sections[0].Content);
    }

    /// <summary>
    /// YAML frontmatter 不进入索引
    /// </summary>
    [Fact]
    public void 跳过前置元数据块()
    {
        const string Markdown = """
            ---
            layout: home
            title: 不该被索引
            ---

            # 真正的标题

            ## 章节

            正文。
            """;

        var sections = MarkdownSectionSplitter.Split("docs/index.md", DocSourceKind.Root, Markdown);

        Assert.Equal("真正的标题", sections[0].DocumentTitle);
        Assert.DoesNotContain(sections, s => s.Content.Contains("layout: home"));
    }

    /// <summary>
    /// 一级标题之后、第一个二级标题之前的前言自成「概述」章节
    /// </summary>
    [Fact]
    public void 前言自成概述章节()
    {
        const string Markdown = """
            # 事件总线

            这是最精华的定位说明。

            ## 章节

            正文。
            """;

        var sections = MarkdownSectionSplitter.Split("docs/guide/event-bus.md", DocSourceKind.Guide, Markdown);

        Assert.Equal("概述", sections[0].Heading);
        Assert.Contains("最精华的定位说明", sections[0].Content);
        Assert.Equal("事件总线 > 概述", sections[0].TitlePath);
    }

    /// <summary>
    /// 没有前言时不产生空的概述章节
    /// </summary>
    [Fact]
    public void 无前言时不产生空概述()
    {
        const string Markdown = """
            # 标题

            ## 章节

            正文。
            """;

        var sections = MarkdownSectionSplitter.Split("docs/guide/x.md", DocSourceKind.Guide, Markdown);

        Assert.Single(sections);
        Assert.Equal("章节", sections[0].Heading);
    }

    /// <summary>
    /// 超长章节按空行二次切分，标题路径带分片序号
    /// </summary>
    [Fact]
    public void 超长章节二次切分()
    {
        var longParagraph = new string('文', 1500);
        var markdown = $"""
            # 标题

            ## 超长章节

            {longParagraph}

            {longParagraph}

            {longParagraph}
            """;

        var sections = MarkdownSectionSplitter.Split("docs/guide/x.md", DocSourceKind.Guide, markdown);

        Assert.True(sections.Count > 1);
        Assert.All(sections, s => Assert.Equal("超长章节", s.Heading));
        Assert.Contains(sections, s => s.TitlePath.EndsWith("(1/2)") || s.TitlePath.EndsWith("(1/3)"));
    }

    /// <summary>
    /// 缺少一级标题时用文件名兜底
    /// </summary>
    [Fact]
    public void 缺少一级标题时用文件名兜底()
    {
        const string Markdown = """
            ## 只有二级标题

            正文。
            """;

        var sections = MarkdownSectionSplitter.Split("docs/guide/event-bus.md", DocSourceKind.Guide, Markdown);

        Assert.Equal("event-bus", sections[0].DocumentTitle);
    }

    /// <summary>
    /// 行号从 1 开始且指向标题行
    /// </summary>
    [Fact]
    public void 行号从一开始且指向标题行()
    {
        const string Markdown = """
            # 标题

            ## 第一节

            正文。
            """;

        var sections = MarkdownSectionSplitter.Split("docs/guide/x.md", DocSourceKind.Guide, Markdown);

        Assert.Equal(3, sections[0].StartLine);
    }
}
