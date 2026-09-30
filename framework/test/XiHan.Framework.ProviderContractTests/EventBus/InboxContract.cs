// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.EventBus.Abstractions.Distributed;

namespace XiHan.Framework.ProviderContractTests.EventBus;

/// <summary>
/// 收件箱提供方契约
/// </summary>
/// <remarks>
/// 派生类实现 <see cref="CreateFixtureAsync"/> 即获得全部用例；用例只经 <see cref="IEventInbox"/> 观察行为。
/// </remarks>
public abstract class InboxContract
{
    /// <summary>
    /// 创建本用例专用的夹具
    /// </summary>
    /// <returns>夹具</returns>
    protected abstract Task<IProviderContractFixture<IEventInbox>> CreateFixtureAsync();

    /// <summary>
    /// 入箱后可取回且按消息标识判定存在
    /// </summary>
    [Fact]
    public async Task 入箱后可取回且按消息标识判定存在()
    {
        await using var fixture = await CreateFixtureAsync();
        var inbox = await fixture.CreateClientAsync();
        var info = NewEvent(BaseTime(fixture), "msg-1");
        await inbox.EnqueueAsync(info);

        var exists = await inbox.ExistsByMessageIdAsync("msg-1");
        var waiting = await inbox.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(exists);
        var item = Assert.Single(waiting);
        Assert.Equal(info.Id, item.Id);
        Assert.Equal("msg-1", item.MessageId);
        Assert.Equal(info.EventName, item.EventName);
    }

    /// <summary>
    /// 未收录或空白的消息标识判定为不存在
    /// </summary>
    [Fact]
    public async Task 未收录或空白的消息标识判定为不存在()
    {
        await using var fixture = await CreateFixtureAsync();
        var inbox = await fixture.CreateClientAsync();
        await inbox.EnqueueAsync(NewEvent(BaseTime(fixture), "msg-1"));

        Assert.False(await inbox.ExistsByMessageIdAsync("msg-missing"));
        Assert.False(await inbox.ExistsByMessageIdAsync(" "));
    }

    /// <summary>
    /// 取回按创建时间升序且不超过上限
    /// </summary>
    [Fact]
    public async Task 取回按创建时间升序且不超过上限()
    {
        await using var fixture = await CreateFixtureAsync();
        var inbox = await fixture.CreateClientAsync();
        var baseTime = BaseTime(fixture);
        var third = NewEvent(baseTime.AddSeconds(3), "msg-3");
        var first = NewEvent(baseTime.AddSeconds(1), "msg-1");
        var second = NewEvent(baseTime.AddSeconds(2), "msg-2");
        await inbox.EnqueueAsync(third);
        await inbox.EnqueueAsync(first);
        await inbox.EnqueueAsync(second);

        var waiting = await inbox.GetWaitingEventsAsync(2, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal([first.Id, second.Id], waiting.Select(item => item.Id));
    }

    /// <summary>
    /// 标记已处理后不再取回且仍判定存在
    /// </summary>
    [Fact]
    public async Task 标记已处理后不再取回且仍判定存在()
    {
        await using var fixture = await CreateFixtureAsync();
        var inbox = await fixture.CreateClientAsync();
        var info = NewEvent(BaseTime(fixture), "msg-1");
        await inbox.EnqueueAsync(info);
        await inbox.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken);

        await inbox.MarkAsProcessedAsync(info.Id);
        await ContractRequirements.ReleaseClaimsAsync(fixture);

        Assert.Empty(await inbox.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(await inbox.ExistsByMessageIdAsync("msg-1"));
    }

    /// <summary>
    /// 标记丢弃后不再取回且仍判定存在
    /// </summary>
    [Fact]
    public async Task 标记丢弃后不再取回且仍判定存在()
    {
        await using var fixture = await CreateFixtureAsync();
        var inbox = await fixture.CreateClientAsync();
        var info = NewEvent(BaseTime(fixture), "msg-1");
        await inbox.EnqueueAsync(info);
        await inbox.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken);

        await inbox.MarkAsDiscardAsync(info.Id);
        await ContractRequirements.ReleaseClaimsAsync(fixture);

        Assert.Empty(await inbox.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(await inbox.ExistsByMessageIdAsync("msg-1"));
    }

    /// <summary>
    /// 延后重试的时刻未到时不取回
    /// </summary>
    [Fact]
    public async Task 延后重试的时刻未到时不取回()
    {
        await using var fixture = await CreateFixtureAsync();
        var inbox = await fixture.CreateClientAsync();
        var info = NewEvent(BaseTime(fixture), "msg-1");
        await inbox.EnqueueAsync(info);
        await inbox.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken);

        await inbox.RetryLaterAsync(info.Id, 1, fixture.UtcNow.AddHours(1));
        await ContractRequirements.ReleaseClaimsAsync(fixture);

        Assert.Empty(await inbox.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 延后重试的时刻已过时重新取回
    /// </summary>
    [Fact]
    public async Task 延后重试的时刻已过时重新取回()
    {
        await using var fixture = await CreateFixtureAsync();
        var inbox = await fixture.CreateClientAsync();
        var info = NewEvent(BaseTime(fixture), "msg-1");
        await inbox.EnqueueAsync(info);
        await inbox.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken);

        await inbox.RetryLaterAsync(info.Id, 1, fixture.UtcNow.AddMinutes(-1));
        await ContractRequirements.ReleaseClaimsAsync(fixture);
        var waiting = await inbox.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal([info.Id], waiting.Select(item => item.Id));
    }

    /// <summary>
    /// 清理过期事件不删除待处理的事件
    /// </summary>
    [Fact]
    public async Task 清理不删除待处理的事件()
    {
        await using var fixture = await CreateFixtureAsync();
        var inbox = await fixture.CreateClientAsync();
        var info = NewEvent(BaseTime(fixture), "msg-1");
        await inbox.EnqueueAsync(info);

        await inbox.DeleteOldEventsAsync();
        var waiting = await inbox.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal([info.Id], waiting.Select(item => item.Id));
    }

    /// <summary>
    /// 已取消的令牌抛出取消异常
    /// </summary>
    [Fact]
    public async Task 已取消的令牌抛出取消异常()
    {
        await using var fixture = await CreateFixtureAsync();
        var inbox = await fixture.CreateClientAsync();
        await inbox.EnqueueAsync(NewEvent(BaseTime(fixture), "msg-1"));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => inbox.GetWaitingEventsAsync(10, null, cancellation.Token));
    }

    /// <summary>
    /// 同一消息标识重复入箱只保留一条且不抛异常
    /// </summary>
    [Fact]
    public async Task 同一消息标识重复入箱只保留一条()
    {
        await using var fixture = await CreateFixtureAsync();
        ContractRequirements.Require(fixture.Capabilities, ProviderCapabilities.Deduplication);
        var inbox = await fixture.CreateClientAsync();
        var baseTime = BaseTime(fixture);

        await inbox.EnqueueAsync(NewEvent(baseTime.AddSeconds(1), "msg-dup"));
        await inbox.EnqueueAsync(NewEvent(baseTime.AddSeconds(2), "msg-dup"));
        var waiting = await inbox.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(waiting);
    }

    /// <summary>
    /// 已处理的事件不会被延后重试回退为待处理
    /// </summary>
    [Fact]
    public async Task 已处理的事件不会被延后重试回退()
    {
        await using var fixture = await CreateFixtureAsync();
        ContractRequirements.Require(fixture.Capabilities, ProviderCapabilities.FinalStateProtection);
        var inbox = await fixture.CreateClientAsync();
        var info = NewEvent(BaseTime(fixture), "msg-1");
        await inbox.EnqueueAsync(info);
        await inbox.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken);
        await inbox.MarkAsProcessedAsync(info.Id);

        await inbox.RetryLaterAsync(info.Id, 1, fixture.UtcNow.AddMinutes(-1));
        await ContractRequirements.ReleaseClaimsAsync(fixture);

        Assert.Empty(await inbox.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 新建的客户端读得到其他客户端写入的事件
    /// </summary>
    [Fact]
    public async Task 新建客户端读得到其他客户端写入的事件()
    {
        await using var fixture = await CreateFixtureAsync();
        ContractRequirements.Require(fixture.Capabilities, ProviderCapabilities.Persistence);
        var writer = await fixture.CreateClientAsync();
        var info = NewEvent(BaseTime(fixture), "msg-1");
        await writer.EnqueueAsync(info);

        var reader = await fixture.CreateClientAsync();

        Assert.True(await reader.ExistsByMessageIdAsync("msg-1"));
        Assert.Equal([info.Id], (await reader.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken)).Select(item => item.Id));
    }

    /// <summary>
    /// 一个客户端领取的事件不会被其他客户端领到
    /// </summary>
    [Fact]
    public async Task 一个客户端领取的事件不会被其他客户端领到()
    {
        await using var fixture = await CreateFixtureAsync();
        ContractRequirements.Require(fixture.Capabilities, ProviderCapabilities.ExclusiveClaim);
        var first = await fixture.CreateClientAsync();
        var second = await fixture.CreateClientAsync();
        var baseTime = BaseTime(fixture);
        for (var index = 0; index < 3; index++)
        {
            await first.EnqueueAsync(NewEvent(baseTime.AddSeconds(index), $"msg-{index}"));
        }

        var claimed = await first.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken);
        var stolen = await second.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(3, claimed.Count);
        Assert.Empty(stolen);
    }

    /// <summary>
    /// 领取过期后其他客户端可重新领取
    /// </summary>
    [Fact]
    public async Task 领取过期后其他客户端可重新领取()
    {
        await using var fixture = await CreateFixtureAsync();
        ContractRequirements.Require(fixture.Capabilities, ProviderCapabilities.ExclusiveClaim | ProviderCapabilities.ClaimExpiry);
        var first = await fixture.CreateClientAsync();
        var second = await fixture.CreateClientAsync();
        var info = NewEvent(BaseTime(fixture), "msg-1");
        await first.EnqueueAsync(info);
        await first.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken);

        await fixture.ExpireClaimsAsync();
        var reclaimed = await second.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal([info.Id], reclaimed.Select(item => item.Id));
    }

    /// <summary>
    /// 多个客户端并发领取时事件不重复且全部领到
    /// </summary>
    [Fact]
    public async Task 并发领取时事件不重复且全部领到()
    {
        const int eventCount = 120;
        const int workerCount = 6;

        await using var fixture = await CreateFixtureAsync();
        ContractRequirements.Require(fixture.Capabilities, ProviderCapabilities.ConcurrentStorage | ProviderCapabilities.ExclusiveClaim);
        var writer = await fixture.CreateClientAsync();
        var baseTime = BaseTime(fixture);
        for (var index = 0; index < eventCount; index++)
        {
            await writer.EnqueueAsync(NewEvent(baseTime.AddSeconds(index), $"msg-{index}"));
        }

        var clients = new List<IEventInbox>();
        for (var index = 0; index < workerCount; index++)
        {
            clients.Add(await fixture.CreateClientAsync());
        }

        var cancellationToken = TestContext.Current.CancellationToken;
        var workers = clients.Select(client => Task.Run(async () =>
        {
            var claimedIds = new List<Guid>();
            for (var round = 0; round <= eventCount; round++)
            {
                var batch = await client.GetWaitingEventsAsync(10, cancellationToken: cancellationToken);
                if (batch.Count == 0)
                {
                    return claimedIds;
                }

                claimedIds.AddRange(batch.Select(item => item.Id));
            }

            Assert.Fail("并发领取未在预期轮数内取空，提供方可能没有标记已领取的记录。");
            return claimedIds;
        }, cancellationToken));

        var claimed = (await Task.WhenAll(workers)).SelectMany(item => item).ToList();

        Assert.Distinct(claimed);
        Assert.Equal(eventCount, claimed.Count);
    }

    private static DateTime BaseTime(IProviderContractFixture<IEventInbox> fixture)
    {
        return ContractRequirements.TruncateToSeconds(fixture.UtcNow.AddMinutes(-10));
    }

    private static IncomingEventInfo NewEvent(DateTime createdTime, string messageId)
    {
        return new IncomingEventInfo(Guid.NewGuid(), messageId, "Order.Created", [1], createdTime);
    }
}
