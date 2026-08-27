// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.FileProviders;
using XiHan.Framework.VirtualFileSystem.Providers;
using XiHan.Framework.VirtualFileSystem.Providers.Physical;

namespace XiHan.Framework.VirtualFileSystem.Tests;

/// <summary>
/// 带优先级的物理文件提供程序测试
/// </summary>
/// <remarks>
/// 物理提供器是虚拟文件系统的安全边界所在：任何形态的 ../ 都不允许读到挂载根之外的文件。
/// 用例特地在挂载根的上一级放了一个真实存在的文件，并先断言它确实存在，
/// 否则「读不到」可能只是因为目标压根不存在，测了个寂寞。
/// </remarks>
public class VirtualPhysicalFileProviderTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly string _root;
    private readonly string _outsideFile;

    /// <summary>
    /// 构造函数
    /// </summary>
    public VirtualPhysicalFileProviderTests()
    {
        _root = _temp.CreateSubDirectory("root");
        _outsideFile = _temp.WriteFile("outside.txt", "secret");
        _temp.WriteFile("root/a.txt", "root-a");
        _temp.WriteFile("root/sub/b.json", "root-b");
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _temp.Dispose();
    }

    /// <summary>
    /// 相对路径根目录不被接受
    /// </summary>
    /// <remarks>
    /// 基类 PhysicalFileProvider 先于本类型的检查抛出，所以这里只锁异常类型不锁消息文案。
    /// </remarks>
    [Fact]
    public void Constructor_WhenRootIsRelative_Throws()
    {
        Assert.Throws<ArgumentException>(() => _ = new VirtualPhysicalFileProvider("relative/path"));
    }

    /// <summary>
    /// 根路径被规范化为绝对路径
    /// </summary>
    [Fact]
    public void Root_IsNormalizedAbsolutePath()
    {
        using var provider = new VirtualPhysicalFileProvider(_root);

        Assert.Equal(
            Path.GetFullPath(_root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            provider.Root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 默认优先级为 100
    /// </summary>
    [Fact]
    public void Priority_DefaultsToOneHundred()
    {
        using var provider = new VirtualPhysicalFileProvider(_root);

        Assert.Equal(100, provider.Priority);
    }

    /// <summary>
    /// 可以显式指定优先级
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(55)]
    [InlineData(-5)]
    public void Priority_AcceptsExplicitValue(int priority)
    {
        using var provider = new VirtualPhysicalFileProvider(_root, priority);

        Assert.Equal(priority, provider.Priority);
    }

    /// <summary>
    /// 直接调用时返回带优先级的文件信息，并能读到真实内容
    /// </summary>
    [Fact]
    public void GetFileInfo_OnConcreteType_ReturnsPrioritizedFileInfoWithRealContent()
    {
        using var provider = new VirtualPhysicalFileProvider(_root, 88);

        var file = provider.GetFileInfo("/a.txt");

        Assert.True(file.Exists);
        Assert.False(file.IsDirectory);
        Assert.Equal(88, file.Priority);
        Assert.Equal("root-a", ReadAllText(file));
    }

    /// <summary>
    /// 通过 IFileProvider 接口调用时走基类实现，不带优先级
    /// </summary>
    [Fact]
    public void GetFileInfo_ViaInterface_UsesBaseImplementation()
    {
        IFileProvider provider = new VirtualPhysicalFileProvider(_root, 88);

        var file = provider.GetFileInfo("/a.txt");

        Assert.IsNotType<PrioritizedFileInfo>(file);
        Assert.True(file.Exists);
        Assert.NotNull(file.PhysicalPath);
    }

    /// <summary>
    /// 不存在的文件返回不存在的文件信息，而不是抛异常
    /// </summary>
    [Fact]
    public void GetFileInfo_ForMissingFile_ReturnsNonExistingInfo()
    {
        using var provider = new VirtualPhysicalFileProvider(_root);

        Assert.False(provider.GetFileInfo("/nope.txt").Exists);
    }

    /// <summary>
    /// 目录不会被当成文件
    /// </summary>
    [Fact]
    public void GetFileInfo_ForDirectory_IsNotAnExistingFile()
    {
        using var provider = new VirtualPhysicalFileProvider(_root);

        Assert.False(provider.GetFileInfo("/sub").Exists);
    }

    /// <summary>
    /// 各种形态的 ../ 都读不到挂载根之外的文件
    /// </summary>
    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("/../outside.txt")]
    [InlineData("sub/../../outside.txt")]
    [InlineData("/sub/../../outside.txt")]
    public void GetFileInfo_ForTraversalPath_CannotEscapeRoot(string subpath)
    {
        Assert.True(File.Exists(_outsideFile));
        using var provider = new VirtualPhysicalFileProvider(_root);

        Assert.False(provider.GetFileInfo(subpath).Exists);
    }

    /// <summary>
    /// 直接传绝对路径也读不到挂载根之外的文件
    /// </summary>
    [Fact]
    public void GetFileInfo_ForAbsoluteOutsidePath_CannotEscapeRoot()
    {
        Assert.True(File.Exists(_outsideFile));
        using var provider = new VirtualPhysicalFileProvider(_root);

        Assert.False(provider.GetFileInfo(_outsideFile).Exists);
    }

    /// <summary>
    /// 目录内容只列出挂载根内的条目
    /// </summary>
    [Fact]
    public void GetDirectoryContents_ForRoot_ListsOnlyEntriesInsideRoot()
    {
        using var provider = new VirtualPhysicalFileProvider(_root);

        var names = provider.GetDirectoryContents("/")
            .Select(x => x.Name)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "a.txt", "sub" }, names);
    }

    /// <summary>
    /// 子目录内容可以正常枚举
    /// </summary>
    [Fact]
    public void GetDirectoryContents_ForSubDirectory_ListsChildren()
    {
        using var provider = new VirtualPhysicalFileProvider(_root);

        var contents = provider.GetDirectoryContents("/sub");

        Assert.True(contents.Exists);
        Assert.Equal("b.json", Assert.Single(contents).Name);
    }

    /// <summary>
    /// 不存在的目录返回不存在的目录内容
    /// </summary>
    [Fact]
    public void GetDirectoryContents_ForMissingDirectory_DoesNotExist()
    {
        using var provider = new VirtualPhysicalFileProvider(_root);

        Assert.False(provider.GetDirectoryContents("/missing").Exists);
    }

    private static string ReadAllText(IFileInfo file)
    {
        using var stream = file.CreateReadStream();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
