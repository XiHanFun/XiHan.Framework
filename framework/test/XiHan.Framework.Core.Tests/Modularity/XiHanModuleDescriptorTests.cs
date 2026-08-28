// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Core.Tests.Modularity;

/// <summary>
/// 曦寒模块描述器测试
/// </summary>
/// <remarks>
/// 描述器是模块在装配期的唯一身份对象，构造期就必须把「类型不是模块」「实例与类型不匹配」两类
/// 装配错误挡住；依赖集合按引用去重，避免菱形依赖把同一个模块塞进依赖表两次。
/// </remarks>
public class XiHanModuleDescriptorTests
{
    /// <summary>
    /// 构造后携带类型、实例、程序集与插件标记
    /// </summary>
    [Fact]
    public void Constructor_KeepsTypeInstanceAndPlugInFlag()
    {
        var instance = new MdSampleModule();

        var descriptor = new XiHanModuleDescriptor(typeof(MdSampleModule), instance, true);

        Assert.Equal(typeof(MdSampleModule), descriptor.Type);
        Assert.Same(instance, descriptor.Instance);
        Assert.True(descriptor.IsLoadedAsPlugIn);
        Assert.Equal(typeof(MdSampleModule).Assembly, descriptor.Assembly);
        Assert.Contains(typeof(MdSampleModule).Assembly, descriptor.AllAssemblies);
        Assert.Empty(descriptor.Dependencies);
    }

    /// <summary>
    /// 模块类型为空时抛出
    /// </summary>
    [Fact]
    public void Constructor_WhenTypeNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = new XiHanModuleDescriptor(null!, new MdSampleModule(), false);
        });
    }

    /// <summary>
    /// 模块实例为空时抛出
    /// </summary>
    [Fact]
    public void Constructor_WhenInstanceNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = new XiHanModuleDescriptor(typeof(MdSampleModule), null!, false);
        });
    }

    /// <summary>
    /// 类型不是曦寒模块时抛出
    /// </summary>
    [Fact]
    public void Constructor_WhenTypeIsNotModule_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
        {
            _ = new XiHanModuleDescriptor(typeof(string), new MdSampleModule(), false);
        });

        Assert.Contains("不是曦寒模块", exception.Message);
    }

    /// <summary>
    /// 实例与声明类型不匹配时抛出
    /// </summary>
    [Fact]
    public void Constructor_WhenInstanceTypeMismatch_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
        {
            _ = new XiHanModuleDescriptor(typeof(MdOtherModule), new MdSampleModule(), false);
        });

        Assert.Contains("不是模块类型", exception.Message);
    }

    /// <summary>
    /// 声明了附加程序集时全部程序集包含附加项
    /// </summary>
    [Fact]
    public void AllAssemblies_WhenAdditionalAssemblyDeclared_ContainsIt()
    {
        var descriptor = new XiHanModuleDescriptor(typeof(MdAdditionalAssemblyModule), new MdAdditionalAssemblyModule(), false);

        Assert.Contains(typeof(XiHanModule).Assembly, descriptor.AllAssemblies);
        Assert.Equal(2, descriptor.AllAssemblies.Length);
    }

    /// <summary>
    /// 添加依赖后可读回且重复添加只保留一份
    /// </summary>
    [Fact]
    public void AddDependency_IsIdempotentForSameDescriptor()
    {
        var descriptor = new XiHanModuleDescriptor(typeof(MdSampleModule), new MdSampleModule(), false);
        var dependency = new XiHanModuleDescriptor(typeof(MdOtherModule), new MdOtherModule(), false);

        descriptor.AddDependency(dependency);
        descriptor.AddDependency(dependency);

        Assert.Same(dependency, Assert.Single(descriptor.Dependencies));
    }

    /// <summary>
    /// 不同依赖按添加顺序保留
    /// </summary>
    [Fact]
    public void AddDependency_KeepsInsertionOrder()
    {
        var descriptor = new XiHanModuleDescriptor(typeof(MdSampleModule), new MdSampleModule(), false);
        var first = new XiHanModuleDescriptor(typeof(MdOtherModule), new MdOtherModule(), false);
        var second = new XiHanModuleDescriptor(typeof(MdAdditionalAssemblyModule), new MdAdditionalAssemblyModule(), false);

        descriptor.AddDependency(first);
        descriptor.AddDependency(second);

        Assert.Equal(2, descriptor.Dependencies.Count);
        Assert.Same(first, descriptor.Dependencies[0]);
        Assert.Same(second, descriptor.Dependencies[1]);
    }

    /// <summary>
    /// 依赖集合对外是快照，外部无法经返回值改写内部状态
    /// </summary>
    [Fact]
    public void Dependencies_ReturnsSnapshot()
    {
        var descriptor = new XiHanModuleDescriptor(typeof(MdSampleModule), new MdSampleModule(), false);
        var before = descriptor.Dependencies;

        descriptor.AddDependency(new XiHanModuleDescriptor(typeof(MdOtherModule), new MdOtherModule(), false));

        Assert.Empty(before);
        Assert.Single(descriptor.Dependencies);
    }

    /// <summary>
    /// 字符串表示包含模块完整类型名
    /// </summary>
    [Fact]
    public void ToString_ContainsModuleFullName()
    {
        var descriptor = new XiHanModuleDescriptor(typeof(MdSampleModule), new MdSampleModule(), false);

        Assert.Equal($"[XiHanModuleDescriptor {typeof(MdSampleModule).FullName}]", descriptor.ToString());
    }
}

/// <summary>
/// 描述器测试用样例模块
/// </summary>
internal class MdSampleModule : XiHanModule;

/// <summary>
/// 描述器测试用另一模块
/// </summary>
internal class MdOtherModule : XiHanModule;

/// <summary>
/// 描述器测试用声明附加程序集的模块
/// </summary>
[AdditionalAssembly(typeof(XiHanModule))]
internal class MdAdditionalAssemblyModule : XiHanModule;
