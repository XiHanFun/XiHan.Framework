// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.FileProviders;
using System.Text;
using XiHan.Framework.VirtualFileSystem.Providers;
using XiHan.Framework.VirtualFileSystem.Tests.TestSupport;

namespace XiHan.Framework.VirtualFileSystem.Tests.Providers;

/// <summary>
/// 优先级文件信息包装器测试
/// </summary>
/// <remarks>
/// 包装器只负责挂上优先级，其余全部原样透传。这里给内层替身设置一组互不推导的值
/// （长度 42 但内容 5 字节、PhysicalPath 与 Name 不同源），确保包装器没有自作主张重算任何一项。
/// </remarks>
public class PrioritizedFileInfoTests
{
    private static readonly DateTimeOffset KnownTime = new(2024, 5, 1, 12, 30, 0, TimeSpan.Zero);

    /// <summary>
    /// 所有元数据原样透传给内层文件信息
    /// </summary>
    [Fact]
    public void Members_DelegateToInnerFileInfo()
    {
        var inner = new FakeFileInfo("app.json")
        {
            Exists = true,
            Length = 42,
            PhysicalPath = "/physical/root/app.json",
            LastModified = KnownTime,
            IsDirectory = false,
            Content = Encoding.UTF8.GetBytes("hello")
        };

        var sut = new PrioritizedFileInfo(inner, 90);

        Assert.True(sut.Exists);
        Assert.Equal(42L, sut.Length);
        Assert.Equal("/physical/root/app.json", sut.PhysicalPath);
        Assert.Equal("app.json", sut.Name);
        Assert.Equal(KnownTime, sut.LastModified);
        Assert.False(sut.IsDirectory);
        Assert.Equal(90, sut.Priority);
    }

    /// <summary>
    /// 目录条目的 IsDirectory 同样透传
    /// </summary>
    [Fact]
    public void IsDirectory_DelegatesToInnerFileInfo()
    {
        var sut = new PrioritizedFileInfo(FakeFileInfo.ForDirectory("sub"), 0);

        Assert.True(sut.IsDirectory);
        Assert.True(sut.Exists);
    }

    /// <summary>
    /// 读取流直接取内层的流，不额外缓冲
    /// </summary>
    [Fact]
    public void CreateReadStream_DelegatesToInnerFileInfo()
    {
        var inner = FakeFileInfo.ForContent("a.txt", "content-from-inner");
        var sut = new PrioritizedFileInfo(inner, 10);

        using var stream = sut.CreateReadStream();
        using var reader = new StreamReader(stream);

        Assert.Equal("content-from-inner", reader.ReadToEnd());
        Assert.Equal(1, inner.CreateReadStreamCallCount);
    }

    /// <summary>
    /// 不存在的内层文件不会被包装成「存在」
    /// </summary>
    [Fact]
    public void Exists_ForMissingInnerFile_IsFalse()
    {
        var sut = new PrioritizedFileInfo(new NotFoundFileInfo("missing.txt"), 100);

        Assert.False(sut.Exists);
        Assert.Equal("missing.txt", sut.Name);
        Assert.Equal(100, sut.Priority);
    }

    /// <summary>
    /// 优先级可以为负，用于表达「兜底」提供程序
    /// </summary>
    [Fact]
    public void Priority_AllowsNegativeValue()
    {
        var sut = new PrioritizedFileInfo(FakeFileInfo.ForContent("a.txt", "x"), -1);

        Assert.Equal(-1, sut.Priority);
    }

    /// <summary>
    /// 实现 IFileInfo，可以直接放进组合视图
    /// </summary>
    [Fact]
    public void Type_ImplementsFileInfoContract()
    {
        var sut = new PrioritizedFileInfo(FakeFileInfo.ForContent("a.txt", "x"), 0);

        Assert.IsAssignableFrom<IFileInfo>(sut);
    }
}
