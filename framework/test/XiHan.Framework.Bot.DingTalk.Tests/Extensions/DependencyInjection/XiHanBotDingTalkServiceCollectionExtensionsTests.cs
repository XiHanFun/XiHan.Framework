// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.DingTalk.Abstractions;
using XiHan.Framework.Bot.DingTalk.Extensions.DependencyInjection;
using XiHan.Framework.Bot.DingTalk.Messaging;
using XiHan.Framework.Bot.DingTalk.Options;
using XiHan.Framework.Bot.DingTalk.Stores;
using XiHan.Framework.Bot.DingTalk.Tests.Fakes;
using XiHan.Framework.Bot.Providers;

namespace XiHan.Framework.Bot.DingTalk.Tests.Extensions.DependencyInjection;

/// <summary>
/// 钉钉 Bot 服务注册扩展测试
/// </summary>
/// <remarks>
/// 与 BotBuilder 入口相比，这个入口的差异点在于配置委托可以省略：
/// 省略时不能凭空写入一份空选项（那会把配置文件绑定上来的值覆盖掉），
/// 所以"不传委托就不注册 IConfigureOptions"这条语义必须被钉死。
/// </remarks>
public class XiHanBotDingTalkServiceCollectionExtensionsTests
{
    /// <summary>
    /// 服务集合为空时抛参数空异常
    /// </summary>
    [Fact]
    public void AddXiHanBotDingTalk_WhenServicesIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => XiHanBotDingTalkServiceCollectionExtensions.AddXiHanBotDingTalk(null!));
    }

    /// <summary>
    /// 返回同一个服务集合以支持链式调用
    /// </summary>
    [Fact]
    public void AddXiHanBotDingTalk_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services.AddXiHanBotDingTalk());
    }

    /// <summary>
    /// 注册配置存储与提供者，且都是单例
    /// </summary>
    [Fact]
    public void AddXiHanBotDingTalk_RegistersStoreAndProviderAsSingletons()
    {
        var services = new ServiceCollection();

        services.AddXiHanBotDingTalk();

        var store = Assert.Single(services.Where(item => item.ServiceType == typeof(IDingTalkConfigStore)));
        var provider = Assert.Single(services.Where(item => item.ServiceType == typeof(IBotProvider)));

        Assert.Equal(typeof(DefaultDingTalkConfigStore), store.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, store.Lifetime);
        Assert.Equal(typeof(DingTalkBotProvider), provider.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, provider.Lifetime);
    }

    /// <summary>
    /// 省略配置委托时不写入任何选项配置
    /// </summary>
    [Fact]
    public void AddXiHanBotDingTalk_WithoutConfigure_DoesNotRegisterOptionsConfiguration()
    {
        var services = new ServiceCollection();

        services.AddXiHanBotDingTalk();

        Assert.DoesNotContain(services, item => item.ServiceType == typeof(IConfigureOptions<DingTalkOptions>));
    }

    /// <summary>
    /// 传入配置委托时写入选项配置
    /// </summary>
    [Fact]
    public void AddXiHanBotDingTalk_WithConfigure_RegistersOptionsConfiguration()
    {
        var services = new ServiceCollection();

        services.AddXiHanBotDingTalk(options => options.AccessToken = "access-token-value");

        Assert.Contains(services, item => item.ServiceType == typeof(IConfigureOptions<DingTalkOptions>));
    }

    /// <summary>
    /// 重复注册不会产生重复的提供者与配置存储
    /// </summary>
    [Fact]
    public void AddXiHanBotDingTalk_CalledTwice_KeepsSingleRegistrationEach()
    {
        var services = new ServiceCollection();

        services.AddXiHanBotDingTalk();
        services.AddXiHanBotDingTalk();

        Assert.Single(services.Where(item => item.ServiceType == typeof(IBotProvider)));
        Assert.Single(services.Where(item => item.ServiceType == typeof(IDingTalkConfigStore)));
    }

    /// <summary>
    /// 应用层已注册的配置存储不会被默认实现顶掉
    /// </summary>
    [Fact]
    public void AddXiHanBotDingTalk_WhenCustomConfigStoreRegistered_DoesNotOverrideIt()
    {
        var services = new ServiceCollection();
        var custom = new FakeDingTalkConfigStore(new DingTalkOptions { AccessToken = "from-database" });
        services.AddSingleton<IDingTalkConfigStore>(custom);

        services.AddXiHanBotDingTalk();

        var descriptor = Assert.Single(services.Where(item => item.ServiceType == typeof(IDingTalkConfigStore)));

        Assert.Same(custom, descriptor.ImplementationInstance);
    }

    /// <summary>
    /// 注册后可从容器解析出提供者，且配置委托生效
    /// </summary>
    [Fact]
    public async Task AddXiHanBotDingTalk_RegisteredGraph_IsResolvable()
    {
        var services = new ServiceCollection();

        services.AddXiHanBotDingTalk(options =>
        {
            options.AccessToken = "access-token-value";
            options.Secret = "SECsecretvalue";
        });

        using var serviceProvider = services.BuildServiceProvider();

        var botProvider = Assert.Single(serviceProvider.GetServices<IBotProvider>());

        Assert.IsType<DingTalkBotProvider>(botProvider);

        var store = serviceProvider.GetRequiredService<IDingTalkConfigStore>();
        var options = await store.GetAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(options);
        Assert.Equal("access-token-value", options.AccessToken);
        Assert.Equal("SECsecretvalue", options.Secret);
    }
}
