// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Docs.Mcp.Indexing;
using XiHan.Framework.Docs.Mcp.Options;
using XiHan.Framework.Docs.Mcp.Search;
using XiHan.Framework.Docs.Mcp.Sources;
using XiHan.Framework.Docs.Mcp.Tools;

namespace XiHan.Framework.Docs.Mcp.Tests;

/// <summary>
/// 工具层的可观测性：三个工具返回给模型的都是普通文本，事后只能靠日志区分到底发生了什么
/// </summary>
/// <remarks>
/// 断言的重点在**结构化字段**而不是渲染出来的那句话：
/// 零命中与相关性截断拒绝返回给客户端的是同一段文字，把它们分开的是 <c>HitCount</c>、<c>Coverage</c> 这些字段。
/// 用插值串写日志一样能凑出好看的消息，但字段不存在，本组用例会直接变红。
/// </remarks>
public class DocsMcpToolsLoggingTests : IDisposable
{
    private readonly string _root;

    /// <summary>
    /// 构造一个最小仓库结构
    /// </summary>
    public DocsMcpToolsLoggingTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "xihan-tools-log-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "docs", "guide"));
        Directory.CreateDirectory(Path.Combine(_root, "docs", "packages"));

        File.WriteAllText(
            Path.Combine(_root, "docs", "guide", "event-bus.md"),
            "# 事件总线\n\n发布方不认识订阅方。\n\n## 本地事件还是分布式事件\n\n分布式事件在事务提交之后发布。\n");
        File.WriteAllText(
            Path.Combine(_root, "docs", "packages", "caching.md"),
            "# 缓存包\n\n## 配置项\n\n缓存过期时间的配置说明。\n");
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

    [Fact]
    public void 检索命中时记下工具名命中数与耗时()
    {
        var (tools, logger) = CreateTools();

        tools.SearchDocs("分布式事件什么时候发布", source: null, limit: 5);

        var entry = Assert.Single(logger.Entries);

        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("search_docs", entry.Value("Tool"));
        Assert.Equal(2, entry.Value("HitCount"));
        Assert.NotNull(entry.Value("ElapsedMs"));

        // 查询串必须在里面：没有它，日志只能告诉你「有人搜了点什么」
        Assert.Equal("分布式事件什么时候发布", entry.Value("Query"));
    }

    /// <summary>
    /// 零命中要单独记一条，而且认得出是零命中
    /// </summary>
    /// <remarks>
    /// 「什么都没找到」是远端最常见的报障，而它多半是正确行为。
    /// 日志里必须能一眼看出这是零命中而不是别的失败，并带上查询串以便复现。
    /// </remarks>
    [Fact]
    public void 零命中单独记一条并带上查询串()
    {
        var (tools, logger) = CreateTools();

        tools.SearchDocs("量子纠缠的宏观表现", source: null, limit: 5);

        var entry = Assert.Single(logger.Entries);

        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("search_docs", entry.Value("Tool"));
        Assert.Equal("量子纠缠的宏观表现", entry.Value("Query"));
        Assert.Contains("零命中", entry.Message, StringComparison.Ordinal);

        // 命中那条带 HitCount，零命中这条不带——两者的字段集不同，才谈得上「区分得开」
        Assert.Null(entry.Value("HitCount"));
    }

    /// <summary>
    /// 相关性截断拒绝时要把覆盖率与阈值一起记下来
    /// </summary>
    /// <remarks>
    /// 0.90 这个阈值是在离线黄金查询集上标定的。要拿真实流量复核它，
    /// 就得知道被拒绝的查询实际落在多少——只记「被拒了」等于没记。
    /// <para>
    /// 这里把 <c>MinSectionsForRelevanceCutoff</c> 调到 1，好让判据在这个几章的小语料上也生效；
    /// 真实语料有一千七百多个章节，本来就在下限之上。
    /// </para>
    /// </remarks>
    [Fact]
    public void 相关性截断拒绝时记下覆盖率()
    {
        var options = new DocsMcpOptions { MinSectionsForRelevanceCutoff = 1 };
        var (tools, logger) = CreateTools(options);

        var result = tools.SearchDocs("缓存的量子纠缠", source: null, limit: 5);

        // 先确认真的走到了「命中了但被拒绝」这条分支，而不是压根没命中
        Assert.Contains("不要基于猜测", result, StringComparison.Ordinal);

        var entry = Assert.Single(logger.Entries);

        Assert.Equal("search_docs", entry.Value("Tool"));
        Assert.Contains("相关性截断", entry.Message, StringComparison.Ordinal);

        var hitCount = Assert.IsType<int>(entry.Value("HitCount"));
        Assert.True(hitCount > 0, $"本用例要的是「命中了但被拒绝」，实际 HitCount = {hitCount}，说明走的是零命中分支。");

        var coverage = Assert.IsType<double>(entry.Value("Coverage"));
        Assert.True(
            coverage < options.MinKnownTermCoverage,
            $"被拒绝的查询覆盖率应低于阈值 {options.MinKnownTermCoverage}，实际 {coverage}。");

        Assert.Equal(options.MinKnownTermCoverage, entry.Value("Threshold"));
    }

    [Fact]
    public void 列出文档时记下条数()
    {
        var (tools, logger) = CreateTools();

        tools.ListDocs(source: null, includeSections: false);

        var entry = Assert.Single(logger.Entries);

        Assert.Equal("list_docs", entry.Value("Tool"));
        Assert.Equal(2, entry.Value("FileCount"));
        Assert.NotNull(entry.Value("ElapsedMs"));
    }

    [Fact]
    public void 读取文档时记下结果分类()
    {
        var (tools, logger) = CreateTools();

        tools.ReadDoc("docs/guide/event-bus.md", section: null);

        var entry = Assert.Single(logger.Entries);

        Assert.Equal("read_doc", entry.Value("Tool"));
        Assert.Equal("返回全文", entry.Value("Outcome"));
        Assert.Equal("docs/guide/event-bus.md", entry.Value("Path"));
    }

    /// <summary>
    /// 越界路径按警告级别记录，与「路径写错了」区分开
    /// </summary>
    [Fact]
    public void 越界路径按警告记录()
    {
        var (tools, logger) = CreateTools();

        tools.ReadDoc("../../etc/passwd", section: null);

        var entry = Assert.Single(logger.Entries);

        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("read_doc", entry.Value("Tool"));
        Assert.Equal("../../etc/passwd", entry.Value("Path"));
    }

    /// <summary>
    /// 未在索引内的路径记 Information，与越界的 Warning 是两回事
    /// </summary>
    [Fact]
    public void 未在索引内的路径记为普通结果()
    {
        var (tools, logger) = CreateTools();

        tools.ReadDoc("docs/guide/不存在的文档.md", section: null);

        var entry = Assert.Single(logger.Entries);

        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("未在索引内", entry.Value("Outcome"));
    }

    /// <summary>
    /// search_docs 抛异常时按 Error 记录，并带上异常类型与消息
    /// </summary>
    /// <remarks>
    /// 三个工具都把异常吞成一段说明文字返回给模型，客户端那边只看得到「发生错误」。
    /// 不记这一条的话，工具抛异常与真的没搜到在远端长得一模一样。
    /// <para>
    /// 制造异常的办法：把 <c>MaxLimit</c> 配成 0，条数夹取那句
    /// <c>Math.Clamp(x, 1, 0)</c> 的下界大于上界，必抛 <see cref="ArgumentException"/>。
    /// 这不是硬凑的场景——它就是一份配错了的 <see cref="DocsMcpOptions"/> 会走到的地方。
    /// </para>
    /// </remarks>
    [Fact]
    public void 检索抛异常时按错误级别记录()
    {
        var (tools, logger) = CreateTools(new DocsMcpOptions { MaxLimit = 0 });

        var result = tools.SearchDocs("分布式事件", source: null, limit: 5);

        Assert.Contains("发生错误", result, StringComparison.Ordinal);

        var entry = Assert.Single(logger.Entries);

        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal("search_docs", entry.Value("Tool"));
        Assert.Equal(typeof(ArgumentException).FullName, entry.Value("ExceptionType"));
        Assert.NotNull(entry.Value("ExceptionMessage"));

        // 异常对象本身也要挂上去，否则结构化后端里没有堆栈
        Assert.NotNull(entry.Exception);
    }

    /// <summary>
    /// read_doc 抛异常时同样按 Error 记录
    /// </summary>
    /// <remarks>
    /// 路径里带一个空字符，<c>Path.GetFullPath</c> 会抛 <see cref="ArgumentException"/>——
    /// 三个平台上行为一致，不依赖文件系统的临时状态。
    /// </remarks>
    [Fact]
    public void 读取抛异常时按错误级别记录()
    {
        var (tools, logger) = CreateTools();

        var result = tools.ReadDoc("docs/guide/\0event-bus.md", section: null);

        Assert.Contains("发生错误", result, StringComparison.Ordinal);

        var entry = Assert.Single(logger.Entries);

        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal("read_doc", entry.Value("Tool"));
        Assert.Equal(typeof(ArgumentException).FullName, entry.Value("ExceptionType"));
        Assert.NotNull(entry.Exception);
    }

    /// <summary>
    /// 构造被测工具层与捕获日志
    /// </summary>
    private (DocsMcpTools Tools, CapturingLogger<DocsMcpTools> Logger) CreateTools(DocsMcpOptions? options = null)
    {
        var effective = options ?? new DocsMcpOptions();
        var locator = new DocSourceLocator(_root);
        var index = new DocIndex(locator, effective, TimeProvider.System, NullLogger<DocIndex>.Instance);
        var logger = new CapturingLogger<DocsMcpTools>();

        var tools = new DocsMcpTools(
            index,
            locator,
            SynonymExpander.Load(jsonPath: null, NullLogger.Instance),
            new SectionScorer(effective),
            new RelevanceGate(effective),
            effective,
            logger);

        return (tools, logger);
    }
}
