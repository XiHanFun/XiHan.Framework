// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.Lark.Abstractions;
using XiHan.Framework.Bot.Lark.Extensions.DependencyInjection;
using XiHan.Framework.Bot.Lark.Messaging;
using XiHan.Framework.Bot.Lark.Options;
using XiHan.Framework.Bot.Lark.Stores;
using XiHan.Framework.Bot.Providers;

namespace XiHan.Framework.Bot.Lark.Tests.Extensions.DependencyInjection;

/// <summary>
/// 飞书 Bot 服务注册扩展测试
/// </summary>
/// <remarks>
/// AddXiHanBotLark 与 UseLark 的区别只有一点：配置委托可省略，省略时「不写入选项」。
/// 这条差异直接决定 IOptionsMonitor 会不会被引入，模块装配路径（XiHanBotLarkModule）走的正是这个重载，
/// 所以单独覆盖，不与 UseLark 的用例合并。
/// </remarks>
public class XiHanBotLarkServiceCollectionExtensionsTests
{
    /// <summary>
    /// 扩展返回同一个服务集合，支持链式调用
    /// </summary>
    [Fact]
    public void AddXiHanBotLark_Always_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var returned = services.AddXiHanBotLark();

        Assert.Same(services, returned);
    }

    /// <summary>
    /// 服务集合为空时抛参数异常
    /// </summary>
    [Fact]
    public void AddXiHanBotLark_WhenServicesNull_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
        {
            XiHanBotLarkServiceCollectionExtensions.AddXiHanBotLark(null!);
        });

        Assert.Equal("services", exception.ParamName);
    }

    /// <summary>
    /// 配置存储与提供者均以单例注册
    /// </summary>
    [Fact]
    public void AddXiHanBotLark_Always_RegistersStoreAndProviderAsSingleton()
    {
        var services = new ServiceCollection();

        services.AddXiHanBotLark();

        var store = Assert.Single(services, item => item.ServiceType == typeof(ILarkConfigStore));
        var provider = Assert.Single(services, item => item.ServiceType == typeof(IBotProvider));

        Assert.Equal(typeof(DefaultLarkConfigStore), store.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, store.Lifetime);
        Assert.Equal(typeof(LarkBotProvider), provider.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, provider.Lifetime);
    }

    /// <summary>
    /// 未传配置委托时不写入选项配置
    /// </summary>
    [Fact]
    public void AddXiHanBotLark_WhenConfigureOmitted_DoesNotRegisterOptionsConfiguration()
    {
        var services = new ServiceCollection();

        services.AddXiHanBotLark();

        Assert.DoesNotContain(services, item => item.ServiceType == typeof(IConfigureOptions<LarkOptions>));
    }

    /// <summary>
    /// 传入配置委托时选项可被解析出配置后的值
    /// </summary>
    [Fact]
    public void AddXiHanBotLark_WhenConfigureProvided_AppliesOptions()
    {
        var services = new ServiceCollection();

        services.AddXiHanBotLark(options =>
        {
            options.AccessToken = "abc-token";
            options.KeyWord = "Alert";
        });

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<LarkOptions>>().Value;

        Assert.Equal("abc-token", options.AccessToken);
        Assert.Equal("Alert", options.KeyWord);
    }

    /// <summary>
    /// 重复注册不会产生重复的提供者与配置存储
    /// </summary>
    [Fact]
    public void AddXiHanBotLark_CalledTwice_RegistersEachServiceOnce()
    {
        var services = new ServiceCollection();

        services.AddXiHanBotLark(options => options.AccessToken = "abc-token");
        services.AddXiHanBotLark(options => options.AccessToken = "def-token");

        Assert.Single(services, item => item.ServiceType == typeof(ILarkConfigStore));
        Assert.Single(services, item => item.ServiceType == typeof(IBotProvider));
    }

    /// <summary>
    /// 重复注册时后一次的配置委托依然生效
    /// </summary>
    /// <remarks>
    /// Configure 是累加的，最后注册的委托最后执行，这决定了「重复调用改配置」的最终态。
    /// </remarks>
    [Fact]
    public void AddXiHanBotLark_CalledTwice_AppliesLastConfigureWins()
    {
        var services = new ServiceCollection();

        services.AddXiHanBotLark(options => options.AccessToken = "abc-token");
        services.AddXiHanBotLark(options => options.AccessToken = "def-token");

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<LarkOptions>>().Value;

        Assert.Equal("def-token", options.AccessToken);
    }

    /// <summary>
    /// 应用层已注册的配置存储不会被覆盖
    /// </summary>
    [Fact]
    public void AddXiHanBotLark_WhenConfigStoreAlreadyRegistered_KeepsExistingImplementation()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILarkConfigStore>(new StubLarkConfigStore());

        services.AddXiHanBotLark(options => options.AccessToken = "abc-token");

        using var serviceProvider = services.BuildServiceProvider();

        Assert.True(serviceProvider.GetRequiredService<ILarkConfigStore>() is StubLarkConfigStore);
    }

    /// <summary>
    /// 注册后飞书提供者可从提供者集合中解析出来
    /// </summary>
    [Fact]
    public void AddXiHanBotLark_WhenConfigureProvided_ResolvesLarkProvider()
    {
        var services = new ServiceCollection();

        services.AddXiHanBotLark(options => options.AccessToken = "abc-token");

        using var serviceProvider = services.BuildServiceProvider();
        var botProvider = Assert.Single(serviceProvider.GetServices<IBotProvider>());

        Assert.True(botProvider is LarkBotProvider);
        Assert.Equal("Lark", botProvider.Name);
    }

    /// <summary>
    /// 测试用的配置存储替身
    /// </summary>
    private sealed class StubLarkConfigStore : ILarkConfigStore
    {
        /// <summary>
        /// 获取配置
        /// </summary>
        public Task<LarkOptions?> GetAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<LarkOptions?>(new LarkOptions());
        }
    }
}
