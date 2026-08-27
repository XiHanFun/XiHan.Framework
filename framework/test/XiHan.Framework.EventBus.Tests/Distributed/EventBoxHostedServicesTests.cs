// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.Distributed;
using XiHan.Framework.EventBus.Local;
using XiHan.Framework.EventBus.Tests.Fakes;

namespace XiHan.Framework.EventBus.Tests.Distributed;

/// <summary>
/// 事件盒后台服务测试
/// </summary>
/// <remarks>
/// 两个后台服务是「事件盒真正被消费」的唯一驱动力：发件箱发送器负责投递后删除，
/// 收件箱处理器负责处理后标记。用最小轮询间隔起真实的后台循环，靠条件轮询等待而不是固定睡眠，
/// 全程不接触任何消息中间件。
/// </remarks>
public class EventBoxHostedServicesTests
{
    /// <summary>
    /// 发件箱里的待发事件会被投递并删除
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task OutboxSender_SendsWaitingEventsAndDeletesThem()
    {
        var distributedOptions = new XiHanDistributedEventBusOptions();
        distributedOptions.Outboxes.Configure(config => config.ImplementationType = typeof(InMemoryEventOutbox));
        using var provider = BuildProvider(distributedOptions);
        var bus = (RecordingDistributedEventBus)provider.GetRequiredService<IDistributedEventBus>();
        var outbox = provider.GetRequiredService<InMemoryEventOutbox>();
        await outbox.EnqueueAsync(CreateOutgoing());

        using var hostedService = new EventBoxOutboxSenderHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(distributedOptions),
            Microsoft.Extensions.Options.Options.Create(CreateFastProcessingOptions()),
            NullLogger<EventBoxOutboxSenderHostedService>.Instance);

        await hostedService.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await WaitUntilAsync(async () => (await outbox.GetWaitingEventsAsync(10)).Count == 0);
        }
        finally
        {
            await hostedService.StopAsync(CancellationToken.None);
        }

        Assert.False(bus.OutboxPublished.IsEmpty);
    }

    /// <summary>
    /// 未配置发件箱时后台服务不动任何数据
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task OutboxSender_WithoutConfiguredOutbox_LeavesEventsUntouched()
    {
        var distributedOptions = new XiHanDistributedEventBusOptions();
        using var provider = BuildProvider(distributedOptions);
        var bus = (RecordingDistributedEventBus)provider.GetRequiredService<IDistributedEventBus>();
        var outbox = provider.GetRequiredService<InMemoryEventOutbox>();
        await outbox.EnqueueAsync(CreateOutgoing());

        using var hostedService = new EventBoxOutboxSenderHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(distributedOptions),
            Microsoft.Extensions.Options.Options.Create(CreateFastProcessingOptions()),
            NullLogger<EventBoxOutboxSenderHostedService>.Instance);

        await hostedService.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            // 未配置发件箱时循环会立刻返回，多跑几轮足以证明它不会误动数据
            await Task.Delay(600);
        }
        finally
        {
            await hostedService.StopAsync(CancellationToken.None);
        }

        Assert.True(bus.OutboxPublished.IsEmpty);
        Assert.Single(await outbox.GetWaitingEventsAsync(10));
    }

    /// <summary>
    /// 关闭发送开关的发件箱不会被处理
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task OutboxSender_WhenSendingDisabled_LeavesEventsWaiting()
    {
        var distributedOptions = new XiHanDistributedEventBusOptions();
        distributedOptions.Outboxes.Configure(config =>
        {
            config.ImplementationType = typeof(InMemoryEventOutbox);
            config.IsSendingEnabled = false;
        });
        using var provider = BuildProvider(distributedOptions);
        var bus = (RecordingDistributedEventBus)provider.GetRequiredService<IDistributedEventBus>();
        var outbox = provider.GetRequiredService<InMemoryEventOutbox>();
        await outbox.EnqueueAsync(CreateOutgoing());

        using var hostedService = new EventBoxOutboxSenderHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(distributedOptions),
            Microsoft.Extensions.Options.Options.Create(CreateFastProcessingOptions()),
            NullLogger<EventBoxOutboxSenderHostedService>.Instance);

        await hostedService.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await Task.Delay(600);
        }
        finally
        {
            await hostedService.StopAsync(CancellationToken.None);
        }

        Assert.True(bus.OutboxPublished.IsEmpty);
        Assert.Single(await outbox.GetWaitingEventsAsync(10));
    }

    /// <summary>
    /// 收件箱里的待处理事件会被处理并标记完成
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task InboxProcessor_ProcessesWaitingEventsAndMarksThemDone()
    {
        var distributedOptions = new XiHanDistributedEventBusOptions();
        distributedOptions.Inboxes.Configure(config => config.ImplementationType = typeof(InMemoryEventInbox));
        using var provider = BuildProvider(distributedOptions);
        var bus = (RecordingDistributedEventBus)provider.GetRequiredService<IDistributedEventBus>();
        var inbox = provider.GetRequiredService<InMemoryEventInbox>();
        await inbox.EnqueueAsync(CreateIncoming());

        using var hostedService = new EventBoxInboxProcessorHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(distributedOptions),
            Microsoft.Extensions.Options.Options.Create(CreateFastProcessingOptions()),
            NullLogger<EventBoxInboxProcessorHostedService>.Instance);

        await hostedService.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await WaitUntilAsync(async () => (await inbox.GetWaitingEventsAsync(10)).Count == 0);
        }
        finally
        {
            await hostedService.StopAsync(CancellationToken.None);
        }

        Assert.False(bus.InboxProcessed.IsEmpty);
    }

    /// <summary>
    /// 处理失败且重试次数用尽的事件被丢弃，不会无限重投
    /// </summary>
    /// <remarks>
    /// 最大重试次数配成 1，首次失败即达到上限，避免测试依赖真实的重试延迟。
    /// </remarks>
    [Fact(Timeout = 60_000)]
    public async Task InboxProcessor_WhenProcessingKeepsFailing_DiscardsAfterRetryLimit()
    {
        var distributedOptions = new XiHanDistributedEventBusOptions();
        distributedOptions.Inboxes.Configure(config => config.ImplementationType = typeof(InMemoryEventInbox));
        using var provider = BuildProvider(distributedOptions);
        var bus = (RecordingDistributedEventBus)provider.GetRequiredService<IDistributedEventBus>();
        bus.FailInboxProcessing = true;
        var inbox = provider.GetRequiredService<InMemoryEventInbox>();
        var incoming = CreateIncoming();
        await inbox.EnqueueAsync(incoming);

        var processingOptions = CreateFastProcessingOptions();
        processingOptions.MaxInboxRetryCount = 1;
        using var hostedService = new EventBoxInboxProcessorHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(distributedOptions),
            Microsoft.Extensions.Options.Options.Create(processingOptions),
            NullLogger<EventBoxInboxProcessorHostedService>.Instance);

        await hostedService.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await WaitUntilAsync(async () => (await inbox.GetWaitingEventsAsync(10)).Count == 0);
        }
        finally
        {
            await hostedService.StopAsync(CancellationToken.None);
        }

        Assert.False(bus.InboxProcessed.IsEmpty);
        // 被丢弃的事件仍保留在收件箱里，只是不再参与派发，因此消息标识依旧可检出
        Assert.True(await inbox.ExistsByMessageIdAsync(incoming.MessageId));
    }

    /// <summary>
    /// 关闭处理开关的收件箱不会被处理
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task InboxProcessor_WhenProcessingDisabled_LeavesEventsWaiting()
    {
        var distributedOptions = new XiHanDistributedEventBusOptions();
        distributedOptions.Inboxes.Configure(config =>
        {
            config.ImplementationType = typeof(InMemoryEventInbox);
            config.IsProcessingEnabled = false;
        });
        using var provider = BuildProvider(distributedOptions);
        var bus = (RecordingDistributedEventBus)provider.GetRequiredService<IDistributedEventBus>();
        var inbox = provider.GetRequiredService<InMemoryEventInbox>();
        await inbox.EnqueueAsync(CreateIncoming());

        using var hostedService = new EventBoxInboxProcessorHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(distributedOptions),
            Microsoft.Extensions.Options.Options.Create(CreateFastProcessingOptions()),
            NullLogger<EventBoxInboxProcessorHostedService>.Instance);

        await hostedService.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await Task.Delay(600);
        }
        finally
        {
            await hostedService.StopAsync(CancellationToken.None);
        }

        Assert.True(bus.InboxProcessed.IsEmpty);
        Assert.Single(await inbox.GetWaitingEventsAsync(10));
    }

    /// <summary>
    /// 构造带记录型事件总线与内存事件盒的服务提供器
    /// </summary>
    /// <param name="distributedOptions">分布式事件总线选项</param>
    /// <returns>服务提供器</returns>
    private static ServiceProvider BuildProvider(XiHanDistributedEventBusOptions distributedOptions)
    {
        var services = new ServiceCollection();
        services.AddSingleton<InMemoryEventOutbox>();
        services.AddSingleton<InMemoryEventInbox>();
        services.AddSingleton<IDistributedEventBus>(serviceProvider => new RecordingDistributedEventBus(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            new FakeCurrentTenant(),
            new FakeUnitOfWorkManager(),
            Microsoft.Extensions.Options.Options.Create(distributedOptions),
            new StubGuidGenerator(),
            new StubClock(),
            new EventHandlerInvoker(),
            NullLocalEventBus.Instance,
            new FakeCorrelationIdProvider()));

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 构造尽可能快的轮询配置
    /// </summary>
    /// <returns>后台处理配置</returns>
    private static EventBoxProcessingOptions CreateFastProcessingOptions()
    {
        // 实现内部会把轮询间隔下钳到 200 毫秒，这里给 1 即取最快节奏
        return new EventBoxProcessingOptions
        {
            PollingIntervalMilliseconds = 1,
            OutboxBatchSize = 10,
            InboxBatchSize = 10,
            InboxRetryDelaySeconds = 1
        };
    }

    /// <summary>
    /// 轮询等待条件成立
    /// </summary>
    /// <param name="condition">判定条件</param>
    /// <returns>表示异步操作的任务</returns>
    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        Assert.Fail("等待后台服务完成处理超时。");
    }

    /// <summary>
    /// 构造用于测试的出站事件
    /// </summary>
    /// <returns>出站事件</returns>
    private static OutgoingEventInfo CreateOutgoing()
    {
        return new OutgoingEventInfo(
            Guid.NewGuid(),
            NamedNoticeEvent.DeclaredEventName,
            Encoding.UTF8.GetBytes("{}"),
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    /// <summary>
    /// 构造用于测试的入站事件
    /// </summary>
    /// <returns>入站事件</returns>
    private static IncomingEventInfo CreateIncoming()
    {
        return new IncomingEventInfo(
            Guid.NewGuid(),
            "message-" + Guid.NewGuid().ToString("N"),
            NamedNoticeEvent.DeclaredEventName,
            Encoding.UTF8.GetBytes("{}"),
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }
}
