// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using System.Text;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.VirtualFileSystem.Options;
using XiHan.Framework.VirtualFileSystem.Providers.Physical;
using XiHan.Framework.VirtualFileSystem.Tests.TestSupport;
using VirtualFileSystemCore = XiHan.Framework.VirtualFileSystem.VirtualFileSystem;

namespace XiHan.Framework.VirtualFileSystem.Tests;

/// <summary>
/// 虚拟文件系统核心实现测试
/// </summary>
/// <remarks>
/// 用例一律关闭 IncludeCurrentDirectory / IncludeAppBaseDirectory / EnableChangeTracking：
/// 前两者会把测试宿主的输出目录整个挂进来，断言就不再可控；后者会在构造时递归扫描全目录，
/// 既慢又与用例无关。变更追踪本身依赖 FileSystemWatcher 与防抖计时，属于时序敏感区，另行说明未覆盖。
/// </remarks>
public class VirtualFileSystemTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly string _root;
    private readonly string _outsideFile;

    /// <summary>
    /// 构造函数
    /// </summary>
    public VirtualFileSystemTests()
    {
        _root = _temp.CreateSubDirectory("root");
        _outsideFile = _temp.WriteFile("outside.txt", "secret");
        _temp.WriteFile("root/a.txt", "root-a");
        _temp.WriteFile("root/sub/b.json", "root-b");
        _temp.WriteFile("root/sub/c.txt", "root-c");
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _temp.Dispose();
    }

    /// <summary>
    /// 配置为空时抛参数空异常，且不会被包装成框架异常
    /// </summary>
    [Fact]
    public void Constructor_WhenOptionsIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _ = new VirtualFileSystemCore(null!));
    }

    /// <summary>
    /// 初始化过程中的任何失败都被包装成框架异常，并保留内部异常
    /// </summary>
    /// <remarks>
    /// 用一个已被文件占用的路径当附加物理目录：自动建目录会失败，从而触发初始化异常包装。
    /// </remarks>
    [Fact]
    public void Constructor_WhenProviderInitializationFails_WrapsIntoXiHanException()
    {
        var options = CreateOptions();
        options.AdditionalPhysicalPaths.Add(_outsideFile);

        var exception = Assert.Throws<XiHanException>(
            () => _ = new VirtualFileSystemCore(new OptionsWrapper<VirtualFileSystemOptions>(options)));

        Assert.NotNull(exception.InnerException);
    }

    /// <summary>
    /// 没有挂载任何提供程序时所有查询都返回空结果而不是抛异常
    /// </summary>
    [Fact]
    public void Queries_WhenNoProviderMounted_ReturnEmptyResults()
    {
        using var sut = CreateSut();

        Assert.False(sut.GetFile("/a.txt").Exists);
        Assert.False(sut.FileExists("/a.txt"));
        Assert.False(sut.DirectoryExists("/"));
        Assert.Empty(sut.EnumerateFiles("/"));
    }

    /// <summary>
    /// 等价的多种路径写法都指向同一个文件
    /// </summary>
    [Theory]
    [InlineData("a.txt")]
    [InlineData("/a.txt")]
    [InlineData("~/a.txt")]
    [InlineData("\\a.txt")]
    [InlineData("/a.txt/")]
    [InlineData("  /a.txt  ")]
    public void FileExists_ForEquivalentPathForms_ReturnsTrue(string virtualPath)
    {
        using var sut = CreateSut(options => options.AddPhysical(_root, 100));

        Assert.True(sut.FileExists(virtualPath));
    }

    /// <summary>
    /// 能读到挂载目录里的真实内容
    /// </summary>
    [Fact]
    public void GetFile_ForMountedPhysicalRoot_ReadsRealContent()
    {
        using var sut = CreateSut(options => options.AddPhysical(_root, 100));

        Assert.Equal("root-a", ReadAllText(sut.GetFile("/a.txt")));
        Assert.Equal("root-b", ReadAllText(sut.GetFile("/sub/b.json")));
    }

    /// <summary>
    /// 目录不会被判定为文件
    /// </summary>
    [Fact]
    public void FileExists_ForDirectoryPath_ReturnsFalse()
    {
        using var sut = CreateSut(options => options.AddPhysical(_root, 100));

        Assert.False(sut.FileExists("/sub"));
    }

    /// <summary>
    /// 目录存在性判定只认目录
    /// </summary>
    [Theory]
    [InlineData("/sub", true)]
    [InlineData("/", true)]
    [InlineData("/missing", false)]
    [InlineData("/a.txt", false)]
    public void DirectoryExists_DistinguishesDirectoriesFromFiles(string virtualPath, bool expected)
    {
        using var sut = CreateSut(options => options.AddPhysical(_root, 100));

        Assert.Equal(expected, sut.DirectoryExists(virtualPath));
    }

    /// <summary>
    /// 目录内容列出顶层条目（含子目录）
    /// </summary>
    [Fact]
    public void GetDirectoryContents_ForRoot_ListsTopLevelEntries()
    {
        using var sut = CreateSut(options => options.AddPhysical(_root, 100));

        var names = sut.GetDirectoryContents("/")
            .Select(x => x.Name)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "a.txt", "sub" }, names);
    }

    /// <summary>
    /// 默认递归枚举所有文件，结果按虚拟路径排序
    /// </summary>
    [Fact]
    public void EnumerateFiles_ByDefault_ReturnsAllFilesSorted()
    {
        using var sut = CreateSut(options => options.AddPhysical(_root, 100));

        Assert.Equal(["/a.txt", "/sub/b.json", "/sub/c.txt"], sut.EnumerateFiles("/"));
    }

    /// <summary>
    /// 关闭递归时只返回当前层的文件
    /// </summary>
    [Fact]
    public void EnumerateFiles_WhenNotRecursive_ReturnsOnlyTopLevelFiles()
    {
        using var sut = CreateSut(options => options.AddPhysical(_root, 100));

        Assert.Equal(["/a.txt"], sut.EnumerateFiles("/", "*", recursive: false));
    }

    /// <summary>
    /// 通配符按文件名匹配
    /// </summary>
    [Fact]
    public void EnumerateFiles_WithNamePattern_FiltersByFileName()
    {
        using var sut = CreateSut(options => options.AddPhysical(_root, 100));

        Assert.Equal(["/sub/b.json"], sut.EnumerateFiles("/", "*.json"));
    }

    /// <summary>
    /// 双星通配符按完整虚拟路径匹配
    /// </summary>
    [Fact]
    public void EnumerateFiles_WithGlobPattern_FiltersByFullVirtualPath()
    {
        using var sut = CreateSut(options => options.AddPhysical(_root, 100));

        Assert.Equal(["/sub/b.json"], sut.EnumerateFiles("/", "**/*.json"));
    }

    /// <summary>
    /// 指定子目录时只枚举该子目录
    /// </summary>
    [Fact]
    public void EnumerateFiles_ForSubDirectory_ScopesToThatDirectory()
    {
        using var sut = CreateSut(options => options.AddPhysical(_root, 100));

        Assert.Equal(["/sub/b.json", "/sub/c.txt"], sut.EnumerateFiles("/sub"));
    }

    /// <summary>
    /// 目录不存在时返回空集合而不是抛异常
    /// </summary>
    [Fact]
    public void EnumerateFiles_ForMissingDirectory_ReturnsEmpty()
    {
        using var sut = CreateSut(options => options.AddPhysical(_root, 100));

        Assert.Empty(sut.EnumerateFiles("/missing"));
    }

    /// <summary>
    /// 多个提供程序中的同名文件在枚举结果里只出现一次
    /// </summary>
    [Fact]
    public void EnumerateFiles_AcrossProviders_DeduplicatesVirtualPaths()
    {
        var second = _temp.CreateSubDirectory("second");
        _temp.WriteFile("second/a.txt", "second-a");
        _temp.WriteFile("second/d.txt", "second-d");

        using var sut = CreateSut(options =>
        {
            options.AddPhysical(_root, 100);
            options.AddPhysical(second, 10);
        });

        Assert.Equal(["/a.txt", "/d.txt"], sut.EnumerateFiles("/", "*", recursive: false));
    }

    /// <summary>
    /// 同名文件由优先级更高的提供程序胜出
    /// </summary>
    [Fact]
    public void GetFile_WhenProvidersOverlap_HighestPriorityWins()
    {
        var second = _temp.CreateSubDirectory("second");
        _temp.WriteFile("second/a.txt", "second-a");

        using var sut = CreateSut(options =>
        {
            options.AddPhysical(_root, 10);
            options.AddPhysical(second, 90);
        });

        Assert.Equal("second-a", ReadAllText(sut.GetFile("/a.txt")));
    }

    /// <summary>
    /// 挂载空提供程序抛参数空异常
    /// </summary>
    [Fact]
    public void Mount_WhenProviderIsNull_Throws()
    {
        using var sut = CreateSut();

        Assert.Throws<ArgumentNullException>(() => sut.Mount(null!));
    }

    /// <summary>
    /// 运行期挂载更高优先级的提供程序会覆盖已有内容，卸载后回到原内容
    /// </summary>
    [Fact]
    public void MountAndUnmount_HigherPriorityProvider_OverridesThenRestores()
    {
        var overrideRoot = _temp.CreateSubDirectory("override");
        _temp.WriteFile("override/a.txt", "override-a");

        using var sut = CreateSut(options => options.AddPhysical(_root, 10));
        Assert.Equal("root-a", ReadAllText(sut.GetFile("/a.txt")));

        using var provider = new VirtualPhysicalFileProvider(overrideRoot, 900);
        sut.Mount(provider, 900);
        Assert.Equal("override-a", ReadAllText(sut.GetFile("/a.txt")));

        Assert.True(sut.Unmount(provider));
        Assert.Equal("root-a", ReadAllText(sut.GetFile("/a.txt")));
    }

    /// <summary>
    /// 同一物理根重复挂载按根路径去重，不会叠加成两条
    /// </summary>
    /// <remarks>
    /// 提供程序身份按物理根路径识别而非实例引用：卸载任一实例都会移除那条唯一登记。
    /// 若去重失效，这里第一次卸载后文件仍能读到，用例会失败。
    /// </remarks>
    [Fact]
    public void Mount_WithSameRootTwice_ReplacesInsteadOfDuplicating()
    {
        var extraRoot = _temp.CreateSubDirectory("extra");
        _temp.WriteFile("extra/x.txt", "extra-x");

        using var sut = CreateSut();
        using var first = new VirtualPhysicalFileProvider(extraRoot, 10);
        using var second = new VirtualPhysicalFileProvider(extraRoot, 20);

        sut.Mount(first, 10);
        sut.Mount(second, 20);
        Assert.True(sut.FileExists("/x.txt"));

        Assert.True(sut.Unmount(first));
        Assert.False(sut.FileExists("/x.txt"));
    }

    /// <summary>
    /// 卸载从未挂载过的提供程序返回 false
    /// </summary>
    [Fact]
    public void Unmount_WhenProviderNeverMounted_ReturnsFalse()
    {
        var otherRoot = _temp.CreateSubDirectory("other");

        using var sut = CreateSut(options => options.AddPhysical(_root, 10));
        using var other = new VirtualPhysicalFileProvider(otherRoot, 5);

        Assert.False(sut.Unmount(other));
    }

    /// <summary>
    /// 卸载空提供程序抛参数空异常
    /// </summary>
    [Fact]
    public void Unmount_WhenProviderIsNull_Throws()
    {
        using var sut = CreateSut();

        Assert.Throws<ArgumentNullException>(() => _ = sut.Unmount(null!));
    }

    /// <summary>
    /// 监视返回可用的变更令牌，过滤条件为空时退化为全量过滤
    /// </summary>
    [Fact]
    public void Watch_ReturnsChangeToken()
    {
        using var sut = CreateSut();

        Assert.NotNull(sut.Watch("**/*.json"));
        Assert.NotNull(sut.Watch(null!));
        Assert.NotNull(sut.Watch("   "));
    }

    /// <summary>
    /// 释放后再访问抛对象已释放异常
    /// </summary>
    [Fact]
    public void Members_AfterDispose_ThrowObjectDisposedException()
    {
        var sut = CreateSut(options => options.AddPhysical(_root, 10));
        sut.Dispose();

        Assert.Throws<ObjectDisposedException>(() => _ = sut.GetFile("/a.txt"));
        Assert.Throws<ObjectDisposedException>(() => _ = sut.GetDirectoryContents("/"));
        Assert.Throws<ObjectDisposedException>(() => _ = sut.EnumerateFiles("/"));
        Assert.Throws<ObjectDisposedException>(() => _ = sut.Watch("*"));
        Assert.Throws<ObjectDisposedException>(() => sut.Mount(new NullFileProvider()));
        Assert.Throws<ObjectDisposedException>(() => _ = sut.Unmount(new NullFileProvider()));
    }

    /// <summary>
    /// 重复释放不抛异常
    /// </summary>
    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var sut = CreateSut(options => options.AddPhysical(_root, 10));

        sut.Dispose();
        sut.Dispose();
    }

    /// <summary>
    /// 各种形态的 ../ 都读不到挂载根之外的文件
    /// </summary>
    /// <remarks>
    /// 这是虚拟文件系统对外承诺的安全边界：虚拟路径只能落在已挂载的根内。
    /// 先断言越界目标真实存在，确保用例不是因为目标缺失而「碰巧」通过。
    /// </remarks>
    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("/../outside.txt")]
    [InlineData("~/../outside.txt")]
    [InlineData("..\\outside.txt")]
    [InlineData("sub/../../outside.txt")]
    public void FileExists_ForTraversalPath_StaysInsideMountedRoot(string virtualPath)
    {
        Assert.True(File.Exists(_outsideFile));
        using var sut = CreateSut(options => options.AddPhysical(_root, 100));

        Assert.False(sut.FileExists(virtualPath));
        Assert.False(sut.GetFile(virtualPath).Exists);
    }

    /// <summary>
    /// 枚举结果不会泄漏挂载根之外的文件
    /// </summary>
    [Fact]
    public void EnumerateFiles_DoesNotLeakFilesOutsideMountedRoot()
    {
        using var sut = CreateSut(options => options.AddPhysical(_root, 100));

        var files = sut.EnumerateFiles("/");

        Assert.DoesNotContain(files, x => x.EndsWith("outside.txt", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 实现对外契约接口
    /// </summary>
    [Fact]
    public void Type_ImplementsContracts()
    {
        using var sut = CreateSut();

        Assert.IsAssignableFrom<IVirtualFileSystem>(sut);
        Assert.IsAssignableFrom<IDisposable>(sut);
    }

    private static VirtualFileSystemOptions CreateOptions()
    {
        return new VirtualFileSystemOptions
        {
            IncludeCurrentDirectory = false,
            IncludeAppBaseDirectory = false,
            EnableChangeTracking = false
        };
    }

    private static VirtualFileSystemCore CreateSut(Action<VirtualFileSystemOptions>? configure = null)
    {
        var options = CreateOptions();
        configure?.Invoke(options);
        return new VirtualFileSystemCore(new OptionsWrapper<VirtualFileSystemOptions>(options));
    }

    private static string ReadAllText(IFileInfo file)
    {
        using var stream = file.CreateReadStream();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
