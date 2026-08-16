// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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

        index.EnsureFresh();

        Assert.NotEmpty(index.Sections);
        Assert.Contains(index.Sections, s => s.Heading == "本地事件");
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
        index.EnsureFresh();

        Assert.DoesNotContain(index.Sections, s => s.Heading == "全新章节");
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
        index.EnsureFresh();

        Assert.Contains(index.Sections, s => s.Heading == "全新章节");
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
        index.EnsureFresh();

        Assert.Contains(index.Sections, s => s.RelativePath == "docs/guide/caching.md");
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
