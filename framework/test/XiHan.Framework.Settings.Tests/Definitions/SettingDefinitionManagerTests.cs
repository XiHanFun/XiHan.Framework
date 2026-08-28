// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Settings.Definitions;
using XiHan.Framework.Settings.Options;
using XiHan.Framework.Settings.Tests.Fakes;

namespace XiHan.Framework.Settings.Tests.Definitions;

/// <summary>
/// 设置定义管理器测试
/// </summary>
/// <remarks>
/// 覆盖"按选项中的提供者类型列表反射实例化 → 依次 Define → 汇总成只读表并缓存"这条主链路，
/// 以及未知键返回 null、重复定义直接失败两个边界。
/// </remarks>
public class SettingDefinitionManagerTests
{
    /// <summary>
    /// 汇总表包含每个提供者贡献的定义
    /// </summary>
    [Fact]
    public void GetOrNull_AggregatesDefinitionsFromEveryProvider()
    {
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var manager = CreateManager(
            serviceProvider,
            typeof(AlphaSettingDefinitionProvider),
            typeof(BetaSettingDefinitionProvider));

        var alpha = manager.GetOrNull(AlphaSettingDefinitionProvider.SettingName);
        var beta = manager.GetOrNull(BetaSettingDefinitionProvider.SettingName);

        Assert.NotNull(alpha);
        Assert.NotNull(beta);
        Assert.Equal(AlphaSettingDefinitionProvider.SettingDefaultValue, alpha!.DefaultValue);
        Assert.Equal(AlphaSettingDefinitionProvider.SettingGroup, alpha.Group);
        Assert.Equal(BetaSettingDefinitionProvider.SettingDefaultValue, beta!.DefaultValue);
    }

    /// <summary>
    /// 未知键返回 null，不抛异常
    /// </summary>
    [Fact]
    public void GetOrNull_WhenNameNotDefined_ReturnsNull()
    {
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var manager = CreateManager(serviceProvider, typeof(AlphaSettingDefinitionProvider));

        Assert.Null(manager.GetOrNull("Not.Defined"));
    }

    /// <summary>
    /// 获取全部定义返回所有提供者汇总后的结果
    /// </summary>
    [Fact]
    public void GetAll_ReturnsEveryAggregatedDefinition()
    {
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var manager = CreateManager(
            serviceProvider,
            typeof(AlphaSettingDefinitionProvider),
            typeof(BetaSettingDefinitionProvider));

        var all = manager.GetAll();

        Assert.Equal(2, all.Count);
        Assert.Contains(all, x => x.Name == AlphaSettingDefinitionProvider.SettingName);
        Assert.Contains(all, x => x.Name == BetaSettingDefinitionProvider.SettingName);
    }

    /// <summary>
    /// 未配置任何提供者时汇总表为空
    /// </summary>
    [Fact]
    public void GetAll_WhenNoProviderConfigured_ReturnsEmpty()
    {
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var manager = CreateManager(serviceProvider);

        Assert.Empty(manager.GetAll());
    }

    /// <summary>
    /// 汇总表只构建一次，多次读取拿到的是同一批定义实例
    /// </summary>
    /// <remarks>
    /// 定义实例每次 Define 都会 new，若懒加载缓存失效，两次取回的引用必然不同；
    /// 用引用相等来验证缓存，比数调用次数更贴近"定义表只读且稳定"这个契约。
    /// </remarks>
    [Fact]
    public void GetOrNull_BuildsDefinitionTableOnlyOnce()
    {
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var manager = CreateManager(serviceProvider, typeof(AlphaSettingDefinitionProvider));

        var first = manager.GetOrNull(AlphaSettingDefinitionProvider.SettingName);
        var second = manager.GetOrNull(AlphaSettingDefinitionProvider.SettingName);
        var fromGetAll = manager.GetAll().Single();

        Assert.Same(first, second);
        Assert.Same(first, fromGetAll);
    }

    /// <summary>
    /// 两个提供者定义同名设置时，汇总阶段直接失败
    /// </summary>
    [Fact]
    public void GetOrNull_WhenTwoProvidersDefineSameName_ThrowsXiHanException()
    {
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var manager = CreateManager(
            serviceProvider,
            typeof(AlphaSettingDefinitionProvider),
            typeof(DuplicateAlphaSettingDefinitionProvider));

        var exception = Assert.Throws<XiHanException>(() => manager.GetOrNull(AlphaSettingDefinitionProvider.SettingName));

        Assert.Contains(AlphaSettingDefinitionProvider.SettingName, exception.Message);
    }

    /// <summary>
    /// 定义提供者由容器作用域实例化，可注入容器内的依赖
    /// </summary>
    [Fact]
    public void GetOrNull_ResolvesProviderDependenciesFromContainer()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new InjectedDefinitionSeed("Injected.Setting", "injected-default"));
        using var serviceProvider = services.BuildServiceProvider();

        var manager = CreateManager(serviceProvider, typeof(InjectedSettingDefinitionProvider));

        var definition = manager.GetOrNull("Injected.Setting");

        Assert.NotNull(definition);
        Assert.Equal("injected-default", definition!.DefaultValue);
    }

    /// <summary>
    /// 定义管理器按单例依赖登记
    /// </summary>
    [Fact]
    public void SettingDefinitionManager_IsSingletonDependency()
    {
        Assert.True(typeof(ISingletonDependency).IsAssignableFrom(typeof(SettingDefinitionManager)));
        Assert.True(typeof(ISettingDefinitionManager).IsAssignableFrom(typeof(SettingDefinitionManager)));
    }

    /// <summary>
    /// 按给定的提供者类型列表构造定义管理器
    /// </summary>
    /// <param name="serviceProvider">服务提供者</param>
    /// <param name="providerTypes">定义提供者类型</param>
    /// <returns>定义管理器</returns>
    private static SettingDefinitionManager CreateManager(IServiceProvider serviceProvider, params Type[] providerTypes)
    {
        var options = new XiHanSettingOptions();
        foreach (var providerType in providerTypes)
        {
            options.DefinitionProviders.Add(providerType);
        }

        return new SettingDefinitionManager(serviceProvider, new OptionsWrapper<XiHanSettingOptions>(options));
    }

    /// <summary>
    /// 供定义提供者从容器注入的种子数据
    /// </summary>
    /// <param name="Name">设置名称</param>
    /// <param name="DefaultValue">默认值</param>
    public sealed record InjectedDefinitionSeed(string Name, string? DefaultValue);

    /// <summary>
    /// 依赖容器内种子数据的设置定义提供者
    /// </summary>
    public sealed class InjectedSettingDefinitionProvider : ISettingDefinitionProvider
    {
        private readonly InjectedDefinitionSeed _seed;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="seed">种子数据</param>
        public InjectedSettingDefinitionProvider(InjectedDefinitionSeed seed)
        {
            _seed = seed;
        }

        /// <summary>
        /// 定义设置
        /// </summary>
        /// <param name="context">设置定义上下文</param>
        public void Define(ISettingDefinitionContext context)
        {
            context.Add(new SettingDefinition(_seed.Name, _seed.DefaultValue));
        }
    }
}
