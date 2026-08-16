// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Docs.Mcp.Sources;

namespace XiHan.Framework.Docs.Mcp.Tests;

/// <summary>
/// 仓库根定位与来源枚举测试
/// </summary>
public class DocSourceLocatorTests : IDisposable
{
    private readonly string _root;

    /// <summary>
    /// 构造一个最小的仓库结构用于测试
    /// </summary>
    public DocSourceLocatorTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "xihan-docs-mcp-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(Path.Combine(_root, "docs", "guide"));
        Directory.CreateDirectory(Path.Combine(_root, "docs", "packages"));
        Directory.CreateDirectory(Path.Combine(_root, "docs", ".vitepress"));
        Directory.CreateDirectory(Path.Combine(_root, "framework", "src", "XiHan.Framework.Utils"));
        Directory.CreateDirectory(Path.Combine(_root, "framework", "src", "XiHan.Framework.Core", "obj"));

        File.WriteAllText(Path.Combine(_root, "docs", "guide", "event-bus.md"), "# 事件总线");
        File.WriteAllText(Path.Combine(_root, "docs", "packages", "eventbus.md"), "# EventBus");
        File.WriteAllText(Path.Combine(_root, "docs", "quickstart.md"), "# 快速开始");

        // 排序名次（第 1）与收集名次（第 3，顶层 docs 排在 guide、packages 之后）刻意不一致，
        // 否则收集顺序恰好等于排序结果，排序被删掉也没有任何断言会红
        File.WriteAllText(Path.Combine(_root, "docs", "changelog.md"), "# 更新日志");
        File.WriteAllText(Path.Combine(_root, "docs", ".vitepress", "config.md"), "# 不该被索引");
        File.WriteAllText(Path.Combine(_root, "framework", "src", "XiHan.Framework.Utils", "README.md"), "# Utils");
        File.WriteAllText(Path.Combine(_root, "framework", "src", "XiHan.Framework.Core", "obj", "README.md"), "# 不该被索引");
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
    /// 从深层子目录逐层向上找到仓库根
    /// </summary>
    [Fact]
    public void 逐层向上找到仓库根()
    {
        var deep = Path.Combine(_root, "framework", "src", "XiHan.Framework.Utils");

        var resolved = DocSourceLocator.ResolveRepositoryRoot(deep, environmentOverride: null);

        Assert.Equal(Path.GetFullPath(_root), Path.GetFullPath(resolved));
    }

    /// <summary>
    /// 环境变量指定的路径优先于向上查找
    /// </summary>
    [Fact]
    public void 环境变量优先()
    {
        var resolved = DocSourceLocator.ResolveRepositoryRoot(Path.GetTempPath(), _root);

        Assert.Equal(Path.GetFullPath(_root), Path.GetFullPath(resolved));
    }

    /// <summary>
    /// 找不到仓库根时抛出专用异常，而不是静默返回空索引
    /// </summary>
    [Fact]
    public void 找不到仓库根时抛出()
    {
        var isolated = Path.Combine(Path.GetTempPath(), "xihan-empty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(isolated);

        try
        {
            Assert.Throws<DocsRootNotFoundException>(
                () => DocSourceLocator.ResolveRepositoryRoot(isolated, environmentOverride: null));
        }
        finally
        {
            Directory.Delete(isolated, recursive: true);
        }
    }

    /// <summary>
    /// 四类来源各自被正确分类
    /// </summary>
    [Fact]
    public void 四类来源分类正确()
    {
        var files = new DocSourceLocator(_root).Enumerate();

        Assert.Contains(files, f => f.RelativePath == "docs/guide/event-bus.md" && f.Source == DocSourceKind.Guide);
        Assert.Contains(files, f => f.RelativePath == "docs/packages/eventbus.md" && f.Source == DocSourceKind.Package);
        Assert.Contains(files, f => f.RelativePath == "docs/quickstart.md" && f.Source == DocSourceKind.Root);
        Assert.Contains(files, f => f.RelativePath == "framework/src/XiHan.Framework.Utils/README.md" && f.Source == DocSourceKind.PackageReadme);
    }

    /// <summary>
    /// docs 下只取 guide 与 packages 两个子目录，其余子目录不递归
    /// </summary>
    [Fact]
    public void 不递归扫描其他文档子目录()
    {
        var files = new DocSourceLocator(_root).Enumerate();

        Assert.DoesNotContain(files, f => f.RelativePath.Contains(".vitepress"));
    }

    /// <summary>
    /// 中间产物目录下的 README 不被索引
    /// </summary>
    [Fact]
    public void 跳过中间产物目录()
    {
        var files = new DocSourceLocator(_root).Enumerate();

        Assert.DoesNotContain(files, f => f.RelativePath.Contains("/obj/"));
    }

    /// <summary>
    /// 相对路径统一使用正斜杠，保证跨平台输出一致
    /// </summary>
    /// <remarks>
    /// 断言的是嵌套路径的**完整期望字符串**，而不只是「不含反斜杠」：后者在 Windows 上
    /// 也能报出问题，但报的是一句「不该有 '\\'」，看不出是哪一段路径拼错；
    /// 前者直接给出「期望 a，实际 b」。
    /// <para>
    /// 这条断言只在 Windows 上真正生效，且只能如此：类 Unix 上
    /// <c>Path.GetRelativePath</c> 本就返回正斜杠，规范化是一次恒等替换——
    /// 把它删掉之后，Linux 上没有任何可观测差异，也就不存在能在 Linux 上变红的写法。
    /// 需要这条断言的平台恰好就是它能生效的平台：CI 跑在 ubuntu 上不会拦住这个退化，
    /// 本机开发者跑一次就会。
    /// </para>
    /// </remarks>
    [Fact]
    public void 相对路径统一正斜杠()
    {
        var files = new DocSourceLocator(_root).Enumerate();

        var readme = Assert.Single(files, f => f.Source == DocSourceKind.PackageReadme);
        Assert.Equal("framework/src/XiHan.Framework.Utils/README.md", readme.RelativePath);

        Assert.All(files, f => Assert.DoesNotContain('\\', f.RelativePath));
    }

    /// <summary>
    /// 枚举结果按相对路径的序数序排序
    /// </summary>
    /// <remarks>
    /// 排序不是锦上添花：<c>Directory.EnumerateFiles</c> 的返回顺序由文件系统决定，
    /// NTFS 上按名字、ext4 上按哈希，不排序的话 <c>list_docs</c> 的输出会随平台甚至
    /// 随文件创建顺序漂移，两次调用的差异无从解释。
    /// <para>
    /// 断言整条序列而不是逐项存在：只用 <c>Assert.Contains</c> 的话，
    /// 把 <c>OrderBy</c> 整个删掉，一条断言都不会红。
    /// </para>
    /// </remarks>
    [Fact]
    public void 枚举结果按相对路径排序()
    {
        var files = new DocSourceLocator(_root).Enumerate();

        string[] expected =
        [
            "docs/changelog.md",
            "docs/guide/event-bus.md",
            "docs/packages/eventbus.md",
            "docs/quickstart.md",
            "framework/src/XiHan.Framework.Utils/README.md"
        ];

        Assert.Equal(expected, files.Select(f => f.RelativePath));
    }

    /// <summary>
    /// 逃逸仓库根的路径被拒绝
    /// </summary>
    [Fact]
    public void 拒绝逃逸仓库根的路径()
    {
        var locator = new DocSourceLocator(_root);

        Assert.False(locator.TryResolveDocumentPath("../../etc/passwd", out _));
    }

    /// <summary>
    /// 仓库根内的合法路径可以解析
    /// </summary>
    [Fact]
    public void 接受仓库根内的合法路径()
    {
        var locator = new DocSourceLocator(_root);

        Assert.True(locator.TryResolveDocumentPath("docs/guide/event-bus.md", out var absolute));
        Assert.True(File.Exists(absolute));
    }

    /// <summary>
    /// 名称以仓库根名为前缀的兄弟目录必须被拒绝
    /// </summary>
    /// <remarks>
    /// 这是前缀末尾补目录分隔符的意义所在：少了那个分隔符，
    /// 「xihan-abc-evil」会被当成落在「xihan-abc」之内。
    /// </remarks>
    [Fact]
    public void 拒绝同名前缀的兄弟目录()
    {
        var locator = new DocSourceLocator(_root);
        var sibling = Path.GetFileName(_root) + "-evil";

        Assert.False(locator.TryResolveDocumentPath($"../{sibling}/secret.txt", out _));
    }

    /// <summary>
    /// 仅大小写不同的兄弟目录：Windows 上指向同一目录，类 Unix 上是两个目录必须拒绝
    /// </summary>
    [Fact]
    public void 大小写不同的兄弟目录按平台判定()
    {
        var locator = new DocSourceLocator(_root);
        var uppercased = Path.GetFileName(_root).ToUpperInvariant();

        var accepted = locator.TryResolveDocumentPath($"../{uppercased}/secret.txt", out _);

        Assert.Equal(OperatingSystem.IsWindows(), accepted);
    }

    /// <summary>
    /// 环境变量指向的目录不是仓库根时直接抛出，不静默回退到逐层向上查找
    /// </summary>
    [Fact]
    public void 环境变量无效时抛出而不回退()
    {
        var invalid = Path.Combine(Path.GetTempPath(), "xihan-invalid-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(invalid);

        try
        {
            // 起点目录本身就是合法仓库根：一旦实现悄悄回退到向上查找，这里会正常返回而不是抛出
            Assert.Throws<DocsRootNotFoundException>(
                () => DocSourceLocator.ResolveRepositoryRoot(_root, invalid));
        }
        finally
        {
            Directory.Delete(invalid, recursive: true);
        }
    }
}
