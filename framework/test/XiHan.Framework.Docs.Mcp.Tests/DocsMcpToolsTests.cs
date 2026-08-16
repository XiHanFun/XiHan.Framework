// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Docs.Mcp.Indexing;
using XiHan.Framework.Docs.Mcp.Options;
using XiHan.Framework.Docs.Mcp.Search;
using XiHan.Framework.Docs.Mcp.Sources;
using XiHan.Framework.Docs.Mcp.Tools;

namespace XiHan.Framework.Docs.Mcp.Tests;

/// <summary>
/// MCP 工具层测试
/// </summary>
public class DocsMcpToolsTests : IDisposable
{
    private readonly string _root;

    /// <summary>
    /// 构造一个最小仓库结构
    /// </summary>
    public DocsMcpToolsTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "xihan-tools-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "docs", "guide"));
        Directory.CreateDirectory(Path.Combine(_root, "docs", "packages"));
        Directory.CreateDirectory(Path.Combine(_root, "framework", "src"));

        File.WriteAllText(
            Path.Combine(_root, "docs", "guide", "event-bus.md"),
            "# 事件总线\n\n发布方不认识订阅方。\n\n## 本地事件还是分布式事件\n\n分布式事件在事务提交之后发布。\n");
        File.WriteAllText(
            Path.Combine(_root, "docs", "packages", "caching.md"),
            "# 缓存包\n\n## 配置项\n\n缓存过期时间的配置说明。\n");

        // 仓库根内、但不属于任何一类文档来源的文件，用来验证 read_doc 的白名单
        File.WriteAllText(
            Path.Combine(_root, "framework", "src", "appsettings.Production.json"),
            "{ \"ConnectionString\": \"绝密连接串\" }");
    }

    /// <summary>
    /// 清理临时目录
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 检索结果带出处：相对路径、标题路径与行号
    /// </summary>
    [Fact]
    public void 检索结果带出处()
    {
        var result = CreateTools().SearchDocs("分布式事件什么时候发布", source: null, limit: 5);

        Assert.Contains("docs/guide/event-bus.md", result);
        Assert.Contains("本地事件还是分布式事件", result);
        Assert.Contains("事务提交之后", result);
    }

    /// <summary>
    /// 零命中时明确告知文档中没有，而不是返回空内容诱导模型编造
    /// </summary>
    [Fact]
    public void 零命中时明确告知()
    {
        var result = CreateTools().SearchDocs("量子纠缠的宏观表现", source: null, limit: 5);

        Assert.Contains("未找到", result);

        // 「没检索到」与「明确要求不要猜」是两件事：只断言前者的话，
        // 一句干巴巴的「未找到」也能过，而那正是诱导模型自行补齐的写法。
        Assert.Contains("不要基于猜测", result);
    }

    /// <summary>
    /// 来源过滤生效：限定指南时不返回包文档
    /// </summary>
    [Fact]
    public void 来源过滤生效()
    {
        var tools = CreateTools();

        var unfiltered = tools.SearchDocs("缓存过期配置", source: null, limit: 5);
        var filtered = tools.SearchDocs("缓存过期配置", source: "guide", limit: 5);

        Assert.Contains("docs/packages/caching.md", unfiltered);
        Assert.Contains("未找到", filtered);
    }

    /// <summary>
    /// 读取整篇文档返回原文
    /// </summary>
    [Fact]
    public void 读取整篇文档()
    {
        var result = CreateTools().ReadDoc("docs/guide/event-bus.md", section: null);

        Assert.Contains("发布方不认识订阅方", result);
        Assert.Contains("分布式事件在事务提交之后发布", result);
    }

    /// <summary>
    /// 指定章节时只返回该节
    /// </summary>
    [Fact]
    public void 读取指定章节()
    {
        var result = CreateTools().ReadDoc("docs/guide/event-bus.md", "本地事件还是分布式事件");

        Assert.Contains("事务提交之后", result);
        Assert.DoesNotContain("发布方不认识订阅方", result);
    }

    /// <summary>
    /// 路径不存在时给出候选建议而不是裸错误
    /// </summary>
    [Fact]
    public void 路径不存在时给出建议()
    {
        var result = CreateTools().ReadDoc("docs/guide/eventbus.md", section: null);

        Assert.Contains("未找到", result);
        Assert.Contains("event-bus.md", result);
    }

    /// <summary>
    /// 逃逸仓库根的路径被拒绝
    /// </summary>
    [Fact]
    public void 拒绝逃逸路径()
    {
        var result = CreateTools().ReadDoc("../../secrets.txt", section: null);

        Assert.Contains("拒绝", result);
    }

    /// <summary>
    /// 仓库根内但未被索引的文件不返回内容
    /// </summary>
    /// <remarks>
    /// 包含性校验只挡得住逃逸仓库根的路径，挡不住仓库内的源码与配置。
    /// 工具的描述写的是「读取曦寒框架的一篇文档」，能力边界必须与描述一致，
    /// 否则一旦按 README 的扩展点把 Tools 层搬到网络传输后面，这就是一个仓库外泄端点。
    /// 断言正文没被带出来，而不只是断言有「未找到」字样——后者一个照读不误但顺手加句提示的实现也能过。
    /// <para>
    /// 先断言夹具文件确实存在：这条测试的全部意义在于「文件就在那儿，但工具不给读」。
    /// 哪天有人清理夹具删掉构造函数里写它的那几行，File.Exists 为假会让实现走进
    /// 「路径不存在」分支，两条断言同时满足，测试**空转变绿**——那正是这个分支被咬过三次的病。
    /// </para>
    /// </remarks>
    [Fact]
    public void 拒绝读取未被索引的仓库内文件()
    {
        var secret = Path.Combine(_root, "framework", "src", "appsettings.Production.json");
        Assert.True(File.Exists(secret), $"夹具文件 {secret} 不存在，这条测试会在空转的情况下变绿。");

        var tools = CreateTools();
        var result = tools.ReadDoc("framework/src/appsettings.Production.json", section: null);

        Assert.DoesNotContain("绝密连接串", result);
        Assert.Contains("未找到", result);

        // 同一个工具实例读一篇被索引的文档仍然正常：证明上面的「未找到」来自白名单，
        // 而不是整个 read_doc 坏掉了——一个恒返回「未找到」的实现同样能满足前两条断言
        Assert.Contains("发布方不认识订阅方", tools.ReadDoc("docs/guide/event-bus.md", section: null));
    }

    /// <summary>
    /// 同一篇文档的等价写法都能读到，不因写法差异退化成「未找到」
    /// </summary>
    /// <remarks>
    /// 白名单比对的基准必须是解析后的绝对路径。拿使用者原串比的话，这几种写法
    /// 都能通过「没逃出仓库根」的包含性校验、却匹配不上白名单里的规范相对路径，
    /// 于是一篇明明被索引了的文档被答成「未找到」——不是安全问题，是纯粹的能用性缺陷，
    /// 而且症状（未找到）会把排查引向完全错误的方向。
    /// </remarks>
    /// <param name="path">等价写法</param>
    [Theory]
    [InlineData("docs/guide/event-bus.md")]
    [InlineData("./docs/guide/event-bus.md")]
    [InlineData("docs//guide/event-bus.md")]
    [InlineData("docs/./guide/event-bus.md")]
    [InlineData("docs/packages/../guide/event-bus.md")]
    public void 路径的等价写法都能读到同一篇文档(string path)
    {
        var result = CreateTools().ReadDoc(path, section: null);

        Assert.Contains("发布方不认识订阅方", result);
    }

    /// <summary>
    /// 白名单对大小写的态度与包含性校验保持一致
    /// </summary>
    /// <remarks>
    /// Windows 上大小写不敏感，`DOCS/GUIDE/...` 指的就是同一个文件，读得到才对；
    /// 类 Unix 上那是另一个并不存在的路径，答「未找到」才对。
    /// 两道关卡各写一遍平台判断迟早会漏改一处，所以它们共用
    /// <see cref="DocSourceLocator.PathComparison"/>，这条断言钉住二者不会分家。
    /// </remarks>
    [Fact]
    public void 大小写写法按平台判定()
    {
        var result = CreateTools().ReadDoc("DOCS/GUIDE/EVENT-BUS.MD", section: null);

        if (OperatingSystem.IsWindows())
        {
            Assert.Contains("发布方不认识订阅方", result);
        }
        else
        {
            Assert.Contains("未找到", result);
            Assert.DoesNotContain("发布方不认识订阅方", result);
        }
    }

    /// <summary>
    /// 换个写法也绕不过白名单
    /// </summary>
    /// <remarks>
    /// 上面几条放宽了写法的容忍度，这条确认放宽的只是写法而不是边界：
    /// 同样用 `./` 与 `..` 绕，未被索引的仓库内文件依然读不出来。
    /// </remarks>
    [Fact]
    public void 等价写法绕不过白名单()
    {
        var result = CreateTools().ReadDoc("./framework/src/../src/appsettings.Production.json", section: null);

        Assert.DoesNotContain("绝密连接串", result);
        Assert.Contains("未找到", result);
    }

    /// <summary>
    /// 默认列表不展开章节标题，控制 token 消耗
    /// </summary>
    [Fact]
    public void 默认列表不展开章节()
    {
        var result = CreateTools().ListDocs(source: null, includeSections: false);

        Assert.Contains("docs/guide/event-bus.md", result);
        Assert.DoesNotContain("本地事件还是分布式事件", result);
    }

    /// <summary>
    /// 显式要求时展开章节标题
    /// </summary>
    [Fact]
    public void 显式要求时展开章节()
    {
        var result = CreateTools().ListDocs(source: null, includeSections: true);

        Assert.Contains("本地事件还是分布式事件", result);
    }

    /// <summary>
    /// 构造被测工具层
    /// </summary>
    private DocsMcpTools CreateTools()
    {
        var locator = new DocSourceLocator(_root);
        var options = new DocsMcpOptions();
        var index = new DocIndex(locator, options, TimeProvider.System, NullLogger<DocIndex>.Instance);

        return new DocsMcpTools(
            index,
            locator,
            SynonymExpander.Load(jsonPath: null, NullLogger.Instance),
            new SectionScorer(options),
            new RelevanceGate(options),
            options);
    }
}
