// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Docs.Mcp.Search;

namespace XiHan.Framework.Docs.Mcp.Tests;

/// <summary>
/// 术语同义词扩展测试
/// </summary>
public class SynonymExpanderTests
{
    /// <summary>
    /// 原始查询词权重为 1
    /// </summary>
    [Fact]
    public void 原始查询词满权重()
    {
        var expander = CreateExpander("""[["重复消费", "去重", "收件箱"]]""");

        var terms = expander.Expand("分布式事件");

        Assert.All(terms, t => Assert.Equal(1.0, t.Weight));
    }

    /// <summary>
    /// 命中术语组时把同组其余术语按半权加入
    /// </summary>
    [Fact]
    public void 命中术语组时半权扩展()
    {
        var expander = CreateExpander("""[["重复消费", "去重", "收件箱"]]""");

        var terms = expander.Expand("怎么避免重复消费");

        Assert.Contains(terms, t => t.Term == "收件" && t.Weight == 0.5);
        Assert.Contains(terms, t => t.Term == "去重" && t.Weight == 0.5);
    }

    /// <summary>
    /// 同一词条同时来自原始查询与扩展时保留高权重
    /// </summary>
    [Fact]
    public void 同词条保留高权重()
    {
        var expander = CreateExpander("""[["重复消费", "重复提交"]]""");

        var terms = expander.Expand("重复消费");

        Assert.Equal(1.0, terms.Single(t => t.Term == "重复").Weight);
    }

    /// <summary>
    /// 纯拉丁术语按词条匹配而非子串
    /// </summary>
    /// <remarks>
    /// 这条钉的是 <c>SynonymExpander.Mentions</c> 里那个「含中文走子串、纯拉丁走词条」的分支。
    /// 「Redis」内含子串「di」，退回子串匹配的话整组依赖注入术语都会被扩展进来，
    /// 把依赖注入文档推向一个问 Redis 的查询的首屏。其余几条测试用的都是中文术语，
    /// 删掉那个分支照样全绿，所以必须单独有这一条。
    /// </remarks>
    [Fact]
    public void 拉丁术语按词条匹配而非子串()
    {
        var expander = CreateExpander("""[["依赖注入", "DI", "容器"]]""");

        var terms = expander.Expand("Redis 事件总线怎么配");

        Assert.DoesNotContain(terms, t => t.Term == "依赖" || t.Term == "容器");
    }

    /// <summary>
    /// 术语表文件缺失时降级为不扩展，服务照常
    /// </summary>
    [Fact]
    public void 术语表缺失时降级()
    {
        var expander = SynonymExpander.Load(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json"),
            NullLogger.Instance);

        var terms = expander.Expand("重复消费");

        Assert.NotEmpty(terms);
        Assert.All(terms, t => Assert.Equal(1.0, t.Weight));
    }

    /// <summary>
    /// 术语表格式损坏时降级为不扩展，服务照常
    /// </summary>
    [Fact]
    public void 术语表损坏时降级()
    {
        var expander = CreateExpander("{ 这不是合法的 JSON 数组");

        var terms = expander.Expand("重复消费");

        Assert.NotEmpty(terms);
        Assert.All(terms, t => Assert.Equal(1.0, t.Weight));
    }

    /// <summary>
    /// 用指定内容写一个临时术语表并加载
    /// </summary>
    private static SynonymExpander CreateExpander(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, json);

        try
        {
            return SynonymExpander.Load(path, NullLogger.Instance);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
