// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Extensions.DependencyInjection;
using XiHan.Framework.Messaging.Models;
using XiHan.Framework.Messaging.Options;
using XiHan.Framework.Messaging.Services;

namespace XiHan.Framework.Messaging.Tests;

/// <summary>
/// 消息服务注册扩展测试
/// </summary>
/// <remarks>
/// 该扩展用的是 TryAdd 系列，语义上宣称「可重复调用、不覆盖既有注册」，
/// 这两条只有靠真实 ServiceCollection 才能验证，所以这里全部走真实容器而不是描述符拼装。
/// </remarks>
public class XiHanMessagingServiceCollectionExtensionsTests
{
    /// <summary>
    /// 服务集合为空时抛出参数异常
    /// </summary>
    [Fact]
    public void AddXiHanMessaging_WhenServicesIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            XiHanMessagingServiceCollectionExtensions.AddXiHanMessaging(null!);
        });
    }

    /// <summary>
    /// 返回同一个服务集合以支持链式调用
    /// </summary>
    [Fact]
    public void AddXiHanMessaging_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services.AddXiHanMessaging());
    }

    /// <summary>
    /// 调度器以单例注册为默认实现
    /// </summary>
    [Fact]
    public void AddXiHanMessaging_RegistersDispatcherAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddXiHanMessaging();

        var descriptor = Assert.Single(services.Where(item => item.ServiceType == typeof(IMessageDispatcher)).ToArray());

        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(DefaultMessageDispatcher), descriptor.ImplementationType);
    }

    /// <summary>
    /// 兜底发送器以单例进入发送器集合
    /// </summary>
    [Fact]
    public void AddXiHanMessaging_RegistersFallbackSenderAsEnumerableSingleton()
    {
        var services = new ServiceCollection();
        services.AddXiHanMessaging();

        var descriptor = Assert.Single(services.Where(item => item.ServiceType == typeof(IMessageSender)).ToArray());

        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(NotConfiguredMessageSender), descriptor.ImplementationType);
    }

    /// <summary>
    /// 重复调用不会产生重复注册
    /// </summary>
    /// <remarks>
    /// 兜底发送器走的是 TryAddEnumerable，一旦退化成 AddEnumerable，模块被多次装配就会出现多份兜底实现。
    /// </remarks>
    [Fact]
    public void AddXiHanMessaging_CalledTwice_DoesNotDuplicateRegistrations()
    {
        var services = new ServiceCollection();
        services.AddXiHanMessaging();
        services.AddXiHanMessaging();

        Assert.Single(services.Where(item => item.ServiceType == typeof(IMessageDispatcher)).ToArray());
        Assert.Single(services.Where(item => item.ServiceType == typeof(IMessageSender)).ToArray());
    }

    /// <summary>
    /// 配置回调生效
    /// </summary>
    [Fact]
    public void AddXiHanMessaging_WithConfigure_AppliesOptions()
    {
        var services = new ServiceCollection();
        services.AddXiHanMessaging(options =>
        {
            options.ContinueOnError = false;
            options.ThrowWhenNoSender = true;
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<XiHanMessagingOptions>>().Value;

        Assert.False(options.ContinueOnError);
        Assert.True(options.ThrowWhenNoSender);
    }

    /// <summary>
    /// 不传配置回调时仍能解析出默认配置
    /// </summary>
    [Fact]
    public void AddXiHanMessaging_WithoutConfigure_ResolvesDefaultOptions()
    {
        var services = new ServiceCollection();
        services.AddXiHanMessaging();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<XiHanMessagingOptions>>().Value;

        Assert.True(options.ContinueOnError);
        Assert.False(options.ThrowWhenNoSender);
    }

    /// <summary>
    /// 已存在调度器注册时不被覆盖
    /// </summary>
    [Fact]
    public void AddXiHanMessaging_WhenDispatcherAlreadyRegistered_KeepsExistingImplementation()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMessageDispatcher, FakeMessageDispatcher>();
        services.AddXiHanMessaging();

        using var provider = services.BuildServiceProvider();

        Assert.IsType<FakeMessageDispatcher>(provider.GetRequiredService<IMessageDispatcher>());
    }

    /// <summary>
    /// 默认装配下解析出的调度器返回「未配置发送器」的失败结果
    /// </summary>
    /// <remarks>
    /// 端到端串起注册与分发：只调 AddXiHanMessaging 而不接任何真实通道时，
    /// 业务侧拿到的必须是明确失败，而不是空集合或成功结果。
    /// </remarks>
    [Fact]
    public async Task ResolvedDispatcher_WithoutRealSender_ReturnsNotConfiguredFailure()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddXiHanMessaging();

        using var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IMessageDispatcher>();
        var envelope = new MessageEnvelope
        {
            Channel = "email",
            Recipients = [new MessageRecipient { Address = "a@x.com" }]
        };

        var result = Assert.Single(await dispatcher.DispatchAsync(envelope, TestContext.Current.CancellationToken));

        Assert.False(result.IsSuccess);
        Assert.Contains("未配置发送器", result.ErrorMessage!);
        Assert.Equal(envelope.MessageId, result.MessageId);
        Assert.Equal("a@x.com", result.RecipientAddress);
    }

    /// <summary>
    /// 调度器按单例解析，两次获取拿到同一实例
    /// </summary>
    [Fact]
    public void ResolvedDispatcher_IsSingletonInstance()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddXiHanMessaging();

        using var provider = services.BuildServiceProvider();

        Assert.Same(provider.GetRequiredService<IMessageDispatcher>(), provider.GetRequiredService<IMessageDispatcher>());
    }
}
