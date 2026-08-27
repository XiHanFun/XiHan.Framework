// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Linq.Expressions;
using System.Reflection;
using XiHan.Framework.EventBus.Abstractions.Distributed;

namespace XiHan.Framework.EventBus.Abstractions.Tests;

/// <summary>
/// 收件箱契约测试
/// </summary>
/// <remarks>
/// 收件箱比发件箱多出一套状态流转：待处理 → 已处理 / 已丢弃，中间可以延迟重试。
/// 抽象包只给出操作集合（<c>MarkAsProcessedAsync</c> / <c>RetryLaterAsync</c> / <c>MarkAsDiscardAsync</c>），
/// 这里用最小内存实现验证这些操作足以表达该状态机，且终态记录不再出现在待处理列表中。
/// 幂等去重靠 <c>ExistsByMessageIdAsync</c>，消息标识而非事件唯一标识才是去重键。
/// </remarks>
public class EventInboxContractTests
{
    /// <summary>
    /// 入队后落在待处理列表
    /// </summary>
    [Fact]
    public async Task GetWaitingEvents_AfterEnqueue_ReturnsEnqueuedEvent()
    {
        var inbox = new InMemoryEventInbox();
        var incoming = EventInfoFactory.CreateIncoming();

        await inbox.EnqueueAsync(incoming);
        var waiting = await inbox.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Same(incoming, Assert.Single(waiting));
    }

    /// <summary>
    /// 标记已处理后退出待处理列表
    /// </summary>
    [Fact]
    public async Task MarkAsProcessed_RemovesEventFromWaitingList()
    {
        var inbox = new InMemoryEventInbox();
        var incoming = EventInfoFactory.CreateIncoming();

        await inbox.EnqueueAsync(incoming);
        await inbox.MarkAsProcessedAsync(incoming.Id);
        var waiting = await inbox.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(waiting);
    }

    /// <summary>
    /// 标记已丢弃同样是终态，不再重投
    /// </summary>
    [Fact]
    public async Task MarkAsDiscard_RemovesEventFromWaitingList()
    {
        var inbox = new InMemoryEventInbox();
        var incoming = EventInfoFactory.CreateIncoming();

        await inbox.EnqueueAsync(incoming);
        await inbox.MarkAsDiscardAsync(incoming.Id);
        var waiting = await inbox.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(waiting);
    }

    /// <summary>
    /// 延迟重试不是终态，事件仍留在待处理列表里
    /// </summary>
    [Fact]
    public async Task RetryLater_KeepsEventWaitingAndRecordsSchedule()
    {
        var inbox = new InMemoryEventInbox();
        var incoming = EventInfoFactory.CreateIncoming();
        var nextRetryTime = EventInfoFactory.FixedCreatedTime.AddMinutes(5);

        await inbox.EnqueueAsync(incoming);
        await inbox.RetryLaterAsync(incoming.Id, 2, nextRetryTime);
        var waiting = await inbox.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Same(incoming, Assert.Single(waiting));

        var entry = Assert.Single(inbox.Entries);
        Assert.Equal(2, entry.RetryCount);
        Assert.Equal(nextRetryTime, entry.NextRetryTime);
    }

    /// <summary>
    /// 下次重试时间可以为空，表示交由实现自行决定重试时机
    /// </summary>
    [Fact]
    public async Task RetryLater_AcceptsNullNextRetryTime()
    {
        var inbox = new InMemoryEventInbox();
        var incoming = EventInfoFactory.CreateIncoming();

        await inbox.EnqueueAsync(incoming);
        await inbox.RetryLaterAsync(incoming.Id, 1, null);

        Assert.Null(Assert.Single(inbox.Entries).NextRetryTime);
    }

    /// <summary>
    /// 去重键是消息标识，同一条消息重复投递可被识别
    /// </summary>
    [Fact]
    public async Task ExistsByMessageId_UsesMessageIdAsDeduplicationKey()
    {
        var inbox = new InMemoryEventInbox();

        await inbox.EnqueueAsync(EventInfoFactory.CreateIncoming("message-1"));

        Assert.True(await inbox.ExistsByMessageIdAsync("message-1"));
        Assert.False(await inbox.ExistsByMessageIdAsync("message-2"));
    }

    /// <summary>
    /// 事件唯一标识不同但消息标识相同时仍判定为重复
    /// </summary>
    /// <remarks>
    /// 事件唯一标识由接收端生成，重复投递时两次生成的值必然不同，只有消息标识能跨投递保持一致。
    /// </remarks>
    [Fact]
    public async Task ExistsByMessageId_IgnoresEventId()
    {
        var inbox = new InMemoryEventInbox();
        var first = EventInfoFactory.CreateIncoming("message-1");
        var second = EventInfoFactory.CreateIncoming("message-1");

        await inbox.EnqueueAsync(first);

        Assert.NotEqual(first.Id, second.Id);
        Assert.True(await inbox.ExistsByMessageIdAsync(second.MessageId));
    }

    /// <summary>
    /// 清理只回收终态记录，待处理记录保留
    /// </summary>
    [Fact]
    public async Task DeleteOldEvents_KeepsWaitingEvents()
    {
        var inbox = new InMemoryEventInbox();
        var processed = EventInfoFactory.CreateIncoming("message-1");
        var waitingEvent = EventInfoFactory.CreateIncoming("message-2");

        await inbox.EnqueueAsync(processed);
        await inbox.EnqueueAsync(waitingEvent);
        await inbox.MarkAsProcessedAsync(processed.Id);
        await inbox.DeleteOldEventsAsync();

        Assert.Same(waitingEvent, Assert.Single(inbox.Entries).Event);
    }

    /// <summary>
    /// 收件箱事件交给事件总线处理时携带收件箱名称
    /// </summary>
    [Fact]
    public async Task ProcessFromInbox_CarriesInboxName()
    {
        var bus = new RecordingEventBoxesBus();
        var incoming = EventInfoFactory.CreateIncoming();

        await bus.ProcessFromInboxAsync(incoming, new InboxConfig("Audit"));

        Assert.Same(incoming, Assert.Single(bus.Processed));
        Assert.Equal("Audit", Assert.Single(bus.InboxNames));
    }

    /// <summary>
    /// 待处理查询的过滤条件写在只读接口上，与发件箱保持一致的形状
    /// </summary>
    [Fact]
    public void EventInbox_GetWaitingEvents_HasOptionalFilterAndToken()
    {
        var method = typeof(IEventInbox).GetMethod(
            nameof(IEventInbox.GetWaitingEventsAsync),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.NotNull(method);

        var parameters = method.GetParameters();

        Assert.Equal(3, parameters.Length);
        Assert.Equal(typeof(Expression<Func<IIncomingEventInfo, bool>>), parameters[1].ParameterType);
        Assert.True(parameters[1].HasDefaultValue);
        Assert.True(parameters[2].HasDefaultValue);
        Assert.Equal(typeof(Task<List<IncomingEventInfo>>), method.ReturnType);
    }

    /// <summary>
    /// 状态流转类操作一律按事件唯一标识定位记录
    /// </summary>
    [Theory]
    [InlineData(nameof(IEventInbox.MarkAsProcessedAsync))]
    [InlineData(nameof(IEventInbox.MarkAsDiscardAsync))]
    public void EventInbox_StateTransitions_AreKeyedByEventId(string methodName)
    {
        var method = typeof(IEventInbox).GetMethod(methodName, [typeof(Guid)]);

        Assert.NotNull(method);
        Assert.Equal(typeof(Task), method.ReturnType);
    }
}
