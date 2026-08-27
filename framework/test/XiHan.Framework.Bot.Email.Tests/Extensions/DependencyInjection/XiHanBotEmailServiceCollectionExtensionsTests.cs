// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.Consts;
using XiHan.Framework.Bot.Email.Abstractions;
using XiHan.Framework.Bot.Email.Extensions.DependencyInjection;
using XiHan.Framework.Bot.Email.Messaging;
using XiHan.Framework.Bot.Email.Options;
using XiHan.Framework.Bot.Email.Stores;
using XiHan.Framework.Bot.Email.Tests.Fakes;
using XiHan.Framework.Bot.Providers;

namespace XiHan.Framework.Bot.Email.Tests.Extensions.DependencyInjection;

/// <summary>
/// <see cref="XiHanBotEmailServiceCollectionExtensions"/> 注册契约测试
/// </summary>
/// <remarks>
/// AddXiHanBotEmail 的 configure 参数是可选的：不传时不写入任何选项配置，
/// 这条语义决定了模块化装配（XiHanBotEmailModule）下选项来自外部配置源而不是硬编码默认值。
/// </remarks>
public class XiHanBotEmailServiceCollectionExtensionsTests
{
    /// <summary>
    /// 服务集合为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void AddXiHanBotEmail_WhenServicesIsNull_Throws()
    {
        IServiceCollection services = null!;

        var exception = Assert.Throws<ArgumentNullException>(() => { services.AddXiHanBotEmail(); });

        Assert.Equal("services", exception.ParamName);
    }

    /// <summary>
    /// 返回同一个服务集合以支持链式调用
    /// </summary>
    [Fact]
    public void AddXiHanBotEmail_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var returned = services.AddXiHanBotEmail();

        Assert.Same(services, returned);
    }

    /// <summary>
    /// 注册配置存储与提供者，均为单例
    /// </summary>
    [Fact]
    public void AddXiHanBotEmail_RegistersConfigStoreAndProviderAsSingleton()
    {
        var services = new ServiceCollection();

        services.AddXiHanBotEmail();

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IEmailConfigStore) &&
            descriptor.ImplementationType == typeof(DefaultEmailConfigStore) &&
            descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IBotProvider) &&
            descriptor.ImplementationType == typeof(EmailBotProvider) &&
            descriptor.Lifetime == ServiceLifetime.Singleton);
    }

    /// <summary>
    /// 不传配置委托时不写入任何选项配置
    /// </summary>
    [Fact]
    public void AddXiHanBotEmail_WithoutConfigure_DoesNotRegisterOptionsConfiguration()
    {
        var services = new ServiceCollection();

        services.AddXiHanBotEmail();

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IConfigureOptions<EmailOptions>));
    }

    /// <summary>
    /// 传入配置委托时写入选项并可被读取
    /// </summary>
    [Fact]
    public async Task AddXiHanBotEmail_WithConfigure_AppliesOptions()
    {
        var services = new ServiceCollection();

        services.AddXiHanBotEmail(options =>
        {
            options.Enabled = false;
            options.IsBodyHtml = false;
            options.From.SmtpPort = 465;
            options.Bcc.Add("bcc@example.com");
        });

        await using var serviceProvider = services.BuildServiceProvider();
        var options = await serviceProvider.GetRequiredService<IEmailConfigStore>().GetAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(options);
        Assert.False(options.Enabled);
        Assert.False(options.IsBodyHtml);
        Assert.Equal(465, options.From.SmtpPort);
        Assert.Single(options.Bcc);
        Assert.Equal("bcc@example.com", options.Bcc[0]);
    }

    /// <summary>
    /// 注册后能解析出名称为 Email 的提供者
    /// </summary>
    [Fact]
    public async Task AddXiHanBotEmail_ResolvesEmailProvider()
    {
        var services = new ServiceCollection();
        services.AddXiHanBotEmail(_ => { });

        await using var serviceProvider = services.BuildServiceProvider();

        var providers = serviceProvider.GetServices<IBotProvider>().ToList();
        Assert.Single(providers);
        Assert.IsType<EmailBotProvider>(providers[0]);
        Assert.Equal(BotProviderNames.Email, providers[0].Name);
    }

    /// <summary>
    /// 重复调用不会产生重复的提供者注册
    /// </summary>
    [Fact]
    public async Task AddXiHanBotEmail_CalledTwice_DoesNotDuplicateProvider()
    {
        var services = new ServiceCollection();

        services.AddXiHanBotEmail(_ => { });
        services.AddXiHanBotEmail(_ => { });

        await using var serviceProvider = services.BuildServiceProvider();
        Assert.Single(serviceProvider.GetServices<IBotProvider>());
    }

    /// <summary>
    /// 应用层已注册的配置存储不会被默认实现覆盖
    /// </summary>
    [Fact]
    public async Task AddXiHanBotEmail_KeepsPreRegisteredConfigStore()
    {
        var services = new ServiceCollection();
        var custom = new FakeEmailConfigStore(new EmailOptions());
        services.AddSingleton<IEmailConfigStore>(custom);

        services.AddXiHanBotEmail(_ => { });

        await using var serviceProvider = services.BuildServiceProvider();
        Assert.Same(custom, serviceProvider.GetRequiredService<IEmailConfigStore>());
    }

    /// <summary>
    /// 提供者以单例复用，多次解析拿到同一实例
    /// </summary>
    [Fact]
    public async Task AddXiHanBotEmail_ProviderIsSingletonInstance()
    {
        var services = new ServiceCollection();
        services.AddXiHanBotEmail(_ => { });
        await using var serviceProvider = services.BuildServiceProvider();

        var first = serviceProvider.GetServices<IBotProvider>().ToList()[0];
        var second = serviceProvider.GetServices<IBotProvider>().ToList()[0];

        Assert.Same(first, second);
    }
}
