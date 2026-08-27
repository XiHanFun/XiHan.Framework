// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.EventBus.Distributed;
using XiHan.Framework.EventBus.Tests.Fakes;

namespace XiHan.Framework.EventBus.Tests.Distributed;

/// <summary>
/// 分布式事件总线选项与开箱订阅测试
/// </summary>
/// <remarks>
/// 选项默认「不启用任何事件盒」，只有显式配置后发件箱/收件箱才会介入；
/// 选项里声明的处理器则在总线构造时被批量订阅到底层本地事件总线。
/// </remarks>
public class XiHanDistributedEventBusOptionsTests
{
    /// <summary>
    /// 默认不登记处理器、不启用任何事件盒
    /// </summary>
    [Fact]
    public void Options_AreEmptyByDefault()
    {
        var options = new XiHanDistributedEventBusOptions();

        Assert.NotNull(options.Handlers);
        Assert.Empty(options.Handlers);
        Assert.NotNull(options.Outboxes);
        Assert.Equal(0, options.Outboxes.Count);
        Assert.NotNull(options.Inboxes);
        Assert.Equal(0, options.Inboxes.Count);
    }

    /// <summary>
    /// 两个选项实例之间的集合相互独立
    /// </summary>
    [Fact]
    public void Options_AreNotSharedBetweenInstances()
    {
        var first = new XiHanDistributedEventBusOptions();
        var second = new XiHanDistributedEventBusOptions();

        first.Handlers.Add<RecordingDistributedHandler<NamedNoticeEvent>>();
        first.Outboxes.Configure(config => config.ImplementationType = typeof(InMemoryEventOutbox));
        first.Inboxes.Configure(config => config.ImplementationType = typeof(InMemoryEventInbox));

        Assert.Single(first.Handlers);
        Assert.Equal(1, first.Outboxes.Count);
        Assert.Equal(1, first.Inboxes.Count);
        Assert.Empty(second.Handlers);
        Assert.Equal(0, second.Outboxes.Count);
        Assert.Equal(0, second.Inboxes.Count);
    }

    /// <summary>
    /// 未指定名称的事件盒配置落到「Default」这个键上
    /// </summary>
    [Fact]
    public void Configure_WithoutName_UsesDefaultKey()
    {
        var options = new XiHanDistributedEventBusOptions();

        options.Outboxes.Configure(config => config.ImplementationType = typeof(InMemoryEventOutbox));
        options.Inboxes.Configure(config => config.ImplementationType = typeof(InMemoryEventInbox));

        Assert.True(options.Outboxes.ContainsKey("Default"));
        Assert.True(options.Inboxes.ContainsKey("Default"));
        Assert.Equal(typeof(InMemoryEventOutbox), options.Outboxes["Default"].ImplementationType);
        Assert.Equal(typeof(InMemoryEventInbox), options.Inboxes["Default"].ImplementationType);
    }

    /// <summary>
    /// 同名配置多次调用是叠加而不是覆盖整条配置
    /// </summary>
    [Fact]
    public void Configure_CalledTwiceWithSameName_MutatesSameConfig()
    {
        var options = new XiHanDistributedEventBusOptions();

        options.Outboxes.Configure(config => config.ImplementationType = typeof(InMemoryEventOutbox));
        options.Outboxes.Configure(config => config.IsSendingEnabled = false);

        Assert.Equal(1, options.Outboxes.Count);
        var config = options.Outboxes["Default"];
        Assert.Equal(typeof(InMemoryEventOutbox), config.ImplementationType);
        Assert.False(config.IsSendingEnabled);
    }

    /// <summary>
    /// 总线构造时会把选项里声明的分布式处理器订阅进去
    /// </summary>
    [Fact]
    public async Task Ctor_SubscribesHandlersDeclaredInOptions()
    {
        using var harness = LocalDistributedEventBusHarness.Create(
            services => services.AddSingleton<RecordingDistributedHandler<NamedNoticeEvent>>(),
            options => options.Handlers.Add<RecordingDistributedHandler<NamedNoticeEvent>>());

        await harness.Bus.PublishAsync(
            typeof(NamedNoticeEvent),
            new NamedNoticeEvent { Message = "开箱订阅" },
            onUnitOfWorkComplete: false,
            useOutbox: false);

        var handler = harness.Services.GetRequiredService<RecordingDistributedHandler<NamedNoticeEvent>>();
        Assert.Single(handler.Received);
    }

    /// <summary>
    /// 选项里声明的处理器同样登记到底层本地事件总线
    /// </summary>
    [Fact]
    public void Ctor_RegistersDeclaredHandlersOnLocalEventBus()
    {
        using var harness = LocalDistributedEventBusHarness.Create(
            services => services.AddSingleton<RecordingDistributedHandler<NamedNoticeEvent>>(),
            options => options.Handlers.Add<RecordingDistributedHandler<NamedNoticeEvent>>());

        Assert.Single(harness.LocalBus.GetEventHandlerFactories(typeof(NamedNoticeEvent)));
    }
}
