// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.FileProviders;
using XiHan.Framework.VirtualFileSystem.Providers;
using XiHan.Framework.VirtualFileSystem.Providers.Composite;
using XiHan.Framework.VirtualFileSystem.Providers.Physical;

namespace XiHan.Framework.VirtualFileSystem.Tests;

/// <summary>
/// 带优先级的组合文件提供程序测试
/// </summary>
/// <remarks>
/// 这里是「优先级覆盖 + 回退顺序」的落点：同名文件必须由优先级最高的提供程序赢，
/// 高优先级没有的文件必须回退到低优先级，全都没有才返回 NotFound。
/// 目录视图另外要保证同名条目只出现一次，否则枚举会重复输出同一逻辑文件。
/// </remarks>
public class VirtualCompositeFileProviderTests : IDisposable
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
    /// 同名文件由优先级最高的提供程序胜出，且构造时会自行按优先级降序排序
    /// </summary>
    /// <remarks>
    /// 故意把低优先级放在数组前面，用来验证排序发生在构造函数里，而不是依赖调用方传入顺序。
    /// </remarks>
    [Fact]
    public void GetFileInfo_WhenMultipleProvidersHaveFile_HighestPriorityWins()
    {
        var low = new FakeFileProvider().WithFile("/app.json", FakeFileInfo.ForContent("app.json", "low"));
        var high = new FakeFileProvider().WithFile("/app.json", FakeFileInfo.ForContent("app.json", "high"));
        var sut = new VirtualCompositeFileProvider(
        [
            new PrioritizedFileProvider(low, 10),
            new PrioritizedFileProvider(high, 90)
        ]);

        var file = sut.GetFileInfo("/app.json");

        var prioritized = Assert.IsType<PrioritizedFileInfo>(file);
        Assert.Equal(90, prioritized.Priority);
        Assert.Equal("high", ReadAllText(file));
    }

    /// <summary>
    /// 高优先级没有该文件时回退到低优先级
    /// </summary>
    [Fact]
    public void GetFileInfo_WhenHighPriorityMisses_FallsBackToLowerPriority()
    {
        var low = new FakeFileProvider().WithFile("/only-low.json", FakeFileInfo.ForContent("only-low.json", "low"));
        var high = new FakeFileProvider().WithFile("/only-high.json", FakeFileInfo.ForContent("only-high.json", "high"));
        var sut = new VirtualCompositeFileProvider(
        [
            new PrioritizedFileProvider(high, 90),
            new PrioritizedFileProvider(low, 10)
        ]);

        var file = sut.GetFileInfo("/only-low.json");

        var prioritized = Assert.IsType<PrioritizedFileInfo>(file);
        Assert.Equal(10, prioritized.Priority);
        Assert.Equal("low", ReadAllText(file));
    }

    /// <summary>
    /// 所有提供程序都没有该文件时返回未找到文件信息
    /// </summary>
    [Fact]
    public void GetFileInfo_WhenNoProviderHasFile_ReturnsNotFound()
    {
        var sut = new VirtualCompositeFileProvider(
        [
            new PrioritizedFileProvider(new FakeFileProvider(), 10)
        ]);

        var file = sut.GetFileInfo("/missing.json");

        var notFound = Assert.IsType<NotFoundFileInfo>(file);
        Assert.False(notFound.Exists);
        Assert.Equal("/missing.json", notFound.Name);
    }

    /// <summary>
    /// 没有任何提供程序时同样返回未找到文件信息而不是抛异常
    /// </summary>
    [Fact]
    public void GetFileInfo_WhenNoProviders_ReturnsNotFound()
    {
        var sut = new VirtualCompositeFileProvider([]);

        Assert.False(sut.GetFileInfo("/a.txt").Exists);
    }

    /// <summary>
    /// 目录视图是带优先级的包装
    /// </summary>
    [Fact]
    public void GetDirectoryContents_ReturnsPrioritizedView()
    {
        var provider = new FakeFileProvider().WithDirectory(true, FakeFileInfo.ForContent("a.txt", "a"));
        var sut = new VirtualCompositeFileProvider(
        [
            new PrioritizedFileProvider(provider, 10)
        ]);

        var contents = sut.GetDirectoryContents("/");

        Assert.IsType<PrioritizedDirectoryContents>(contents);
        Assert.True(contents.Exists);
    }

    /// <summary>
    /// 多个物理提供程序的目录内容会合并，同名条目只出现一次
    /// </summary>
    [Fact]
    public void GetDirectoryContents_MergesProvidersAndDeduplicatesByName()
    {
        var highRoot = _temp.CreateSubDirectory("high");
        var lowRoot = _temp.CreateSubDirectory("low");
        _temp.WriteFile("high/shared.txt", "high-shared");
        _temp.WriteFile("high/only-high.txt", "high-only");
        _temp.WriteFile("low/shared.txt", "low-shared");
        _temp.WriteFile("low/only-low.txt", "low-only");

        using var high = new VirtualPhysicalFileProvider(highRoot, 100);
        using var low = new VirtualPhysicalFileProvider(lowRoot, 10);
        var sut = new VirtualCompositeFileProvider(
        [
            new PrioritizedFileProvider(high, 100),
            new PrioritizedFileProvider(low, 10)
        ]);

        var names = sut.GetDirectoryContents("/").Select(x => x.Name).ToArray();

        Assert.Equal(3, names.Length);
        Assert.Single(names, x => string.Equals(x, "shared.txt", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("only-high.txt", names);
        Assert.Contains("only-low.txt", names);
    }

    /// <summary>
    /// 同名文件的内容取自优先级更高的物理目录
    /// </summary>
    [Fact]
    public void GetFileInfo_AcrossPhysicalProviders_ReadsFromHighestPriorityRoot()
    {
        var highRoot = _temp.CreateSubDirectory("high");
        var lowRoot = _temp.CreateSubDirectory("low");
        _temp.WriteFile("high/shared.txt", "high-shared");
        _temp.WriteFile("low/shared.txt", "low-shared");

        using var high = new VirtualPhysicalFileProvider(highRoot, 100);
        using var low = new VirtualPhysicalFileProvider(lowRoot, 10);
        var sut = new VirtualCompositeFileProvider(
        [
            new PrioritizedFileProvider(low, 10),
            new PrioritizedFileProvider(high, 100)
        ]);

        Assert.Equal("high-shared", ReadAllText(sut.GetFileInfo("/shared.txt")));
    }

    /// <summary>
    /// 监视令牌聚合所有提供程序，任一发生变化即视为变化
    /// </summary>
    [Fact]
    public void Watch_AggregatesTokensFromAllProviders()
    {
        var quiet = new FakeFileProvider();
        var noisy = new FakeFileProvider();
        noisy.ChangeToken.HasChanged = true;
        var sut = new VirtualCompositeFileProvider(
        [
            new PrioritizedFileProvider(quiet, 10),
            new PrioritizedFileProvider(noisy, 20)
        ]);

        var token = sut.Watch("**/*.json");

        Assert.True(token.HasChanged);
        Assert.Equal("**/*.json", quiet.LastWatchFilter);
        Assert.Equal("**/*.json", noisy.LastWatchFilter);
    }

    /// <summary>
    /// 所有提供程序都没有变化时聚合令牌也没有变化
    /// </summary>
    [Fact]
    public void Watch_WhenNoProviderChanged_TokenIsUnchanged()
    {
        var sut = new VirtualCompositeFileProvider(
        [
            new PrioritizedFileProvider(new FakeFileProvider(), 10),
            new PrioritizedFileProvider(new FakeFileProvider(), 20)
        ]);

        Assert.False(sut.Watch("*").HasChanged);
    }

    private static string ReadAllText(IFileInfo file)
    {
        using var stream = file.CreateReadStream();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
