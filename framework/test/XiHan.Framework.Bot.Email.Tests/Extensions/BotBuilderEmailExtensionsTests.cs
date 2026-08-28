// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.Consts;
using XiHan.Framework.Bot.Email.Abstractions;
using XiHan.Framework.Bot.Email.Extensions;
using XiHan.Framework.Bot.Email.Messaging;
using XiHan.Framework.Bot.Email.Options;
using XiHan.Framework.Bot.Email.Stores;
using XiHan.Framework.Bot.Email.Tests.Fakes;
using XiHan.Framework.Bot.Extensions;
using XiHan.Framework.Bot.Providers;

namespace XiHan.Framework.Bot.Email.Tests.Extensions;

/// <summary>
/// <see cref="BotBuilderEmailExtensions"/> 注册契约测试
/// </summary>
/// <remarks>
/// UseEmail 是子包对外唯一的装配入口，三条契约必须锁死：
/// 参数为空立即抛、TryAdd 语义不覆盖应用层已注册的实现、IBotProvider 以 Enumerable 追加且不重复。
/// </remarks>
public class BotBuilderEmailExtensionsTests
{
    /// <summary>
    /// 构建器为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void UseEmail_WhenBuilderIsNull_Throws()
    {
        BotBuilder builder = null!;

        var exception = Assert.Throws<ArgumentNullException>(() => { builder.UseEmail(_ => { }); });

        Assert.Equal("builder", exception.ParamName);
    }

    /// <summary>
    /// 配置委托为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void UseEmail_WhenConfigureIsNull_Throws()
    {
        var builder = new BotBuilder(new ServiceCollection());

        var exception = Assert.Throws<ArgumentNullException>(() => { builder.UseEmail(null!); });

        Assert.Equal("configure", exception.ParamName);
    }

    /// <summary>
    /// 返回同一个构建器实例以支持链式调用
    /// </summary>
    [Fact]
    public void UseEmail_ReturnsSameBuilder()
    {
        var builder = new BotBuilder(new ServiceCollection());

        var returned = builder.UseEmail(_ => { });

        Assert.Same(builder, returned);
    }

    /// <summary>
    /// 注册邮件提供者与默认配置存储，均为单例
    /// </summary>
    [Fact]
    public void UseEmail_RegistersProviderAndConfigStoreAsSingleton()
    {
        var services = new ServiceCollection();

        new BotBuilder(services).UseEmail(_ => { });

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IBotProvider) &&
            descriptor.ImplementationType == typeof(EmailBotProvider) &&
            descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IEmailConfigStore) &&
            descriptor.ImplementationType == typeof(DefaultEmailConfigStore) &&
            descriptor.Lifetime == ServiceLifetime.Singleton);
    }

    /// <summary>
    /// 配置委托被写入选项系统并可被配置存储读到
    /// </summary>
    [Fact]
    public async Task UseEmail_AppliesConfigureDelegateToOptions()
    {
        var services = new ServiceCollection();
        new BotBuilder(services).UseEmail(options =>
        {
            options.Enabled = false;
            options.From.SmtpHost = "smtp.example.com";
            options.To.Add("to@example.com");
        });
        await using var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredService<IEmailConfigStore>();
        var options = await store.GetAsync(TestContext.Current.CancellationToken);

        Assert.IsType<DefaultEmailConfigStore>(store);
        Assert.NotNull(options);
        Assert.False(options.Enabled);
        Assert.Equal("smtp.example.com", options.From.SmtpHost);
        Assert.Single(options.To);
    }

    /// <summary>
    /// 注册后能从容器解析出名称为 Email 的提供者
    /// </summary>
    [Fact]
    public async Task UseEmail_ResolvesEmailProviderFromContainer()
    {
        var services = new ServiceCollection();
        new BotBuilder(services).UseEmail(_ => { });
        await using var serviceProvider = services.BuildServiceProvider();

        var providers = serviceProvider.GetServices<IBotProvider>().ToList();

        Assert.Single(providers);
        Assert.IsType<EmailBotProvider>(providers[0]);
        Assert.Equal(BotProviderNames.Email, providers[0].Name);
    }

    /// <summary>
    /// 重复调用不会产生重复的提供者注册
    /// </summary>
    /// <remarks>
    /// TryAddEnumerable 按实现类型去重，多个子包重复装配时不能让同一提供者被调度两次。
    /// </remarks>
    [Fact]
    public async Task UseEmail_CalledTwice_DoesNotDuplicateProvider()
    {
        var services = new ServiceCollection();
        var builder = new BotBuilder(services);

        builder.UseEmail(_ => { });
        builder.UseEmail(_ => { });

        await using var serviceProvider = services.BuildServiceProvider();
        Assert.Single(serviceProvider.GetServices<IBotProvider>());
    }

    /// <summary>
    /// 应用层已注册的配置存储不会被默认实现覆盖
    /// </summary>
    /// <remarks>
    /// IEmailConfigStore 用 TryAddSingleton 注册，这正是"应用层可换成数据库实现"的承诺。
    /// </remarks>
    [Fact]
    public async Task UseEmail_KeepsPreRegisteredConfigStore()
    {
        var services = new ServiceCollection();
        var custom = new FakeEmailConfigStore(new EmailOptions { Enabled = false });
        services.AddSingleton<IEmailConfigStore>(custom);

        new BotBuilder(services).UseEmail(_ => { });

        await using var serviceProvider = services.BuildServiceProvider();
        Assert.Same(custom, serviceProvider.GetRequiredService<IEmailConfigStore>());
    }

    /// <summary>
    /// 注册会引入选项系统，IOptionsMonitor 可解析
    /// </summary>
    [Fact]
    public async Task UseEmail_BringsInOptionsInfrastructure()
    {
        var services = new ServiceCollection();

        new BotBuilder(services).UseEmail(options => options.IsBodyHtml = false);

        await using var serviceProvider = services.BuildServiceProvider();
        var monitor = serviceProvider.GetRequiredService<IOptionsMonitor<EmailOptions>>();
        Assert.False(monitor.CurrentValue.IsBodyHtml);
    }
}
