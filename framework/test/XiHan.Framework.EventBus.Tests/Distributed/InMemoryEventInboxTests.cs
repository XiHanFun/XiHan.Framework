// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.Distributed;

namespace XiHan.Framework.EventBus.Tests.Distributed;

/// <summary>
/// 内存事件收件箱测试
/// </summary>
/// <remarks>
/// 收件箱承担幂等去重与失败重试两件事：待处理集合必须随状态迁移正确进出，
/// 消息去重按 <c>MessageId</c> 严格比较（不能大小写不敏感，否则会把不同消息当成重复丢弃）。
/// </remarks>
public class InMemoryEventInboxTests
{
    /// <summary>
    /// 入队空事件时抛出参数异常
    /// </summary>
    [Fact]
    public async Task EnqueueAsync_WhenEventNull_Throws()
    {
        var inbox = new InMemoryEventInbox();

        await Assert.ThrowsAsync<ArgumentNullException>(() => inbox.EnqueueAsync(null!));
    }

    /// <summary>
    /// 请求数量非正时直接返回空集合
    /// </summary>
    /// <param name="maxCount">最大数量</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetWaitingEventsAsync_WhenMaxCountNotPositive_ReturnsEmpty(int maxCount)
    {
        var inbox = new InMemoryEventInbox();
        await inbox.EnqueueAsync(CreateEvent("message-1"));

        var waiting = await inbox.GetWaitingEventsAsync(maxCount, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(waiting);
    }

    /// <summary>
    /// 入队后的事件处于待处理状态
    /// </summary>
    [Fact]
    public async Task GetWaitingEventsAsync_ReturnsEnqueuedEvents()
    {
        var inbox = new InMemoryEventInbox();
        var incoming = CreateEvent("message-1");
        await inbox.EnqueueAsync(incoming);

        var waiting = await inbox.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Same(incoming, Assert.Single(waiting));
    }

    /// <summary>
    /// 待处理事件按创建时间升序返回并受数量上限约束
    /// </summary>
    [Fact]
    public async Task GetWaitingEventsAsync_OrdersByCreatedTimeAndRespectsMaxCount()
    {
        var inbox = new InMemoryEventInbox();
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await inbox.EnqueueAsync(CreateEvent("late", createdTime: baseTime.AddMinutes(20)));
        await inbox.EnqueueAsync(CreateEvent("early", createdTime: baseTime));
        await inbox.EnqueueAsync(CreateEvent("middle", createdTime: baseTime.AddMinutes(10)));

        var waiting = await inbox.GetWaitingEventsAsync(2, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "early", "middle" }, waiting.Select(item => item.MessageId));
    }

    /// <summary>
    /// 过滤条件生效
    /// </summary>
    [Fact]
    public async Task GetWaitingEventsAsync_AppliesFilter()
    {
        var inbox = new InMemoryEventInbox();
        await inbox.EnqueueAsync(CreateEvent("message-1", "xihan.tests.kept"));
        await inbox.EnqueueAsync(CreateEvent("message-2", "xihan.tests.filtered"));

        var waiting = await inbox.GetWaitingEventsAsync(
            10,
            item => item.EventName == "xihan.tests.kept",
            TestContext.Current.CancellationToken);

        Assert.Equal("message-1", Assert.Single(waiting).MessageId);
    }

    /// <summary>
    /// 取消令牌已取消时立即抛出取消异常
    /// </summary>
    [Fact]
    public async Task GetWaitingEventsAsync_WhenCancelled_Throws()
    {
        var inbox = new InMemoryEventInbox();
        await inbox.EnqueueAsync(CreateEvent("message-1"));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await inbox.GetWaitingEventsAsync(10, cancellationToken: cancellation.Token));
    }

    /// <summary>
    /// 标记为已处理后不再出现在待处理集合
    /// </summary>
    [Fact]
    public async Task MarkAsProcessedAsync_RemovesEventFromWaitingSet()
    {
        var inbox = new InMemoryEventInbox();
        var incoming = CreateEvent("message-1");
        await inbox.EnqueueAsync(incoming);

        await inbox.MarkAsProcessedAsync(incoming.Id);

        Assert.Empty(await inbox.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 标记为丢弃后不再出现在待处理集合
    /// </summary>
    [Fact]
    public async Task MarkAsDiscardAsync_RemovesEventFromWaitingSet()
    {
        var inbox = new InMemoryEventInbox();
        var incoming = CreateEvent("message-1");
        await inbox.EnqueueAsync(incoming);

        await inbox.MarkAsDiscardAsync(incoming.Id);

        Assert.Empty(await inbox.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 安排到将来重试的事件在到点前不参与派发
    /// </summary>
    [Fact]
    public async Task RetryLaterAsync_WithFutureTime_HidesEventUntilDue()
    {
        var inbox = new InMemoryEventInbox();
        var incoming = CreateEvent("message-1");
        await inbox.EnqueueAsync(incoming);

        await inbox.RetryLaterAsync(incoming.Id, 1, DateTime.UtcNow.AddHours(1));

        Assert.Empty(await inbox.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 重试时间已过的事件重新回到待处理集合
    /// </summary>
    [Fact]
    public async Task RetryLaterAsync_WithElapsedTime_RequeuesEvent()
    {
        var inbox = new InMemoryEventInbox();
        var incoming = CreateEvent("message-1");
        await inbox.EnqueueAsync(incoming);
        await inbox.MarkAsProcessedAsync(incoming.Id);

        await inbox.RetryLaterAsync(incoming.Id, 1, DateTime.UtcNow.AddHours(-1));

        Assert.Same(incoming, Assert.Single(await inbox.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// 未指定重试时间时按「立即可重试」处理
    /// </summary>
    [Fact]
    public async Task RetryLaterAsync_WithoutNextRetryTime_RequeuesImmediately()
    {
        var inbox = new InMemoryEventInbox();
        var incoming = CreateEvent("message-1");
        await inbox.EnqueueAsync(incoming);
        await inbox.MarkAsDiscardAsync(incoming.Id);

        await inbox.RetryLaterAsync(incoming.Id, 2, null);

        Assert.Single(await inbox.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 对不存在的事件做状态迁移不抛异常
    /// </summary>
    [Fact]
    public async Task StateTransitions_WithUnknownId_DoNotThrow()
    {
        var inbox = new InMemoryEventInbox();
        var unknownId = Guid.NewGuid();

        await inbox.MarkAsProcessedAsync(unknownId);
        await inbox.MarkAsDiscardAsync(unknownId);
        await inbox.RetryLaterAsync(unknownId, 1, null);
    }

    /// <summary>
    /// 已入队的消息标识可被检出
    /// </summary>
    [Fact]
    public async Task ExistsByMessageIdAsync_WhenEnqueued_ReturnsTrue()
    {
        var inbox = new InMemoryEventInbox();
        await inbox.EnqueueAsync(CreateEvent("message-1"));

        Assert.True(await inbox.ExistsByMessageIdAsync("message-1"));
    }

    /// <summary>
    /// 未入队的消息标识检不出
    /// </summary>
    [Fact]
    public async Task ExistsByMessageIdAsync_WhenUnknown_ReturnsFalse()
    {
        var inbox = new InMemoryEventInbox();
        await inbox.EnqueueAsync(CreateEvent("message-1"));

        Assert.False(await inbox.ExistsByMessageIdAsync("message-2"));
    }

    /// <summary>
    /// 消息标识按序数严格比较，大小写不同即视为不同消息
    /// </summary>
    [Fact]
    public async Task ExistsByMessageIdAsync_IsCaseSensitive()
    {
        var inbox = new InMemoryEventInbox();
        await inbox.EnqueueAsync(CreateEvent("Message-1"));

        Assert.False(await inbox.ExistsByMessageIdAsync("message-1"));
    }

    /// <summary>
    /// 空白消息标识不参与去重
    /// </summary>
    /// <param name="messageId">消息标识</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExistsByMessageIdAsync_WhenMessageIdBlank_ReturnsFalse(string messageId)
    {
        var inbox = new InMemoryEventInbox();
        await inbox.EnqueueAsync(CreateEvent(messageId));

        Assert.False(await inbox.ExistsByMessageIdAsync(messageId));
    }

    /// <summary>
    /// 已处理事件在保留期内不会被清理
    /// </summary>
    [Fact]
    public async Task DeleteOldEventsAsync_KeepsRecentlyProcessedEvents()
    {
        var inbox = new InMemoryEventInbox();
        var incoming = CreateEvent("message-1");
        await inbox.EnqueueAsync(incoming);
        await inbox.MarkAsProcessedAsync(incoming.Id);

        await inbox.DeleteOldEventsAsync();

        Assert.True(await inbox.ExistsByMessageIdAsync("message-1"));
    }

    /// <summary>
    /// 待处理事件永远不会被清理任务带走
    /// </summary>
    [Fact]
    public async Task DeleteOldEventsAsync_NeverRemovesWaitingEvents()
    {
        var inbox = new InMemoryEventInbox();
        await inbox.EnqueueAsync(CreateEvent("message-1"));

        await inbox.DeleteOldEventsAsync();

        Assert.Single(await inbox.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 构造用于测试的入站事件
    /// </summary>
    /// <param name="messageId">消息标识</param>
    /// <param name="eventName">事件名称</param>
    /// <param name="createdTime">创建时间</param>
    /// <returns>入站事件</returns>
    private static IncomingEventInfo CreateEvent(
        string messageId,
        string eventName = "xihan.tests.inbox",
        DateTime? createdTime = null)
    {
        return new IncomingEventInfo(
            Guid.NewGuid(),
            messageId,
            eventName,
            Encoding.UTF8.GetBytes("{}"),
            createdTime ?? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }
}
