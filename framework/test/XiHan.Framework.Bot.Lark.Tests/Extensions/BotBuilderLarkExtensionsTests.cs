// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.Extensions;
using XiHan.Framework.Bot.Providers;
using XiHan.Framework.Bot.Lark.Abstractions;
using XiHan.Framework.Bot.Lark.Extensions;
using XiHan.Framework.Bot.Lark.Messaging;
using XiHan.Framework.Bot.Lark.Options;
using XiHan.Framework.Bot.Lark.Stores;

namespace XiHan.Framework.Bot.Lark.Tests.Extensions;

/// <summary>
/// BotBuilder 飞书扩展测试
/// </summary>
/// <remarks>
/// UseLark 的契约有四条：参数校验、单例生命周期、TryAddEnumerable 的幂等、TryAdd 不覆盖应用层实现。
/// 用真实 ServiceCollection 注册后既查描述符也真的解析一次，确保依赖链（提供者 → 配置存储 → 选项）是通的。
/// </remarks>
public class BotBuilderLarkExtensionsTests
{
    /// <summary>
    /// 扩展返回同一个构建器，支持链式调用
    /// </summary>
    [Fact]
    public void UseLark_Always_ReturnsSameBuilder()
    {
        var builder = new BotBuilder(new ServiceCollection());

        var returned = builder.UseLark(options => options.AccessToken = "abc-token");

        Assert.Same(builder, returned);
    }

    /// <summary>
    /// 构建器为空时抛参数异常
    /// </summary>
    [Fact]
    public void UseLark_WhenBuilderNull_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
        {
            BotBuilderLarkExtensions.UseLark(null!, _ => { });
        });

        Assert.Equal("builder", exception.ParamName);
    }

    /// <summary>
    /// 配置委托为空时抛参数异常
    /// </summary>
    [Fact]
    public void UseLark_WhenConfigureNull_Throws()
    {
        var builder = new BotBuilder(new ServiceCollection());

        var exception = Assert.Throws<ArgumentNullException>(() =>
        {
            builder.UseLark(null!);
        });

        Assert.Equal("configure", exception.ParamName);
    }

    /// <summary>
    /// 配置存储以单例注册
    /// </summary>
    [Fact]
    public void UseLark_Always_RegistersConfigStoreAsSingleton()
    {
        var services = new ServiceCollection();
        new BotBuilder(services).UseLark(options => options.AccessToken = "abc-token");

        var descriptor = Assert.Single(services.Where(item => item.ServiceType == typeof(ILarkConfigStore)));

        Assert.Equal(typeof(DefaultLarkConfigStore), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    /// <summary>
    /// 飞书提供者以单例加入提供者集合
    /// </summary>
    [Fact]
    public void UseLark_Always_RegistersProviderAsEnumerableSingleton()
    {
        var services = new ServiceCollection();
        new BotBuilder(services).UseLark(options => options.AccessToken = "abc-token");

        var descriptor = Assert.Single(services.Where(item => item.ServiceType == typeof(IBotProvider)));

        Assert.Equal(typeof(LarkBotProvider), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    /// <summary>
    /// 重复调用不会重复注册提供者
    /// </summary>
    [Fact]
    public void UseLark_CalledTwice_RegistersProviderOnce()
    {
        var services = new ServiceCollection();
        var builder = new BotBuilder(services);

        builder.UseLark(options => options.AccessToken = "abc-token");
        builder.UseLark(options => options.AccessToken = "abc-token");

        Assert.Single(services.Where(item => item.ServiceType == typeof(IBotProvider)));
        Assert.Single(services.Where(item => item.ServiceType == typeof(ILarkConfigStore)));
    }

    /// <summary>
    /// 应用层已注册的配置存储不会被覆盖
    /// </summary>
    /// <remarks>
    /// 这是 ILarkConfigStore 注释里承诺的 TryAdd 语义：应用层可以换成数据库实现。
    /// </remarks>
    [Fact]
    public void UseLark_WhenConfigStoreAlreadyRegistered_KeepsExistingImplementation()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILarkConfigStore>(new StubLarkConfigStore());

        new BotBuilder(services).UseLark(options => options.AccessToken = "abc-token");

        using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<ILarkConfigStore>();

        Assert.True(store is StubLarkConfigStore);
    }

    /// <summary>
    /// 注册后整条依赖链可以真实解析
    /// </summary>
    [Fact]
    public void UseLark_Always_ResolvesProviderWithConfiguredOptions()
    {
        var services = new ServiceCollection();
        new BotBuilder(services).UseLark(options =>
        {
            options.AccessToken = "abc-token";
            options.Secret = "sign-secret";
        });

        using var serviceProvider = services.BuildServiceProvider();
        var botProvider = Assert.Single(serviceProvider.GetServices<IBotProvider>());
        var options = serviceProvider.GetRequiredService<IOptions<LarkOptions>>().Value;

        Assert.True(botProvider is LarkBotProvider);
        Assert.Equal("Lark", botProvider.Name);
        Assert.Equal("abc-token", options.AccessToken);
        Assert.Equal("sign-secret", options.Secret);
    }

    /// <summary>
    /// 提供者与配置存储都是单例，跨作用域拿到同一实例
    /// </summary>
    [Fact]
    public void UseLark_Always_ResolvesSingletonInstances()
    {
        var services = new ServiceCollection();
        new BotBuilder(services).UseLark(options => options.AccessToken = "abc-token");

        using var serviceProvider = services.BuildServiceProvider();
        using var firstScope = serviceProvider.CreateScope();
        using var secondScope = serviceProvider.CreateScope();

        Assert.Same(
            firstScope.ServiceProvider.GetRequiredService<ILarkConfigStore>(),
            secondScope.ServiceProvider.GetRequiredService<ILarkConfigStore>());
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
