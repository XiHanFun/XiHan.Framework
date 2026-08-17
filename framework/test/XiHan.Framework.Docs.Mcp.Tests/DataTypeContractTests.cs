// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Docs.Mcp.Indexing;
using XiHan.Framework.Docs.Mcp.Sources;

namespace XiHan.Framework.Docs.Mcp.Tests;

/// <summary>
/// 数据类型的契约测试：位置参数顺序与来源分类的成员集合
/// </summary>
/// <remarks>
/// 这里不测「记录能存住传进去的值」——那是在测编译器。测的是位置参数的**顺序**：
/// <see cref="DocSection"/> 有四个连续的 <see cref="string"/> 参数，把其中任意两个对调，
/// 编译一路绿灯、类型系统一句话都不会说，六个消费方却会全部错位，
/// 最终表现为检索结果的出处指向另一篇文档。这类改动没有任何编译期信号可依赖，
/// 只能靠「按位置构造、按名字断言」把顺序钉死在测试里。
/// </remarks>
public class DataTypeContractTests
{
    /// <summary>
    /// 文档文件的位置参数顺序：绝对路径在前，相对路径在后
    /// </summary>
    /// <remarks>
    /// 两个相邻的 <see cref="string"/> 参数一旦对调，索引里存的「对外展示路径」会变成
    /// 本机绝对路径，检索结果的出处就从 <c>docs/guide/event-bus.md</c> 变成
    /// <c>C:\...\docs\guide\event-bus.md</c>，而 <c>read_doc</c> 的白名单比对也会随之全部落空。
    /// </remarks>
    [Fact]
    public void 文档文件的位置参数顺序()
    {
        var lastWriteUtc = new DateTime(2026, 8, 16, 12, 34, 56, DateTimeKind.Utc);

        var file = new DocFile(
            "/repo/docs/guide/event-bus.md",
            "docs/guide/event-bus.md",
            DocSourceKind.Guide,
            lastWriteUtc);

        Assert.Equal("/repo/docs/guide/event-bus.md", file.AbsolutePath);
        Assert.Equal("docs/guide/event-bus.md", file.RelativePath);
        Assert.Equal(DocSourceKind.Guide, file.Source);
        Assert.Equal(lastWriteUtc, file.LastWriteUtc);
    }

    /// <summary>
    /// 章节的位置参数顺序：四个连续字符串与两个连续行号各就各位
    /// </summary>
    /// <remarks>
    /// 四个字符串取值刻意互不相同且互不为子串，任意两个对调都至少让一条断言变红；
    /// 起止行号也取了不同的值，把 <c>StartLine</c> 与 <c>EndLine</c> 对调同样会被抓住——
    /// 出处的行区间是模型据以引用原文的唯一坐标，倒过来就是一段指不到任何地方的引用。
    /// </remarks>
    [Fact]
    public void 章节的位置参数顺序()
    {
        var section = new DocSection(
            "docs/guide/event-bus.md",
            DocSourceKind.Guide,
            "事件总线",
            "本地事件还是分布式事件",
            "事件总线 > 本地事件还是分布式事件",
            "分布式事件在事务提交之后发布。",
            7,
            26);

        Assert.Equal("docs/guide/event-bus.md", section.RelativePath);
        Assert.Equal(DocSourceKind.Guide, section.Source);
        Assert.Equal("事件总线", section.DocumentTitle);
        Assert.Equal("本地事件还是分布式事件", section.Heading);
        Assert.Equal("事件总线 > 本地事件还是分布式事件", section.TitlePath);
        Assert.Equal("分布式事件在事务提交之后发布。", section.Content);
        Assert.Equal(7, section.StartLine);
        Assert.Equal(26, section.EndLine);
    }

    /// <summary>
    /// 来源分类恰好是四个成员
    /// </summary>
    /// <remarks>
    /// <c>DocsMcpTools</c> 里的 <c>ParseSource</c> 与 <c>DescribeSource</c> 两处 switch
    /// 都按这四个成员穷举，新增第五个成员时两处都不会报编译错误：新来源既选不中
    /// （<c>source</c> 参数无对应取值，只会得到「无法识别」提示），在检索结果里也只会显示成「未知」。
    /// 这条断言就是那两处 switch 的提醒器——枚举一变它先红，红了就去补 switch。
    /// </remarks>
    [Fact]
    public void 来源分类恰好四个成员()
    {
        string[] expected = ["Guide", "Package", "PackageReadme", "Root"];

        Assert.Equal(expected, Enum.GetNames<DocSourceKind>().Order(StringComparer.Ordinal));
    }
}
