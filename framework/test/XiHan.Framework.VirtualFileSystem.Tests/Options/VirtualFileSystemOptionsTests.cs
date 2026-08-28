// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.FileProviders;
using XiHan.Framework.VirtualFileSystem.Options;
using XiHan.Framework.VirtualFileSystem.Providers.Embedded;
using XiHan.Framework.VirtualFileSystem.Providers.Physical;

namespace XiHan.Framework.VirtualFileSystem.Tests;

/// <summary>
/// 虚拟文件系统配置选项测试
/// </summary>
/// <remarks>
/// 选项类没有 Validate()，它的契约体现在默认值和「同一来源重复添加要覆盖而不是叠加」这条去重规则上：
/// 一旦去重失效，同一目录会被挂载多次，优先级排序结果随插入顺序漂移。
/// </remarks>
public class VirtualFileSystemOptionsTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _temp.Dispose();
    }

    /// <summary>
    /// 配置节名称是持久化契约，不允许漂移
    /// </summary>
    [Fact]
    public void SectionName_IsStable()
    {
        Assert.Equal("XiHan:VirtualFileSystem", VirtualFileSystemOptions.SectionName);
    }

    /// <summary>
    /// 默认值语义
    /// </summary>
    [Fact]
    public void Defaults_AreDocumentedValues()
    {
        var options = new VirtualFileSystemOptions();

        Assert.Equal(500, options.ChangeDebounceMilliseconds);
        Assert.True(options.EnableChangeTracking);
        Assert.True(options.IncludeCurrentDirectory);
        Assert.True(options.IncludeAppBaseDirectory);
        Assert.Empty(options.Providers);
        Assert.Empty(options.AdditionalPhysicalPaths);
    }

    /// <summary>
    /// 添加空提供程序抛参数空异常
    /// </summary>
    [Fact]
    public void AddProvider_WhenProviderIsNull_Throws()
    {
        var options = new VirtualFileSystemOptions();

        Assert.Throws<ArgumentNullException>(() => _ = options.AddProvider(null!));
    }

    /// <summary>
    /// 添加提供程序返回自身，支持链式配置
    /// </summary>
    [Fact]
    public void AddProvider_ReturnsSameOptionsInstance()
    {
        var options = new VirtualFileSystemOptions();

        Assert.Same(options, options.AddProvider(new FakeFileProvider(), 10));
    }

    /// <summary>
    /// 同一个提供程序实例重复添加时后者覆盖前者
    /// </summary>
    [Fact]
    public void AddProvider_WithSameInstanceTwice_ReplacesPreviousEntry()
    {
        var provider = new FakeFileProvider();
        var options = new VirtualFileSystemOptions();

        options.AddProvider(provider, 10);
        options.AddProvider(provider, 20);

        var entry = Assert.Single(options.Providers);
        Assert.Same(provider, entry.Provider);
        Assert.Equal(20, entry.Priority);
    }

    /// <summary>
    /// 不同来源的提供程序各自保留
    /// </summary>
    [Fact]
    public void AddProvider_WithDifferentProviders_KeepsBoth()
    {
        var options = new VirtualFileSystemOptions();

        options.AddProvider(new FakeFileProvider(), 10);
        options.AddProvider(new NullFileProvider(), 20);

        Assert.Equal(2, options.Providers.Count);
    }

    /// <summary>
    /// 物理目录为空时抛参数异常
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddPhysical_WhenRootPathIsBlank_Throws(string? rootPath)
    {
        var options = new VirtualFileSystemOptions();

        var exception = Assert.Throws<ArgumentException>(() => _ = options.AddPhysical(rootPath!));
        Assert.Equal("rootPath", exception.ParamName);
    }

    /// <summary>
    /// 目录不存在时自动创建，构造物理提供程序才不会失败
    /// </summary>
    /// <remarks>
    /// 这是 AddPhysical 相对裸 PhysicalFileProvider 的关键增量：目录缺失不再是致命错误。
    /// </remarks>
    [Fact]
    public void AddPhysical_WhenDirectoryMissing_CreatesItFirst()
    {
        var target = Path.Combine(_temp.Root, "auto-created");
        Assert.False(Directory.Exists(target));

        var options = new VirtualFileSystemOptions();
        options.AddPhysical(target, priority: 30);

        Assert.True(Directory.Exists(target));
    }

    /// <summary>
    /// 优先级同时写入提供程序自身与包装条目
    /// </summary>
    [Fact]
    public void AddPhysical_AppliesPriorityToProviderAndEntry()
    {
        var options = new VirtualFileSystemOptions();

        options.AddPhysical(_temp.Root, priority: 77);

        var entry = Assert.Single(options.Providers);
        var provider = Assert.IsType<VirtualPhysicalFileProvider>(entry.Provider);
        Assert.Equal(77, entry.Priority);
        Assert.Equal(77, provider.Priority);
        provider.Dispose();
    }

    /// <summary>
    /// 同一物理目录重复添加时后者覆盖前者
    /// </summary>
    [Fact]
    public void AddPhysical_WithSameRootTwice_ReplacesPreviousEntry()
    {
        var options = new VirtualFileSystemOptions();

        options.AddPhysical(_temp.Root, priority: 10);
        options.AddPhysical(_temp.Root, priority: 20);

        var entry = Assert.Single(options.Providers);
        Assert.Equal(20, entry.Priority);
    }

    /// <summary>
    /// 批量添加会跳过空白项
    /// </summary>
    [Fact]
    public void AddPhysicalRange_SkipsBlankEntries()
    {
        var first = _temp.CreateSubDirectory("first");
        var second = _temp.CreateSubDirectory("second");
        var options = new VirtualFileSystemOptions();

        options.AddPhysicalRange([first, "   ", second], priority: 40);

        Assert.Equal(2, options.Providers.Count);
        Assert.All(options.Providers, entry => Assert.Equal(40, entry.Priority));
    }

    /// <summary>
    /// 批量添加传入 null 抛参数空异常
    /// </summary>
    [Fact]
    public void AddPhysicalRange_WhenSourceIsNull_Throws()
    {
        var options = new VirtualFileSystemOptions();

        Assert.Throws<ArgumentNullException>(() => _ = options.AddPhysicalRange(null!));
    }

    /// <summary>
    /// 按程序集添加嵌入式提供程序，默认优先级为 50
    /// </summary>
    [Fact]
    public void AddEmbedded_ByAssembly_UsesDefaultPriority()
    {
        var assembly = typeof(VirtualFileSystemOptionsTests).Assembly;
        var options = new VirtualFileSystemOptions();

        options.AddEmbedded(assembly);

        var entry = Assert.Single(options.Providers);
        var provider = Assert.IsType<VirtualEmbeddedFileProvider>(entry.Provider);
        Assert.Same(assembly, provider.Assembly);
        Assert.Equal(50, provider.Priority);
        Assert.Equal(50, entry.Priority);
    }

    /// <summary>
    /// 按泛型标记类型添加嵌入式提供程序，取该类型所在程序集
    /// </summary>
    [Fact]
    public void AddEmbedded_ByMarkerType_UsesTypeAssembly()
    {
        var options = new VirtualFileSystemOptions();

        options.AddEmbedded<VirtualFileSystemOptionsTests>(60);

        var entry = Assert.Single(options.Providers);
        var provider = Assert.IsType<VirtualEmbeddedFileProvider>(entry.Provider);
        Assert.Same(typeof(VirtualFileSystemOptionsTests).Assembly, provider.Assembly);
        Assert.Equal(60, provider.Priority);
    }

    /// <summary>
    /// 同一程序集重复添加时后者覆盖前者
    /// </summary>
    [Fact]
    public void AddEmbedded_WithSameAssemblyTwice_ReplacesPreviousEntry()
    {
        var assembly = typeof(VirtualFileSystemOptionsTests).Assembly;
        var options = new VirtualFileSystemOptions();

        options.AddEmbedded(assembly, 10);
        options.AddEmbedded(assembly, 20);

        var entry = Assert.Single(options.Providers);
        Assert.Equal(20, entry.Priority);
    }
}
