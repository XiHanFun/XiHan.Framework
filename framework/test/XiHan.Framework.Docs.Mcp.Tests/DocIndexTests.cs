// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Docs.Mcp.Indexing;
using XiHan.Framework.Docs.Mcp.Options;
using XiHan.Framework.Docs.Mcp.Sources;

namespace XiHan.Framework.Docs.Mcp.Tests;

/// <summary>
/// 索引门面与热更新测试
/// </summary>
public class DocIndexTests : IDisposable
{
    private readonly string _root;
    private readonly string _guidePath;

    /// <summary>
    /// 构造一个最小仓库结构
    /// </summary>
    public DocIndexTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "xihan-docindex-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "docs", "guide"));
        Directory.CreateDirectory(Path.Combine(_root, "framework", "src"));

        _guidePath = Path.Combine(_root, "docs", "guide", "event-bus.md");
        File.WriteAllText(_guidePath, "# 事件总线\n\n## 本地事件\n\n最初的内容。\n");
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
    /// 首次调用即建立索引
    /// </summary>
    [Fact]
    public void 首次调用建立索引()
    {
        var (index, _) = CreateIndex();

        var snapshot = index.EnsureFresh();

        Assert.NotEmpty(snapshot.Sections);
        Assert.Contains(snapshot.Sections, s => s.Heading == "本地事件");
    }

    /// <summary>
    /// 节流窗口内的重复调用不重新扫描
    /// </summary>
    [Fact]
    public void 节流窗口内不重复扫描()
    {
        var (index, _) = CreateIndex();
        index.EnsureFresh();

        File.WriteAllText(_guidePath, "# 事件总线\n\n## 全新章节\n\n改过的内容。\n");
        var snapshot = index.EnsureFresh();

        Assert.DoesNotContain(snapshot.Sections, s => s.Heading == "全新章节");
    }

    /// <summary>
    /// 超过节流窗口且文件变化时重建索引
    /// </summary>
    [Fact]
    public void 文件变化后重建索引()
    {
        var (index, time) = CreateIndex();
        index.EnsureFresh();

        File.WriteAllText(_guidePath, "# 事件总线\n\n## 全新章节\n\n改过的内容。\n");
        File.SetLastWriteTimeUtc(_guidePath, DateTime.UtcNow.AddMinutes(1));
        time.Advance(TimeSpan.FromSeconds(5));
        var snapshot = index.EnsureFresh();

        Assert.Contains(snapshot.Sections, s => s.Heading == "全新章节");
    }

    /// <summary>
    /// 新增文件后被纳入索引
    /// </summary>
    [Fact]
    public void 新增文件被纳入索引()
    {
        var (index, time) = CreateIndex();
        index.EnsureFresh();

        File.WriteAllText(
            Path.Combine(_root, "docs", "guide", "caching.md"),
            "# 缓存\n\n## 分布式缓存\n\n缓存正文。\n");
        time.Advance(TimeSpan.FromSeconds(5));
        var snapshot = index.EnsureFresh();

        Assert.Contains(snapshot.Sections, s => s.RelativePath == "docs/guide/caching.md");
    }

    /// <summary>
    /// 重建索引时记下文件数、章节数与耗时
    /// </summary>
    /// <remarks>
    /// 重建是同步的：撞上它的那次查询要把整次重建的时间算进自己头上。
    /// 没有 <c>ElapsedMs</c> 这个字段，「今天查询怎么这么慢」就没法回答。
    /// </remarks>
    [Fact]
    public void 重建索引时记下文件数章节数与耗时()
    {
        // 再加一篇两节的文档，好让文件数与章节数取到不同的值——
        // 两者相等的话，把两个字段写反了本用例也照样绿
        File.WriteAllText(
            Path.Combine(_root, "docs", "guide", "caching.md"),
            "# 缓存\n\n## 分布式缓存\n\n正文一。\n\n## 本地缓存\n\n正文二。\n");

        var logger = new CapturingLogger<DocIndex>();
        var index = new DocIndex(
            new DocSourceLocator(_root),
            new DocsMcpOptions(),
            new FakeTimeProvider(),
            logger);

        index.EnsureFresh();

        var entry = Assert.Single(logger.Entries);

        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal(2, entry.Value("FileCount"));
        Assert.Equal(3, entry.Value("SectionCount"));
        Assert.NotNull(entry.Value("ElapsedMs"));
    }

    /// <summary>
    /// 构造被测索引与可控时钟
    /// </summary>
    private (DocIndex Index, FakeTimeProvider Time) CreateIndex()
    {
        var time = new FakeTimeProvider();
        var index = new DocIndex(
            new DocSourceLocator(_root),
            new DocsMcpOptions(),
            time,
            NullLogger<DocIndex>.Instance);

        return (index, time);
    }
}
