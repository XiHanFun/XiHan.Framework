// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.Distributed;

namespace XiHan.Framework.EventBus.Tests.Distributed;

/// <summary>
/// 内存事件发件箱测试
/// </summary>
/// <remarks>
/// 发件箱是「先落库再投递」的缓冲区，发送成功后由发送器批量删除，
/// 因此队列语义（先进先出、批量上限、按标识删除）是它唯一的公共契约。
/// </remarks>
public class InMemoryEventOutboxTests
{
    /// <summary>
    /// 入队空事件时抛出参数异常
    /// </summary>
    [Fact]
    public async Task EnqueueAsync_WhenEventNull_Throws()
    {
        var outbox = new InMemoryEventOutbox();

        await Assert.ThrowsAsync<ArgumentNullException>(() => outbox.EnqueueAsync(null!));
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
        var outbox = new InMemoryEventOutbox();
        await outbox.EnqueueAsync(CreateEvent());

        var waiting = await outbox.GetWaitingEventsAsync(maxCount, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(waiting);
    }

    /// <summary>
    /// 入队后的事件处于待发送状态
    /// </summary>
    [Fact]
    public async Task GetWaitingEventsAsync_ReturnsEnqueuedEvents()
    {
        var outbox = new InMemoryEventOutbox();
        var outgoing = CreateEvent();
        await outbox.EnqueueAsync(outgoing);

        var waiting = await outbox.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Same(outgoing, Assert.Single(waiting));
    }

    /// <summary>
    /// 待发送事件按创建时间升序返回并受数量上限约束
    /// </summary>
    [Fact]
    public async Task GetWaitingEventsAsync_OrdersByCreatedTimeAndRespectsMaxCount()
    {
        var outbox = new InMemoryEventOutbox();
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await outbox.EnqueueAsync(CreateEvent("xihan.tests.late", baseTime.AddMinutes(20)));
        await outbox.EnqueueAsync(CreateEvent("xihan.tests.early", baseTime));
        await outbox.EnqueueAsync(CreateEvent("xihan.tests.middle", baseTime.AddMinutes(10)));

        var waiting = await outbox.GetWaitingEventsAsync(2, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(
            new[] { "xihan.tests.early", "xihan.tests.middle" },
            waiting.Select(item => item.EventName));
    }

    /// <summary>
    /// 过滤条件生效
    /// </summary>
    [Fact]
    public async Task GetWaitingEventsAsync_AppliesFilter()
    {
        var outbox = new InMemoryEventOutbox();
        await outbox.EnqueueAsync(CreateEvent("xihan.tests.kept"));
        await outbox.EnqueueAsync(CreateEvent("xihan.tests.filtered"));

        var waiting = await outbox.GetWaitingEventsAsync(
            10,
            item => item.EventName == "xihan.tests.kept",
            TestContext.Current.CancellationToken);

        Assert.Equal("xihan.tests.kept", Assert.Single(waiting).EventName);
    }

    /// <summary>
    /// 取消令牌已取消时立即抛出取消异常
    /// </summary>
    [Fact]
    public async Task GetWaitingEventsAsync_WhenCancelled_Throws()
    {
        var outbox = new InMemoryEventOutbox();
        await outbox.EnqueueAsync(CreateEvent());
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await outbox.GetWaitingEventsAsync(10, cancellationToken: cancellation.Token));
    }

    /// <summary>
    /// 相同标识重复入队只保留一条，避免重复投递
    /// </summary>
    [Fact]
    public async Task EnqueueAsync_WithSameId_KeepsSingleEntry()
    {
        var outbox = new InMemoryEventOutbox();
        var id = Guid.NewGuid();
        await outbox.EnqueueAsync(CreateEvent("xihan.tests.first", id: id));
        await outbox.EnqueueAsync(CreateEvent("xihan.tests.second", id: id));

        var waiting = await outbox.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("xihan.tests.second", Assert.Single(waiting).EventName);
    }

    /// <summary>
    /// 按标识删除已发送事件
    /// </summary>
    [Fact]
    public async Task DeleteAsync_RemovesEvent()
    {
        var outbox = new InMemoryEventOutbox();
        var outgoing = CreateEvent();
        await outbox.EnqueueAsync(outgoing);

        await outbox.DeleteAsync(outgoing.Id);

        Assert.Empty(await outbox.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 删除不存在的标识不抛异常
    /// </summary>
    [Fact]
    public async Task DeleteAsync_WithUnknownId_DoesNotThrow()
    {
        var outbox = new InMemoryEventOutbox();

        await outbox.DeleteAsync(Guid.NewGuid());
    }

    /// <summary>
    /// 批量删除会清掉列出的全部事件并保留其余事件
    /// </summary>
    [Fact]
    public async Task DeleteManyAsync_RemovesOnlyListedEvents()
    {
        var outbox = new InMemoryEventOutbox();
        var first = CreateEvent("xihan.tests.first");
        var second = CreateEvent("xihan.tests.second");
        var kept = CreateEvent("xihan.tests.kept");
        await outbox.EnqueueAsync(first);
        await outbox.EnqueueAsync(second);
        await outbox.EnqueueAsync(kept);

        await outbox.DeleteManyAsync([first.Id, second.Id]);

        var waiting = await outbox.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("xihan.tests.kept", Assert.Single(waiting).EventName);
    }

    /// <summary>
    /// 批量删除对重复标识幂等
    /// </summary>
    [Fact]
    public async Task DeleteManyAsync_WithDuplicateIds_IsIdempotent()
    {
        var outbox = new InMemoryEventOutbox();
        var outgoing = CreateEvent();
        await outbox.EnqueueAsync(outgoing);

        await outbox.DeleteManyAsync([outgoing.Id, outgoing.Id]);

        Assert.Empty(await outbox.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 批量删除的标识集合为空引用时抛出参数异常
    /// </summary>
    [Fact]
    public async Task DeleteManyAsync_WhenIdsNull_Throws()
    {
        var outbox = new InMemoryEventOutbox();

        await Assert.ThrowsAsync<ArgumentNullException>(() => outbox.DeleteManyAsync(null!));
    }

    /// <summary>
    /// 构造用于测试的出站事件
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="createdTime">创建时间</param>
    /// <param name="id">事件标识</param>
    /// <returns>出站事件</returns>
    private static OutgoingEventInfo CreateEvent(
        string eventName = "xihan.tests.outbox",
        DateTime? createdTime = null,
        Guid? id = null)
    {
        return new OutgoingEventInfo(
            id ?? Guid.NewGuid(),
            eventName,
            Encoding.UTF8.GetBytes("{}"),
            createdTime ?? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }
}
