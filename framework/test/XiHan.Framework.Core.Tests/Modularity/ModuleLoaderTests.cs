// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Logging;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Core.Modularity.PlugIns;

namespace XiHan.Framework.Core.Tests.Modularity;

/// <summary>
/// 模块加载器测试
/// </summary>
/// <remarks>
/// 加载器负责把「起始模块 + 插件源」摊平成拓扑有序的模块描述器序列，四条硬契约：
/// 依赖必须排在依赖者之前；起始模块被强制挪到最后（它承担搭建管道的收尾职责）；
/// 重复依赖只产出一个描述器；环状依赖必须在这一步炸掉而不是留到运行期。
/// 每个模块同时以其自身类型注册成单例，描述器里的实例与容器里的必须是同一个。
/// </remarks>
public class ModuleLoaderTests
{
    /// <summary>
    /// 依赖模块排在依赖者之前
    /// </summary>
    [Fact]
    public void LoadModules_SortsDependenciesBeforeDependents()
    {
        var services = CreateServices();

        var modules = new ModuleLoader().LoadModules(services, typeof(MlStartupModule), new PlugInSourceList());

        var order = modules.Select(m => m.Type).ToList();
        Assert.Equal(3, order.Count);
        Assert.True(order.IndexOf(typeof(MlLeafModule)) < order.IndexOf(typeof(MlMiddleModule)));
        Assert.True(order.IndexOf(typeof(MlMiddleModule)) < order.IndexOf(typeof(MlStartupModule)));
    }

    /// <summary>
    /// 起始模块被排到最后
    /// </summary>
    [Fact]
    public void LoadModules_PlacesStartupModuleLast()
    {
        var services = CreateServices();
        var plugInSources = new PlugInSourceList();
        plugInSources.AddTypes(typeof(MlPlugInModule));

        var modules = new ModuleLoader().LoadModules(services, typeof(MlStartupModule), plugInSources);

        Assert.Equal(typeof(MlStartupModule), modules[^1].Type);
    }

    /// <summary>
    /// 传递依赖被完整展开且每个模块只出现一次
    /// </summary>
    [Fact]
    public void LoadModules_WhenDependencyDeclaredTwice_ProducesSingleDescriptor()
    {
        var services = CreateServices();

        var modules = new ModuleLoader().LoadModules(services, typeof(MlStartupModule), new PlugInSourceList());

        Assert.Single(modules, m => m.Type == typeof(MlLeafModule));
    }

    /// <summary>
    /// 描述器上带有解析出的依赖关系
    /// </summary>
    [Fact]
    public void LoadModules_ResolvesDependenciesOnDescriptors()
    {
        var services = CreateServices();

        var modules = new ModuleLoader().LoadModules(services, typeof(MlStartupModule), new PlugInSourceList());

        var middle = modules.Single(m => m.Type == typeof(MlMiddleModule));
        Assert.Equal(typeof(MlLeafModule), Assert.Single(middle.Dependencies).Type);

        var startup = modules.Single(m => m.Type == typeof(MlStartupModule));
        Assert.Equal(2, startup.Dependencies.Count);
    }

    /// <summary>
    /// 每个模块以自身类型注册为单例且与描述器实例一致
    /// </summary>
    [Fact]
    public void LoadModules_RegistersModuleInstanceAsSingleton()
    {
        var services = CreateServices();

        var modules = new ModuleLoader().LoadModules(services, typeof(MlStartupModule), new PlugInSourceList());

        foreach (var module in modules)
        {
            var descriptor = Assert.Single(services, d => d.ServiceType == module.Type);
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
            Assert.Same(module.Instance, descriptor.ImplementationInstance);
        }
    }

    /// <summary>
    /// 插件源里的模块被标记为插件加载
    /// </summary>
    [Fact]
    public void LoadModules_WhenPlugInSourceGiven_MarksModuleAsPlugIn()
    {
        var services = CreateServices();
        var plugInSources = new PlugInSourceList();
        plugInSources.AddTypes(typeof(MlPlugInModule));

        var modules = new ModuleLoader().LoadModules(services, typeof(MlStartupModule), plugInSources);

        Assert.True(modules.Single(m => m.Type == typeof(MlPlugInModule)).IsLoadedAsPlugIn);
        Assert.False(modules.Single(m => m.Type == typeof(MlLeafModule)).IsLoadedAsPlugIn);
    }

    /// <summary>
    /// 插件模块已在依赖图中时不重复加载
    /// </summary>
    [Fact]
    public void LoadModules_WhenPlugInAlreadyInGraph_DoesNotDuplicate()
    {
        var services = CreateServices();
        var plugInSources = new PlugInSourceList();
        plugInSources.AddTypes(typeof(MlLeafModule));

        var modules = new ModuleLoader().LoadModules(services, typeof(MlStartupModule), plugInSources);

        Assert.Equal(3, modules.Length);
        Assert.False(modules.Single(m => m.Type == typeof(MlLeafModule)).IsLoadedAsPlugIn);
    }

    /// <summary>
    /// 环状依赖在拓扑排序阶段报错
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void LoadModules_WhenCyclicDependency_Throws()
    {
        var services = CreateServices();

        var exception = Assert.Throws<ArgumentException>(() =>
            new ModuleLoader().LoadModules(services, typeof(MlCycleAModule), new PlugInSourceList()));

        Assert.Contains("循环依赖", exception.Message);
    }

    /// <summary>
    /// 起始类型不是模块时报错
    /// </summary>
    [Fact]
    public void LoadModules_WhenStartupTypeIsNotModule_Throws()
    {
        var services = CreateServices();

        var exception = Assert.Throws<ArgumentException>(() =>
            new ModuleLoader().LoadModules(services, typeof(ModuleLoaderTests), new PlugInSourceList()));

        Assert.Contains("不是曦寒模块", exception.Message);
    }

    /// <summary>
    /// 服务集合为空时抛出
    /// </summary>
    [Fact]
    public void LoadModules_WhenServicesNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ModuleLoader().LoadModules(null!, typeof(MlLeafModule), new PlugInSourceList()));
    }

    /// <summary>
    /// 起始模块类型为空时抛出
    /// </summary>
    [Fact]
    public void LoadModules_WhenStartupModuleTypeNull_Throws()
    {
        var services = CreateServices();

        Assert.Throws<ArgumentNullException>(() =>
            new ModuleLoader().LoadModules(services, null!, new PlugInSourceList()));
    }

    /// <summary>
    /// 插件源列表为空时抛出
    /// </summary>
    [Fact]
    public void LoadModules_WhenPlugInSourcesNull_Throws()
    {
        var services = CreateServices();

        Assert.Throws<ArgumentNullException>(() =>
            new ModuleLoader().LoadModules(services, typeof(MlLeafModule), null!));
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
/// 加载器测试用叶子模块
/// </summary>
internal class MlLeafModule : XiHanModule;

/// <summary>
/// 加载器测试用中间模块
/// </summary>
[DependsOn(typeof(MlLeafModule))]
internal class MlMiddleModule : XiHanModule;

/// <summary>
/// 加载器测试用起始模块
/// </summary>
[DependsOn(typeof(MlMiddleModule), typeof(MlLeafModule))]
internal class MlStartupModule : XiHanModule;

/// <summary>
/// 加载器测试用插件模块
/// </summary>
internal class MlPlugInModule : XiHanModule;

/// <summary>
/// 加载器测试用环状依赖模块甲
/// </summary>
[DependsOn(typeof(MlCycleBModule))]
internal class MlCycleAModule : XiHanModule;

/// <summary>
/// 加载器测试用环状依赖模块乙
/// </summary>
[DependsOn(typeof(MlCycleAModule))]
internal class MlCycleBModule : XiHanModule;
