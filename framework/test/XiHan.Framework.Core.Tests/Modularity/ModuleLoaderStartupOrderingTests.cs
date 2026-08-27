// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Core.Logging;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Core.Modularity.PlugIns;

namespace XiHan.Framework.Core.Tests.Modularity;

/// <summary>
/// 模块加载器起始模块归位测试
/// </summary>
/// <remarks>
/// 「起始模块排在最后」是硬契约：它负责收尾整条装配管道。
/// 归位用的目标下标必须取自拓扑排序结果自身，而不是排序前的入参列表——
/// 依赖图上可能挂着不在入参列表里的描述器（Dependencies 可由公开的 AddDependency 追加，
/// SetDependencies 本身也是可重写的），两者一旦长度不等，起始模块就会被塞到中间。
/// 另外起始模块缺失时必须给出带模块类型名的明确错误，而不是一个越界异常。
/// </remarks>
public class ModuleLoaderStartupOrderingTests
{
    /// <summary>
    /// 依赖图里混入列表外描述器时起始模块仍排在最后
    /// </summary>
    [Fact]
    public void LoadModules_WhenDependencyGraphIsLongerThanModuleList_StillPlacesStartupLast()
    {
        var services = CreateServices();

        var modules = new MlsGhostDependencyModuleLoader().LoadModules(services, typeof(MlsSoloModule), new PlugInSourceList());

        // 排序结果比填充出来的模块列表多了一个幽灵依赖，目标下标若沿用入参长度就会把起始模块挪到最前
        Assert.Equal(2, modules.Length);
        Assert.Equal(typeof(MlsSoloModule), modules[^1].Type);
    }

    /// <summary>
    /// 起始模块自身带依赖时同样排在最后且依赖在前
    /// </summary>
    [Fact]
    public void LoadModules_WhenStartupHasDependencyAndGhostInGraph_KeepsDependenciesBeforeStartup()
    {
        var services = CreateServices();

        var modules = new MlsGhostDependencyModuleLoader().LoadModules(services, typeof(MlsStartupModule), new PlugInSourceList());

        var order = modules.Select(m => m.Type).ToList();
        Assert.Equal(3, order.Count);
        Assert.Equal(typeof(MlsStartupModule), order[^1]);
        Assert.True(order.IndexOf(typeof(MlsLeafModule)) < order.IndexOf(typeof(MlsStartupModule)));
        Assert.True(order.IndexOf(typeof(MlsGhostModule)) < order.IndexOf(typeof(MlsStartupModule)));
    }

    /// <summary>
    /// 起始模块不在模块列表中时抛出带模块类型名的明确异常
    /// </summary>
    [Fact]
    public void LoadModules_WhenStartupModuleMissingFromList_ThrowsWithModuleTypeName()
    {
        var services = CreateServices();

        var exception = Assert.Throws<XiHanException>(() =>
            new MlsMissingStartupModuleLoader().LoadModules(services, typeof(MlsStartupModule), new PlugInSourceList()));

        Assert.Contains(nameof(MlsStartupModule), exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 常规加载路径不受归位逻辑调整影响
    /// </summary>
    /// <remarks>反例：入参与排序结果等长的常规情形，起始模块照旧排在最后。</remarks>
    [Fact]
    public void LoadModules_WhenGraphMatchesModuleList_StillPlacesStartupLast()
    {
        var services = CreateServices();

        var modules = new ModuleLoader().LoadModules(services, typeof(MlsStartupModule), new PlugInSourceList());

        Assert.Equal(2, modules.Length);
        Assert.Equal(typeof(MlsStartupModule), modules[^1].Type);
        Assert.Equal(typeof(MlsLeafModule), modules[0].Type);
    }

    /// <summary>
    /// 构建带初始化日志工厂的服务集合
    /// </summary>
    /// <returns>服务集合</returns>
    private static IServiceCollection CreateServices()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<IInitLoggerFactory>(new DefaultInitLoggerFactory());
        return services;
    }
}

/// <summary>
/// 会往依赖图里挂一个不在模块列表中的描述器的加载器
/// </summary>
/// <remarks>
/// 模拟「依赖描述器来自模块列表之外」的情形：Dependencies 通过公开的 AddDependency 追加即可，
/// 拓扑排序会把这个幽灵依赖一并收进结果，于是排序结果比入参列表长。
/// </remarks>
internal sealed class MlsGhostDependencyModuleLoader : ModuleLoader
{
    /// <summary>
    /// 不参与填充、只挂在依赖边上的描述器
    /// </summary>
    public XiHanModuleDescriptor Ghost { get; } = new(typeof(MlsGhostModule), new MlsGhostModule(), false);

    /// <summary>
    /// 在常规依赖解析之后，给每个模块再挂上幽灵依赖
    /// </summary>
    /// <param name="modules">模块描述器列表</param>
    protected override void SetDependencies(List<XiHanModuleDescriptor> modules)
    {
        base.SetDependencies(modules);

        foreach (var module in modules)
        {
            module.AddDependency(Ghost);
        }
    }
}

/// <summary>
/// 填充时故意漏掉起始模块的加载器
/// </summary>
internal sealed class MlsMissingStartupModuleLoader : ModuleLoader
{
    /// <summary>
    /// 只填充一个与起始模块无关的模块
    /// </summary>
    /// <param name="modules">模块描述器列表</param>
    /// <param name="services">服务集合</param>
    /// <param name="startupModuleType">起始模块类型</param>
    /// <param name="plugInSources">插件源列表</param>
    protected override void FillModules(List<XiHanModuleDescriptor> modules, IServiceCollection services, Type startupModuleType, PlugInSourceList plugInSources)
    {
        modules.Add(CreateModuleDescriptor(services, typeof(MlsOtherModule)));
    }
}

/// <summary>
/// 归位用例的无依赖模块
/// </summary>
internal class MlsSoloModule : XiHanModule;

/// <summary>
/// 归位用例的叶子模块
/// </summary>
internal class MlsLeafModule : XiHanModule;

/// <summary>
/// 归位用例的起始模块
/// </summary>
[DependsOn(typeof(MlsLeafModule))]
internal class MlsStartupModule : XiHanModule;

/// <summary>
/// 只挂在依赖边上、不参与填充的幽灵模块
/// </summary>
internal class MlsGhostModule : XiHanModule;

/// <summary>
/// 顶替起始模块被填充进列表的无关模块
/// </summary>
internal class MlsOtherModule : XiHanModule;
