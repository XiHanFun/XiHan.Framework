// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Core.Modularity.PlugIns;

namespace XiHan.Framework.Core.Tests.Modularity.PlugIns;

/// <summary>
/// 类型插件源测试
/// </summary>
/// <remarks>
/// 类型插件源是最直白的一种来源：给什么类型就原样吐回什么类型，不做模块合法性校验，
/// 校验推迟到模块加载器创建描述器时统一进行。传空数组引用必须退化为空集合。
/// </remarks>
public class TypePlugInSourceTests
{
    /// <summary>
    /// 原样返回构造时给定的类型并保持顺序
    /// </summary>
    [Fact]
    public void GetModules_ReturnsGivenTypesInOrder()
    {
        var source = new TypePlugInSource(typeof(PlsSampleModule), typeof(PlsDependentModule));

        var modules = source.GetModules();

        Assert.Equal(2, modules.Length);
        Assert.Equal(typeof(PlsSampleModule), modules[0]);
        Assert.Equal(typeof(PlsDependentModule), modules[1]);
    }

    /// <summary>
    /// 无参构造时返回空集合
    /// </summary>
    [Fact]
    public void GetModules_WhenNoType_ReturnsEmpty()
    {
        Assert.Empty(new TypePlugInSource().GetModules());
    }

    /// <summary>
    /// 传入空数组引用时返回空集合
    /// </summary>
    [Fact]
    public void GetModules_WhenNullArray_ReturnsEmpty()
    {
        Assert.Empty(new TypePlugInSource((Type[]?)null).GetModules());
    }

    /// <summary>
    /// 不校验类型是否为模块，交由后续加载阶段处理
    /// </summary>
    [Fact]
    public void GetModules_DoesNotValidateModuleType()
    {
        var source = new TypePlugInSource(typeof(string));

        Assert.Equal(typeof(string), Assert.Single(source.GetModules()));
    }

    /// <summary>
    /// 实现插件源契约
    /// </summary>
    [Fact]
    public void TypePlugInSource_ImplementsPlugInSourceContract()
    {
        IPlugInSource source = new TypePlugInSource(typeof(PlsSampleModule));

        Assert.Equal(typeof(PlsSampleModule), Assert.Single(source.GetModules()));
    }
}

/// <summary>
/// 插件测试用样例模块
/// </summary>
internal class PlsSampleModule : XiHanModule;

/// <summary>
/// 插件测试用依赖样例模块的模块
/// </summary>
[DependsOn(typeof(PlsSampleModule))]
internal class PlsDependentModule : XiHanModule;
