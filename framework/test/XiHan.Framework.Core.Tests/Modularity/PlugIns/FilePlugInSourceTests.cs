// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Core.Modularity.PlugIns;

namespace XiHan.Framework.Core.Tests.Modularity.PlugIns;

/// <summary>
/// 文件插件源测试
/// </summary>
/// <remarks>
/// 文件插件源逐个加载指定程序集并挑出其中的曦寒模块类型。
/// 扫描目标选用已被测试进程加载的框架核心程序集，既能真实走一遍「加载 + 类型筛选」，
/// 又不会把新程序集引入默认加载上下文而污染同一进程内的其他用例。
/// </remarks>
public class FilePlugInSourceTests
{
    /// <summary>
    /// 原样保留构造时给定的文件路径
    /// </summary>
    [Fact]
    public void FilePaths_KeepsGivenPathsInOrder()
    {
        var source = new FilePlugInSource("first.dll", "second.dll");

        Assert.Equal(2, source.FilePaths.Length);
        Assert.Equal("first.dll", source.FilePaths[0]);
        Assert.Equal("second.dll", source.FilePaths[1]);
    }

    /// <summary>
    /// 无参构造时路径为空且扫描结果为空
    /// </summary>
    [Fact]
    public void GetModules_WhenNoPath_ReturnsEmpty()
    {
        var source = new FilePlugInSource();

        Assert.Empty(source.FilePaths);
        Assert.Empty(source.GetModules());
    }

    /// <summary>
    /// 传入空数组引用时路径为空
    /// </summary>
    [Fact]
    public void FilePaths_WhenNullArray_IsEmpty()
    {
        Assert.Empty(new FilePlugInSource((string[]?)null).FilePaths);
    }

    /// <summary>
    /// 扫描真实程序集时只挑出合法的曦寒模块类型
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void GetModules_WhenScanningRealAssembly_SelectsOnlyEligibleModuleTypes()
    {
        var location = typeof(XiHanModule).Assembly.Location;
        Assert.SkipUnless(!string.IsNullOrEmpty(location), "无法定位框架核心程序集文件，跳过真实扫描验证。");

        var modules = new FilePlugInSource(location).GetModules();

        Assert.All(modules, type => Assert.True(XiHanModuleHelper.IsXiHanModule(type)));
        // 抽象基类与契约接口不是可加载模块，必须被筛掉
        Assert.DoesNotContain(typeof(XiHanModule), modules);
        Assert.DoesNotContain(typeof(IXiHanModule), modules);
    }

    /// <summary>
    /// 实现插件源契约
    /// </summary>
    [Fact]
    public void FilePlugInSource_ImplementsPlugInSourceContract()
    {
        IPlugInSource source = new FilePlugInSource();

        Assert.Empty(source.GetModules());
    }
}
