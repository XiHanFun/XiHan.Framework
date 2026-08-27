// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.FileProviders;
using System.Reflection;
using XiHan.Framework.VirtualFileSystem.Providers;
using XiHan.Framework.VirtualFileSystem.Providers.Embedded;

namespace XiHan.Framework.VirtualFileSystem.Tests;

/// <summary>
/// 带优先级的嵌入式文件提供程序测试
/// </summary>
/// <remarks>
/// 该类型用 new 隐藏了基类的 GetFileInfo，而不是重写。这一点非常关键：
/// 组合提供器持有的是 IFileProvider 静态类型，走的是基类实现，拿不到 PrioritizedFileInfo。
/// 用例把两条调用路径都锁住，避免以后有人误以为优先级会自动透到组合视图里。
/// </remarks>
public class VirtualEmbeddedFileProviderTests
{
    private const string SampleSubpath = "/Resources/embedded-sample.txt";

    private static readonly Assembly TestAssembly = typeof(VirtualEmbeddedFileProviderTests).Assembly;

    /// <summary>
    /// 默认优先级为 50，并原样保留程序集引用
    /// </summary>
    [Fact]
    public void Constructor_UsesDefaultPriorityAndKeepsAssembly()
    {
        var provider = new VirtualEmbeddedFileProvider(TestAssembly);

        Assert.Equal(50, provider.Priority);
        Assert.Same(TestAssembly, provider.Assembly);
    }

    /// <summary>
    /// 可以显式指定优先级
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(70)]
    [InlineData(-10)]
    public void Constructor_AcceptsExplicitPriority(int priority)
    {
        var provider = new VirtualEmbeddedFileProvider(TestAssembly, priority);

        Assert.Equal(priority, provider.Priority);
    }

    /// <summary>
    /// 直接调用时返回带优先级的文件信息
    /// </summary>
    [Fact]
    public void GetFileInfo_OnConcreteType_ReturnsPrioritizedFileInfo()
    {
        var provider = new VirtualEmbeddedFileProvider(TestAssembly, 70);

        var file = provider.GetFileInfo("/not-an-embedded-resource.txt");

        Assert.Equal(70, file.Priority);
        Assert.False(file.Exists);
    }

    /// <summary>
    /// 通过 IFileProvider 接口调用时走基类实现，不带优先级
    /// </summary>
    [Fact]
    public void GetFileInfo_ViaInterface_UsesBaseImplementation()
    {
        IFileProvider provider = new VirtualEmbeddedFileProvider(TestAssembly, 70);

        var file = provider.GetFileInfo("/not-an-embedded-resource.txt");

        Assert.IsNotType<PrioritizedFileInfo>(file);
        Assert.False(file.Exists);
    }

    /// <summary>
    /// 真实嵌入资源可以按虚拟路径读出内容
    /// </summary>
    [Fact]
    public void GetFileInfo_ForRealEmbeddedResource_ReadsContent()
    {
        // 嵌入资源的清单名由「根命名空间 + 目录路径」拼成；名字对不上时跳过而不是判负，
        // 避免用例被构建配置的默认值细节绑架。
        var expectedResourceName = TestAssembly.GetName().Name + ".Resources.embedded-sample.txt";
        Assert.SkipUnless(
            TestAssembly.GetManifestResourceNames().Contains(expectedResourceName, StringComparer.Ordinal),
            $"测试程序集未包含嵌入资源 {expectedResourceName}，跳过该组验证。");

        var provider = new VirtualEmbeddedFileProvider(TestAssembly, 65);
        var file = provider.GetFileInfo(SampleSubpath);

        Assert.True(file.Exists);
        Assert.False(file.IsDirectory);
        Assert.Equal(65, file.Priority);

        using var reader = new StreamReader(file.CreateReadStream());
        Assert.Contains("xihan-embedded-sample-content", reader.ReadToEnd());
    }

    /// <summary>
    /// 继承自 EmbeddedFileProvider，可以直接当成标准文件提供程序使用
    /// </summary>
    [Fact]
    public void Type_DerivesFromEmbeddedFileProvider()
    {
        var provider = new VirtualEmbeddedFileProvider(TestAssembly);

        Assert.IsAssignableFrom<EmbeddedFileProvider>(provider);
        Assert.IsAssignableFrom<IFileProvider>(provider);
    }
}
