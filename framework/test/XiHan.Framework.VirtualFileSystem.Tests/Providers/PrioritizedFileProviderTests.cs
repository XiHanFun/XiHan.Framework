// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.VirtualFileSystem.Providers;
using XiHan.Framework.VirtualFileSystem.Tests.TestSupport;

namespace XiHan.Framework.VirtualFileSystem.Tests.Providers;

/// <summary>
/// 带优先级的提供程序包装测试
/// </summary>
/// <remarks>
/// 这是个只读容器，但它的相等语义是有约束的：VirtualFileSystem 用 List.Remove 卸载条目，
/// 依赖默认的引用相等；若改成 record，同优先级同提供程序的两个条目会互相误删。
/// </remarks>
public class PrioritizedFileProviderTests
{
    /// <summary>
    /// 构造后原样保留提供程序与优先级
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(-50)]
    public void Constructor_KeepsProviderAndPriority(int priority)
    {
        var provider = new FakeFileProvider();

        var sut = new PrioritizedFileProvider(provider, priority);

        Assert.Same(provider, sut.Provider);
        Assert.Equal(priority, sut.Priority);
    }

    /// <summary>
    /// 相等性按引用判定，不按字段值判定
    /// </summary>
    [Fact]
    public void Equality_IsReferenceBased()
    {
        var provider = new FakeFileProvider();
        var first = new PrioritizedFileProvider(provider, 10);
        var second = new PrioritizedFileProvider(provider, 10);

        Assert.NotEqual(first, second);
        Assert.Equal(first, first);
    }
}
