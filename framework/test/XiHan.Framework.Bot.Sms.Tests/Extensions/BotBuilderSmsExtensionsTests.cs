// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Bot.Extensions;
using XiHan.Framework.Bot.Providers;
using XiHan.Framework.Bot.Sms.Abstractions;
using XiHan.Framework.Bot.Sms.Extensions;
using XiHan.Framework.Bot.Sms.Messaging;
using XiHan.Framework.Bot.Sms.Stores;
using XiHan.Framework.Bot.Sms.Tests.Fakes;

namespace XiHan.Framework.Bot.Sms.Tests.Extensions;

/// <summary>
/// <see cref="BotBuilderSmsExtensions"/> BotBuilder 短信扩展测试
/// </summary>
/// <remarks>
/// UseSms 是 Bot 主包对外的启用入口，注册内容必须与 AddXiHanBotSms 完全一致，
/// 且同样是 TryAdd 语义——应用层先注册的数据库配置存储不能被默认空实现顶掉。
/// </remarks>
public class BotBuilderSmsExtensionsTests
{
    /// <summary>
    /// 构建器为 null 时抛 ArgumentNullException
    /// </summary>
    [Fact]
    public void UseSms_WhenBuilderNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => BotBuilderSmsExtensions.UseSms(null!));
    }

    /// <summary>
    /// 返回原构建器本身，支持链式调用
    /// </summary>
    [Fact]
    public void UseSms_ReturnsSameBuilder()
    {
        var builder = new BotBuilder(new ServiceCollection());

        var returned = builder.UseSms();

        Assert.Same(builder, returned);
    }

    /// <summary>
    /// 注册配置存储、网关解析器与短信提供者三项服务
    /// </summary>
    [Fact]
    public void UseSms_RegistersSmsServices()
    {
        var services = new ServiceCollection();

        new BotBuilder(services).UseSms();

        using var provider = services.BuildServiceProvider();
        Assert.IsType<DefaultSmsConfigStore>(provider.GetRequiredService<ISmsConfigStore>());
        Assert.IsType<SmsGatewayResolver>(provider.GetRequiredService<ISmsGatewayResolver>());
        var providers = provider.GetServices<IBotProvider>().ToList();
        Assert.Single(providers);
        Assert.IsType<SmsBotProvider>(providers[0]);
    }

    /// <summary>
    /// 应用层已注册的配置存储不被默认实现覆盖（TryAdd 语义）
    /// </summary>
    [Fact]
    public void UseSms_KeepsPreRegisteredConfigStore()
    {
        var custom = new FakeSmsConfigStore();
        var services = new ServiceCollection();
        services.AddSingleton<ISmsConfigStore>(custom);

        new BotBuilder(services).UseSms();

        using var provider = services.BuildServiceProvider();
        Assert.Same(custom, provider.GetRequiredService<ISmsConfigStore>());
    }

    /// <summary>
    /// 重复启用不会产生重复的短信提供者
    /// </summary>
    [Fact]
    public void UseSms_CalledTwice_RegistersProviderOnce()
    {
        var services = new ServiceCollection();
        var builder = new BotBuilder(services);

        builder.UseSms();
        builder.UseSms();

        using var provider = services.BuildServiceProvider();
        Assert.Single(provider.GetServices<IBotProvider>());
    }

    /// <summary>
    /// UseSms 只注册短信相关服务，不额外拉起 Bot 主包的调度器与策略
    /// </summary>
    /// <remarks>
    /// 主包内核由 AddXiHanBot 负责，UseSms 只做提供者挂载；
    /// 这里锁死注册条目数量，防止后续误把主包注册塞进来造成重复注册。
    /// </remarks>
    [Fact]
    public void UseSms_RegistersExactlyThreeDescriptors()
    {
        var services = new ServiceCollection();

        new BotBuilder(services).UseSms();

        Assert.Equal(3, services.Count);
    }
}
