// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Bot.Clients;
using XiHan.Framework.Bot.Core;
using XiHan.Framework.Bot.Extensions.DependencyInjection;
using XiHan.Framework.Bot.Pipeline;
using XiHan.Framework.Bot.Strategy;
using XiHan.Framework.Bot.Template;
using XiHan.Framework.Templating.Services;

namespace XiHan.Framework.Bot.Tests;

/// <summary>
/// <see cref="XiHanBotServiceCollectionExtensions"/> 测试
/// </summary>
/// <remarks>
/// 注册顺序即管道执行顺序（日志 → 环境过滤 → 重试 → 限流），这是有语义的：
/// 环境过滤必须在重试之外，否则被跳过的调度会被重试管道反复重跑；限流在最内层，
/// 保证每次真实投递都消耗令牌。全部用 TryAdd*，重复调用不得产生重复注册。
/// </remarks>
public class XiHanBotServiceCollectionExtensionsTests
{
    /// <summary>
    /// 服务集合为 null 时抛出参数空异常
    /// </summary>
    [Fact]
    public void AddXiHanBot_WhenServicesNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => { _ = XiHanBotServiceCollectionExtensions.AddXiHanBot(null!); });
    }

    /// <summary>
    /// 核心服务都能解析出来
    /// </summary>
    [Fact]
    public void AddXiHanBot_RegistersCoreServices()
    {
        var provider = BuildProvider();

        Assert.NotNull(provider.GetRequiredService<BotProviderManager>());
        Assert.NotNull(provider.GetRequiredService<BotDispatcher>());
        Assert.NotNull(provider.GetRequiredService<IBotClient>());
        Assert.NotNull(provider.GetRequiredService<IBotTemplateEngine>());
    }

    /// <summary>
    /// 核心服务是单例
    /// </summary>
    [Fact]
    public void AddXiHanBot_CoreServicesAreSingleton()
    {
        var provider = BuildProvider();

        Assert.Same(provider.GetRequiredService<IBotClient>(), provider.GetRequiredService<IBotClient>());
        Assert.Same(provider.GetRequiredService<BotDispatcher>(), provider.GetRequiredService<BotDispatcher>());
        Assert.Same(provider.GetRequiredService<BotProviderManager>(), provider.GetRequiredService<BotProviderManager>());
        Assert.Same(provider.GetRequiredService<IBotTemplateEngine>(), provider.GetRequiredService<IBotTemplateEngine>());
    }

    /// <summary>
    /// 默认实现类型正确
    /// </summary>
    [Fact]
    public void AddXiHanBot_UsesDefaultImplementations()
    {
        var provider = BuildProvider();

        Assert.IsType<BotClient>(provider.GetRequiredService<IBotClient>());
        Assert.IsType<BotTemplateEngine>(provider.GetRequiredService<IBotTemplateEngine>());
    }

    /// <summary>
    /// 三个内置策略全部注册且各自名称唯一
    /// </summary>
    [Fact]
    public void AddXiHanBot_RegistersThreeStrategies()
    {
        var strategies = BuildProvider().GetServices<IBotStrategy>().ToArray();

        Assert.Equal(3, strategies.Length);
        Assert.Contains(strategies, strategy => strategy is BroadcastStrategy);
        Assert.Contains(strategies, strategy => strategy is FailoverStrategy);
        Assert.Contains(strategies, strategy => strategy is PriorityStrategy);
        Assert.Equal(3, strategies.Select(strategy => strategy.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    /// <summary>
    /// 四个内置管道按"日志 → 环境过滤 → 重试 → 限流"的顺序注册
    /// </summary>
    [Fact]
    public void AddXiHanBot_RegistersPipelinesInExecutionOrder()
    {
        var pipelines = BuildProvider().GetServices<IBotPipeline>().ToArray();

        Assert.Equal(4, pipelines.Length);
        Assert.IsType<LoggingPipeline>(pipelines[0]);
        Assert.IsType<EnvironmentFilterPipeline>(pipelines[1]);
        Assert.IsType<RetryPipeline>(pipelines[2]);
        Assert.IsType<RateLimitPipeline>(pipelines[3]);
    }

    /// <summary>
    /// 没有注册宿主环境时环境过滤管道仍可构造（依赖为可选参数）
    /// </summary>
    [Fact]
    public void AddXiHanBot_WithoutHostEnvironment_ResolvesEnvironmentFilterPipeline()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddXiHanBot();

        var pipelines = services.BuildServiceProvider().GetServices<IBotPipeline>().ToArray();

        Assert.Contains(pipelines, pipeline => pipeline is EnvironmentFilterPipeline);
    }

    /// <summary>
    /// 顺带把模板服务也注册进去，模板引擎才能在作用域里取到它
    /// </summary>
    [Fact]
    public void AddXiHanBot_RegistersTemplatingServices()
    {
        using var scope = BuildProvider().CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ITemplateService>());
    }

    /// <summary>
    /// 重复调用不产生重复注册
    /// </summary>
    [Fact]
    public void AddXiHanBot_CalledTwice_DoesNotDuplicateRegistrations()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddXiHanBot();
        services.AddXiHanBot();

        var provider = services.BuildServiceProvider();

        Assert.Equal(3, provider.GetServices<IBotStrategy>().Count());
        Assert.Equal(4, provider.GetServices<IBotPipeline>().Count());
        Assert.Single(services.Where(descriptor => descriptor.ServiceType == typeof(IBotClient)));
    }

    /// <summary>
    /// 配置回调拿到的构建器绑定的是同一个服务集合
    /// </summary>
    [Fact]
    public void AddXiHanBot_ConfigureCallback_ReceivesBuilderOnSameCollection()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var invoked = 0;

        services.AddXiHanBot(builder =>
        {
            invoked++;
            Assert.Same(services, builder.Services);
        });

        Assert.Equal(1, invoked);
    }

    /// <summary>
    /// 扩展方法返回传入的服务集合以便链式调用
    /// </summary>
    [Fact]
    public void AddXiHanBot_ReturnsSameCollection()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services.AddXiHanBot());
    }

    /// <summary>
    /// 关键服务的生命周期都是单例
    /// </summary>
    [Theory]
    [InlineData(typeof(BotProviderManager))]
    [InlineData(typeof(BotDispatcher))]
    [InlineData(typeof(IBotClient))]
    [InlineData(typeof(IBotTemplateEngine))]
    public void AddXiHanBot_KeyServicesAreSingletonLifetime(Type serviceType)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddXiHanBot();

        var descriptor = services.Single(item => item.ServiceType == serviceType);

        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    /// <summary>
    /// 没有任何提供者时调度返回失败而不是抛出，整条链路可空跑
    /// </summary>
    [Fact]
    public async Task AddXiHanBot_WithoutProvider_DispatchReturnsFailure()
    {
        var client = BuildProvider().GetRequiredService<IBotClient>();

        var result = await client.SendAsync(
            new XiHan.Framework.Bot.Models.BotMessage { Content = "hi" },
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("No bot provider configured.", result.ErrorMessage);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddXiHanBot();
        return services.BuildServiceProvider();
    }
}
