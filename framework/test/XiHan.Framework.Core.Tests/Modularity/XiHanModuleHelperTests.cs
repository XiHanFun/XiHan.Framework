// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Core.Tests.Modularity;

/// <summary>
/// 曦寒模块帮助类测试
/// </summary>
/// <remarks>
/// 模块发现阶段的三条契约：只有可实例化的非泛型模块类才算模块；
/// 依赖声明按类型去重；递归发现遇到已加载的模块直接短路（因此菱形依赖与环状依赖都不会无限递归）。
/// 注意此处环状依赖只保证「发现阶段不炸」，真正的报错发生在后续拓扑排序，见 ModuleLoaderTests。
/// </remarks>
public class XiHanModuleHelperTests
{
    /// <summary>
    /// 合法模块类型被识别为曦寒模块
    /// </summary>
    [Fact]
    public void IsXiHanModule_WhenConcreteModuleClass_ReturnsTrue()
    {
        Assert.True(XiHanModuleHelper.IsXiHanModule(typeof(MhLeafModule)));
    }

    /// <summary>
    /// 抽象类、接口、泛型定义与非模块类型都不算曦寒模块
    /// </summary>
    /// <param name="type">待判定类型</param>
    [Theory]
    [InlineData(typeof(XiHanModule))]
    [InlineData(typeof(IXiHanModule))]
    [InlineData(typeof(MhGenericModule<>))]
    [InlineData(typeof(string))]
    public void IsXiHanModule_WhenNotEligible_ReturnsFalse(Type type)
    {
        Assert.False(XiHanModuleHelper.IsXiHanModule(type));
    }

    /// <summary>
    /// 无依赖声明的模块返回空依赖
    /// </summary>
    [Fact]
    public void FindDependedModuleTypes_WhenNoAttribute_ReturnsEmpty()
    {
        Assert.Empty(XiHanModuleHelper.FindDependedModuleTypes(typeof(MhLeafModule)));
    }

    /// <summary>
    /// 依赖声明按类型去重且保留全部不同依赖
    /// </summary>
    [Fact]
    public void FindDependedModuleTypes_WhenDeclaredTwice_Deduplicates()
    {
        var dependencies = XiHanModuleHelper.FindDependedModuleTypes(typeof(MhDuplicateDependsModule));

        Assert.Equal(2, dependencies.Count);
        Assert.Contains(typeof(MhLeafModule), dependencies);
        Assert.Contains(typeof(MhMiddleModule), dependencies);
    }

    /// <summary>
    /// 非模块类型查询依赖时抛出
    /// </summary>
    [Fact]
    public void FindDependedModuleTypes_WhenNotModule_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() => XiHanModuleHelper.FindDependedModuleTypes(typeof(string)));

        Assert.Contains("不是曦寒模块", exception.Message);
    }

    /// <summary>
    /// 递归发现覆盖全部传递依赖且起始模块排在最前
    /// </summary>
    [Fact]
    public void FindAllModuleTypes_ReturnsStartupFirstAndAllTransitiveDependencies()
    {
        var modules = XiHanModuleHelper.FindAllModuleTypes(typeof(MhRootModule), null);

        Assert.Equal(typeof(MhRootModule), modules[0]);
        Assert.Contains(typeof(MhMiddleModule), modules);
        Assert.Contains(typeof(MhLeafModule), modules);
        Assert.Equal(3, modules.Count);
    }

    /// <summary>
    /// 菱形依赖下每个模块只出现一次
    /// </summary>
    [Fact]
    public void FindAllModuleTypes_WhenDiamondDependency_ContainsEachModuleOnce()
    {
        var modules = XiHanModuleHelper.FindAllModuleTypes(typeof(MhRootModule), null);

        Assert.Equal(modules.Count, modules.Distinct().Count());
    }

    /// <summary>
    /// 环状依赖在发现阶段被短路而不是无限递归
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void FindAllModuleTypes_WhenCyclicDependency_TerminatesWithEachModuleOnce()
    {
        var modules = XiHanModuleHelper.FindAllModuleTypes(typeof(MhCycleAModule), null);

        Assert.Equal(2, modules.Count);
        Assert.Contains(typeof(MhCycleAModule), modules);
        Assert.Contains(typeof(MhCycleBModule), modules);
    }

    /// <summary>
    /// 起始类型不是模块时抛出
    /// </summary>
    [Fact]
    public void FindAllModuleTypes_WhenStartupTypeIsNotModule_Throws()
    {
        Assert.Throws<ArgumentException>(() => XiHanModuleHelper.FindAllModuleTypes(typeof(string), null));
    }

    /// <summary>
    /// 无附加程序集声明时只返回模块自身所在程序集
    /// </summary>
    [Fact]
    public void GetAllAssemblies_WhenNoAdditionalAssembly_ReturnsOwnAssemblyOnly()
    {
        var assemblies = XiHanModuleHelper.GetAllAssemblies(typeof(MhLeafModule));

        Assert.Equal(typeof(MhLeafModule).Assembly, Assert.Single(assemblies));
    }

    /// <summary>
    /// 声明附加程序集时合并且去重
    /// </summary>
    [Fact]
    public void GetAllAssemblies_WhenAdditionalAssemblyDeclared_MergesAndDeduplicates()
    {
        var assemblies = XiHanModuleHelper.GetAllAssemblies(typeof(MhAdditionalAssemblyModule));

        Assert.Equal(2, assemblies.Length);
        Assert.Contains(typeof(XiHanModule).Assembly, assemblies);
        Assert.Contains(typeof(MhAdditionalAssemblyModule).Assembly, assemblies);
    }
}

/// <summary>
/// 无依赖的叶子模块
/// </summary>
internal class MhLeafModule : XiHanModule;

/// <summary>
/// 依赖叶子模块的中间模块
/// </summary>
[DependsOn(typeof(MhLeafModule))]
internal class MhMiddleModule : XiHanModule;

/// <summary>
/// 同时依赖中间与叶子模块的根模块
/// </summary>
[DependsOn(typeof(MhMiddleModule), typeof(MhLeafModule))]
internal class MhRootModule : XiHanModule;

/// <summary>
/// 重复声明同一依赖的模块
/// </summary>
[DependsOn(typeof(MhLeafModule))]
[DependsOn(typeof(MhLeafModule), typeof(MhMiddleModule))]
internal class MhDuplicateDependsModule : XiHanModule;

/// <summary>
/// 环状依赖模块甲
/// </summary>
[DependsOn(typeof(MhCycleBModule))]
internal class MhCycleAModule : XiHanModule;

/// <summary>
/// 环状依赖模块乙
/// </summary>
[DependsOn(typeof(MhCycleAModule))]
internal class MhCycleBModule : XiHanModule;

/// <summary>
/// 声明附加程序集的模块
/// </summary>
[AdditionalAssembly(typeof(XiHanModule))]
internal class MhAdditionalAssemblyModule : XiHanModule;

/// <summary>
/// 泛型模块定义，不应被识别为可加载模块
/// </summary>
/// <typeparam name="T">占位类型</typeparam>
internal class MhGenericModule<T> : XiHanModule;
