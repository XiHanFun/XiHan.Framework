// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Traffic.Extensions.DependencyInjection;
using XiHan.Framework.Traffic.GrayRouting.Abstractions;
using XiHan.Framework.Traffic.GrayRouting.Enums;
using XiHan.Framework.Traffic.GrayRouting.Implementations;
using XiHan.Framework.Traffic.GrayRouting.Matchers;
using XiHan.Framework.Traffic.GrayRouting.Models;
using XiHan.Framework.Traffic.Tests.Fakes;

namespace XiHan.Framework.Traffic.Tests.Extensions.DependencyInjection;

/// <summary>
/// 流量治理服务集合扩展测试
/// </summary>
/// <remarks>
/// 装配契约有三层：默认实现能解析出来且生命周期为单例；引擎与仓储用 TryAdd，允许宿主先注册自己的实现；
/// 匹配器集合要覆盖全部五种内置规则类型，缺一种就意味着对应规则在线上被静默跳过。
/// </remarks>
public class XiHanTrafficServiceCollectionExtensionsTests
{
    /// <summary>
    /// 默认注册的引擎与仓储可以解析出来
    /// </summary>
    [Fact]
    public void AddGrayRouting_ResolvesDefaultEngineAndRepository()
    {
        using var provider = BuildProvider(services => services.AddGrayRouting());

        Assert.IsType<DefaultGrayRuleEngine>(provider.GetRequiredService<IGrayRuleEngine>());
        Assert.IsType<InMemoryGrayRuleRepository>(provider.GetRequiredService<IGrayRuleRepository>());
    }

    /// <summary>
    /// 内置匹配器共五个，且一一覆盖五种内置规则类型
    /// </summary>
    [Fact]
    public void AddGrayRouting_RegistersFiveBuiltInMatchersCoveringEveryBuiltInRuleType()
    {
        using var provider = BuildProvider(services => services.AddGrayRouting());

        var matchers = provider.GetServices<IGrayMatcher>().ToList();

        Assert.Equal(5, matchers.Count);
        Assert.Contains(matchers, matcher => matcher is PercentageGrayMatcher);
        Assert.Contains(matchers, matcher => matcher is UserIdGrayMatcher);
        Assert.Contains(matchers, matcher => matcher is TenantIdGrayMatcher);
        Assert.Contains(matchers, matcher => matcher is HeaderGrayMatcher);
        Assert.Contains(matchers, matcher => matcher is IpAddressGrayMatcher);

        var ruleTypes = matchers.Select(matcher => matcher.RuleType).ToList();

        Assert.Contains(GrayRuleType.Percentage, ruleTypes);
        Assert.Contains(GrayRuleType.UserId, ruleTypes);
        Assert.Contains(GrayRuleType.TenantId, ruleTypes);
        Assert.Contains(GrayRuleType.Header, ruleTypes);
        Assert.Contains(GrayRuleType.IpAddress, ruleTypes);
        Assert.Equal(5, ruleTypes.Distinct().Count());
    }

    /// <summary>
    /// 全部注册都是单例生命周期
    /// </summary>
    /// <remarks>
    /// 引擎持有 IEnumerable&lt;IGrayMatcher&gt;，仓储又是进程内状态，只有单例才能保证规则不会随请求重建。
    /// </remarks>
    [Fact]
    public void AddGrayRouting_UsesSingletonLifetimeForEveryRegistration()
    {
        var services = new ServiceCollection();

        services.AddGrayRouting();

        Assert.All(services, descriptor => Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime));
    }

    /// <summary>
    /// 引擎与仓储在容器中确实是同一个实例
    /// </summary>
    [Fact]
    public void AddGrayRouting_ResolvesSameInstanceOnRepeatedRequests()
    {
        using var provider = BuildProvider(services => services.AddGrayRouting());

        Assert.Same(provider.GetRequiredService<IGrayRuleEngine>(), provider.GetRequiredService<IGrayRuleEngine>());
        Assert.Same(provider.GetRequiredService<IGrayRuleRepository>(), provider.GetRequiredService<IGrayRuleRepository>());
    }

    /// <summary>
    /// 重复调用不会重复注册引擎与仓储
    /// </summary>
    [Fact]
    public void AddGrayRouting_CalledTwice_DoesNotDuplicateEngineOrRepository()
    {
        var services = new ServiceCollection();

        services.AddGrayRouting();
        services.AddGrayRouting();

        var engineDescriptors = services.Where(descriptor => descriptor.ServiceType == typeof(IGrayRuleEngine)).ToList();
        var repositoryDescriptors = services.Where(descriptor => descriptor.ServiceType == typeof(IGrayRuleRepository)).ToList();

        Assert.Single(engineDescriptors);
        Assert.Single(repositoryDescriptors);
    }

    /// <summary>
    /// 宿主先注册的仓储实现不会被默认实现覆盖
    /// </summary>
    [Fact]
    public void AddGrayRouting_KeepsPreRegisteredRepository()
    {
        using var provider = BuildProvider(services =>
        {
            services.AddSingleton<IGrayRuleRepository, StubGrayRuleRepository>();
            services.AddGrayRouting();
        });

        Assert.IsType<StubGrayRuleRepository>(provider.GetRequiredService<IGrayRuleRepository>());
    }

    /// <summary>
    /// 宿主先注册的引擎实现不会被默认实现覆盖
    /// </summary>
    [Fact]
    public void AddGrayRouting_KeepsPreRegisteredEngine()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IGrayRuleEngine>(new FakeGrayRuleEngine());

        services.AddGrayRouting();

        using var provider = services.BuildServiceProvider();

        Assert.IsType<FakeGrayRuleEngine>(provider.GetRequiredService<IGrayRuleEngine>());
    }

    /// <summary>
    /// 自定义匹配器追加进集合而不影响内置匹配器
    /// </summary>
    [Fact]
    public void AddGrayMatcher_AppendsMatcherAlongsideBuiltInOnes()
    {
        using var provider = BuildProvider(services => services.AddGrayRouting().AddGrayMatcher<StubGrayMatcher>());

        var matchers = provider.GetServices<IGrayMatcher>().ToList();

        Assert.Equal(6, matchers.Count);
        Assert.Contains(matchers, matcher => matcher is StubGrayMatcher);
        Assert.Contains(matchers, matcher => matcher is PercentageGrayMatcher);
    }

    /// <summary>
    /// 自定义匹配器按单例注册到 IGrayMatcher 服务类型上
    /// </summary>
    [Fact]
    public void AddGrayMatcher_RegistersSingletonAgainstMatcherServiceType()
    {
        var services = new ServiceCollection();

        services.AddGrayMatcher<StubGrayMatcher>();

        var descriptors = services.ToList();

        Assert.Single(descriptors);
        Assert.Equal(typeof(IGrayMatcher), descriptors[0].ServiceType);
        Assert.Equal(typeof(StubGrayMatcher), descriptors[0].ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptors[0].Lifetime);
    }

    /// <summary>
    /// 替换仓储实现时移除旧注册，不会留下两条描述符
    /// </summary>
    [Fact]
    public void ReplaceGrayRuleRepository_SwapsDefaultImplementationWithoutDuplicating()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddGrayRouting();

        services.ReplaceGrayRuleRepository<StubGrayRuleRepository>();

        var descriptors = services.Where(descriptor => descriptor.ServiceType == typeof(IGrayRuleRepository)).ToList();

        Assert.Single(descriptors);
        Assert.Equal(typeof(StubGrayRuleRepository), descriptors[0].ImplementationType);

        using var provider = services.BuildServiceProvider();

        Assert.IsType<StubGrayRuleRepository>(provider.GetRequiredService<IGrayRuleRepository>());
    }

    /// <summary>
    /// 在没有既有注册时替换等同于新增
    /// </summary>
    [Fact]
    public void ReplaceGrayRuleRepository_OnEmptyCollection_AddsRegistration()
    {
        var services = new ServiceCollection();

        services.ReplaceGrayRuleRepository<StubGrayRuleRepository>();

        var descriptors = services.ToList();

        Assert.Single(descriptors);
        Assert.Equal(typeof(IGrayRuleRepository), descriptors[0].ServiceType);
        Assert.Equal(typeof(StubGrayRuleRepository), descriptors[0].ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptors[0].Lifetime);
    }

    /// <summary>
    /// 三个扩展方法都返回原服务集合，支持链式调用
    /// </summary>
    [Fact]
    public void Extensions_ReturnSameServiceCollectionForChaining()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services.AddGrayRouting());
        Assert.Same(services, services.AddGrayMatcher<StubGrayMatcher>());
        Assert.Same(services, services.ReplaceGrayRuleRepository<StubGrayRuleRepository>());
    }

    /// <summary>
    /// 从容器解析出来的引擎能端到端完成一次请求头灰度决策
    /// </summary>
    [Fact]
    public async Task AddGrayRouting_ResolvedEngineWorksEndToEnd()
    {
        using var provider = BuildProvider(services => services.AddGrayRouting());

        var repository = (InMemoryGrayRuleRepository)provider.GetRequiredService<IGrayRuleRepository>();
        repository.AddRule(new GrayRule
        {
            RuleId = "header-rule",
            RuleName = "请求头灰度",
            RuleType = GrayRuleType.Header,
            IsEnabled = true,
            Priority = 1,
            TargetVersion = "v2",
            Configuration = """{"HeaderName":"X-Gray","HeaderValue":"true"}"""
        });

        var engine = provider.GetRequiredService<IGrayRuleEngine>();
        var context = new GrayContext();
        context.Headers!["X-Gray"] = "true";

        var decision = await engine.DecideAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsGray);
        Assert.Equal("v2", decision.TargetVersion);
        Assert.Equal("header-rule", decision.MatchedRuleId);
    }

    /// <summary>
    /// 构建带日志基础设施的服务提供器
    /// </summary>
    /// <remarks>
    /// DefaultGrayRuleEngine 依赖 ILogger&lt;T&gt;，扩展方法本身不注册日志，所以测试侧必须补 AddLogging。
    /// </remarks>
    private static ServiceProvider BuildProvider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        configure(services);

        return services.BuildServiceProvider();
    }
}
