// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Core.Modularity.PlugIns;

namespace XiHan.Framework.Core.Tests.Modularity.PlugIns;

/// <summary>
/// 插件源扩展测试
/// </summary>
/// <remarks>
/// 插件不是孤立的：声明一个插件模块等于把它整条依赖链都拉进应用，
/// 因此扩展方法会对每个插件模块跑一遍完整的模块发现，并对结果去重。
/// </remarks>
public class PlugInSourceExtensionsTests
{
    /// <summary>
    /// 插件模块的传递依赖被一并带出
    /// </summary>
    [Fact]
    public void GetModulesWithAllDependencies_IncludesTransitiveDependencies()
    {
        IPlugInSource source = new TypePlugInSource(typeof(PlsDependentModule));

        var modules = source.GetModulesWithAllDependencies(NullLogger.Instance);

        Assert.Equal(2, modules.Length);
        Assert.Contains(typeof(PlsDependentModule), modules);
        Assert.Contains(typeof(PlsSampleModule), modules);
    }

    /// <summary>
    /// 多个插件模块共享同一依赖时结果去重
    /// </summary>
    [Fact]
    public void GetModulesWithAllDependencies_WhenSharedDependency_Deduplicates()
    {
        IPlugInSource source = new TypePlugInSource(typeof(PlsDependentModule), typeof(PlsSampleModule));

        var modules = source.GetModulesWithAllDependencies(NullLogger.Instance);

        Assert.Equal(2, modules.Length);
        Assert.Equal(modules.Length, modules.Distinct().Count());
    }

    /// <summary>
    /// 无插件模块时返回空集合
    /// </summary>
    [Fact]
    public void GetModulesWithAllDependencies_WhenSourceEmpty_ReturnsEmpty()
    {
        IPlugInSource source = new TypePlugInSource();

        Assert.Empty(source.GetModulesWithAllDependencies(NullLogger.Instance));
    }

    /// <summary>
    /// 插件模块不是曦寒模块时抛出
    /// </summary>
    [Fact]
    public void GetModulesWithAllDependencies_WhenTypeIsNotModule_Throws()
    {
        IPlugInSource source = new TypePlugInSource(typeof(string));

        Assert.Throws<ArgumentException>(() => source.GetModulesWithAllDependencies(NullLogger.Instance));
    }

    /// <summary>
    /// 插件源为空引用时抛出
    /// </summary>
    [Fact]
    public void GetModulesWithAllDependencies_WhenSourceNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => PlugInSourceExtensions.GetModulesWithAllDependencies(null!, NullLogger.Instance));
    }
}
