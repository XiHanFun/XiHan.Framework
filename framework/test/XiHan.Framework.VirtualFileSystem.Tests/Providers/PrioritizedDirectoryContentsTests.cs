// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.FileProviders;
using XiHan.Framework.VirtualFileSystem.Providers;

namespace XiHan.Framework.VirtualFileSystem.Tests;

/// <summary>
/// 优先级目录内容包装器测试
/// </summary>
/// <remarks>
/// 枚举时会用裸文件名反查来源提供程序，这个反查天然可能落空（子目录、合并视图下按相对路径索引的提供程序）。
/// 落空时必须退化成默认优先级继续枚举，而不是抛异常——历史上用 First 抛过
/// "Sequence contains no matching element"，导致整个目录枚举崩溃。
/// </remarks>
public class PrioritizedDirectoryContentsTests
{
    /// <summary>
    /// 是否存在直接透传内层目录内容
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Exists_DelegatesToInner(bool exists)
    {
        var sut = new PrioritizedDirectoryContents(new FakeDirectoryContents(exists), []);

        Assert.Equal(exists, sut.Exists);
    }

    /// <summary>
    /// 命中来源提供程序时使用该提供程序的优先级
    /// </summary>
    [Fact]
    public void Enumerate_WhenProviderOwnsFile_UsesThatProviderPriority()
    {
        var owner = new FakeFileProvider().WithFile("a.txt", FakeFileInfo.ForContent("a.txt", "A"));
        var providers = new List<PrioritizedFileProvider>
        {
            new(owner, 100),
            new(new FakeFileProvider(), 10)
        };
        IDirectoryContents sut = new PrioritizedDirectoryContents(
            new FakeDirectoryContents(true, FakeFileInfo.ForContent("a.txt", "A")),
            providers);

        var prioritized = Assert.IsType<PrioritizedFileInfo>(Assert.Single(sut));

        Assert.Equal(100, prioritized.Priority);
        Assert.Equal("a.txt", prioritized.Name);
    }

    /// <summary>
    /// 多个提供程序都拥有同名文件时取列表中的第一个
    /// </summary>
    /// <remarks>
    /// 组合提供器传进来的列表已按优先级降序排好，所以「第一个」等价于「优先级最高的那个」。
    /// </remarks>
    [Fact]
    public void Enumerate_WhenMultipleProvidersOwnFile_TakesFirstInList()
    {
        var high = new FakeFileProvider().WithFile("a.txt", FakeFileInfo.ForContent("a.txt", "high"));
        var low = new FakeFileProvider().WithFile("a.txt", FakeFileInfo.ForContent("a.txt", "low"));
        var providers = new List<PrioritizedFileProvider>
        {
            new(high, 90),
            new(low, 5)
        };
        IDirectoryContents sut = new PrioritizedDirectoryContents(
            new FakeDirectoryContents(true, FakeFileInfo.ForContent("a.txt", "high")),
            providers);

        var prioritized = Assert.IsType<PrioritizedFileInfo>(Assert.Single(sut));

        Assert.Equal(90, prioritized.Priority);
    }

    /// <summary>
    /// 反查不到来源提供程序时退化为默认优先级，且不抛异常
    /// </summary>
    [Fact]
    public void Enumerate_WhenNoProviderOwnsFile_FallsBackToZeroPriority()
    {
        var providers = new List<PrioritizedFileProvider>
        {
            new(new FakeFileProvider(), 100)
        };
        IDirectoryContents sut = new PrioritizedDirectoryContents(
            new FakeDirectoryContents(true, FakeFileInfo.ForContent("orphan.txt", "x")),
            providers);

        var prioritized = Assert.IsType<PrioritizedFileInfo>(Assert.Single(sut));

        Assert.Equal(0, prioritized.Priority);
        Assert.Equal("orphan.txt", prioritized.Name);
    }

    /// <summary>
    /// 提供程序列表为空时同样退化为默认优先级
    /// </summary>
    [Fact]
    public void Enumerate_WhenProviderListEmpty_FallsBackToZeroPriority()
    {
        IDirectoryContents sut = new PrioritizedDirectoryContents(
            new FakeDirectoryContents(true, FakeFileInfo.ForContent("a.txt", "x")),
            []);

        var prioritized = Assert.IsType<PrioritizedFileInfo>(Assert.Single(sut));

        Assert.Equal(0, prioritized.Priority);
    }

    /// <summary>
    /// 保持内层目录内容的原始顺序
    /// </summary>
    [Fact]
    public void Enumerate_PreservesInnerOrder()
    {
        IDirectoryContents sut = new PrioritizedDirectoryContents(
            new FakeDirectoryContents(
                true,
                FakeFileInfo.ForContent("z.txt", "z"),
                FakeFileInfo.ForDirectory("sub"),
                FakeFileInfo.ForContent("a.txt", "a")),
            []);

        var names = sut.Select(x => x.Name).ToArray();

        Assert.Equal(new[] { "z.txt", "sub", "a.txt" }, names);
    }

    /// <summary>
    /// 内层为空时枚举结果为空
    /// </summary>
    [Fact]
    public void Enumerate_WhenInnerIsEmpty_YieldsNothing()
    {
        IDirectoryContents sut = new PrioritizedDirectoryContents(
            new FakeDirectoryContents(false),
            []);

        Assert.Empty(sut);
    }
}
