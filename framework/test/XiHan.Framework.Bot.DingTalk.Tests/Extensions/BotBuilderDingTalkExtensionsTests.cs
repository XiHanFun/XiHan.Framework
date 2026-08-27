// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.Consts;
using XiHan.Framework.Bot.DingTalk.Abstractions;
using XiHan.Framework.Bot.DingTalk.Extensions;
using XiHan.Framework.Bot.DingTalk.Messaging;
using XiHan.Framework.Bot.DingTalk.Options;
using XiHan.Framework.Bot.DingTalk.Stores;
using XiHan.Framework.Bot.DingTalk.Tests.Fakes;
using XiHan.Framework.Bot.Extensions;
using XiHan.Framework.Bot.Providers;

namespace XiHan.Framework.Bot.DingTalk.Tests.Extensions;

/// <summary>
/// BotBuilder 钉钉扩展测试
/// </summary>
/// <remarks>
/// 这个扩展是应用侧启用钉钉渠道的唯一入口，契约有四点：
/// 参数守卫、可链式返回、提供者按 TryAddEnumerable 去重注册为单例、配置存储按 TryAdd 让位于应用层实现。
/// 其中"重复调用不产生重复提供者"最容易在多处 UseDingTalk 时踩坑——重复注册会让同一条消息被发两遍。
/// </remarks>
public class BotBuilderDingTalkExtensionsTests
{
    /// <summary>
    /// 构建器为空时抛参数空异常
    /// </summary>
    [Fact]
    public void UseDingTalk_WhenBuilderIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => BotBuilderDingTalkExtensions.UseDingTalk(null!, _ => { }));
    }

    /// <summary>
    /// 配置委托为空时抛参数空异常
    /// </summary>
    [Fact]
    public void UseDingTalk_WhenConfigureIsNull_ThrowsArgumentNullException()
    {
        var builder = new BotBuilder(new ServiceCollection());

        Assert.Throws<ArgumentNullException>(() => builder.UseDingTalk(null!));
    }

    /// <summary>
    /// 返回同一个构建器以支持链式调用
    /// </summary>
    [Fact]
    public void UseDingTalk_ReturnsSameBuilder()
    {
        var builder = new BotBuilder(new ServiceCollection());

        var returned = builder.UseDingTalk(_ => { });

        Assert.Same(builder, returned);
    }

    /// <summary>
    /// 提供者以单例形式注册进提供者集合
    /// </summary>
    [Fact]
    public void UseDingTalk_RegistersProviderAsSingleton()
    {
        var services = new ServiceCollection();

        new BotBuilder(services).UseDingTalk(_ => { });

        var descriptor = Assert.Single(services.Where(item => item.ServiceType == typeof(IBotProvider)));

        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(DingTalkBotProvider), descriptor.ImplementationType);
    }

    /// <summary>
    /// 配置存储默认落到选项实现上
    /// </summary>
    [Fact]
    public void UseDingTalk_RegistersDefaultConfigStoreAsSingleton()
    {
        var services = new ServiceCollection();

        new BotBuilder(services).UseDingTalk(_ => { });

        var descriptor = Assert.Single(services.Where(item => item.ServiceType == typeof(IDingTalkConfigStore)));

        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(DefaultDingTalkConfigStore), descriptor.ImplementationType);
    }

    /// <summary>
    /// 重复启用不会产生重复的提供者注册
    /// </summary>
    [Fact]
    public void UseDingTalk_CalledTwice_KeepsSingleProviderRegistration()
    {
        var services = new ServiceCollection();
        var builder = new BotBuilder(services);

        builder.UseDingTalk(_ => { });
        builder.UseDingTalk(_ => { });

        Assert.Single(services.Where(item => item.ServiceType == typeof(IBotProvider)));
        Assert.Single(services.Where(item => item.ServiceType == typeof(IDingTalkConfigStore)));
    }

    /// <summary>
    /// 应用层已注册的配置存储不会被默认实现顶掉
    /// </summary>
    /// <remarks>
    /// 接口注释明确写了"应用层可注册数据库实现覆盖（TryAdd 语义）"，这是把配置搬进数据库的前提。
    /// </remarks>
    [Fact]
    public void UseDingTalk_WhenCustomConfigStoreRegistered_DoesNotOverrideIt()
    {
        var services = new ServiceCollection();
        var custom = new FakeDingTalkConfigStore(new DingTalkOptions { AccessToken = "from-database" });
        services.AddSingleton<IDingTalkConfigStore>(custom);

        new BotBuilder(services).UseDingTalk(_ => { });

        var descriptor = Assert.Single(services.Where(item => item.ServiceType == typeof(IDingTalkConfigStore)));

        Assert.Same(custom, descriptor.ImplementationInstance);
    }

    /// <summary>
    /// 配置委托最终作用到解析出来的配置存储上
    /// </summary>
    [Fact]
    public async Task UseDingTalk_ConfiguredOptions_FlowIntoResolvedConfigStore()
    {
        var services = new ServiceCollection();

        new BotBuilder(services).UseDingTalk(options =>
        {
            options.AccessToken = "access-token-value";
            options.Secret = "SECsecretvalue";
            options.KeyWord = "告警";
        });

        using var provider = services.BuildServiceProvider();

        var botProvider = Assert.Single(provider.GetServices<IBotProvider>());

        Assert.IsType<DingTalkBotProvider>(botProvider);
        Assert.Equal(BotProviderNames.DingTalk, botProvider.Name);

        var store = provider.GetRequiredService<IDingTalkConfigStore>();

        Assert.IsType<DefaultDingTalkConfigStore>(store);

        var options = await store.GetAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(options);
        Assert.Equal("access-token-value", options.AccessToken);
        Assert.Equal("SECsecretvalue", options.Secret);
        Assert.Equal("告警", options.KeyWord);
        Assert.Equal("access-token-value", provider.GetRequiredService<IOptions<DingTalkOptions>>().Value.AccessToken);
    }
}
