// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.EventBus.Abstractions.Distributed;

namespace XiHan.Framework.ProviderContractTests.EventBus;

/// <summary>
/// 发件箱提供方契约
/// </summary>
/// <remarks>
/// 派生类实现 <see cref="CreateFixtureAsync"/> 即获得全部用例；用例只经 <see cref="IEventOutbox"/> 观察行为。
/// </remarks>
public abstract class OutboxContract
{
    /// <summary>
    /// 创建本用例专用的夹具
    /// </summary>
    /// <returns>夹具</returns>
    protected abstract Task<IProviderContractFixture<IEventOutbox>> CreateFixtureAsync();

    /// <summary>
    /// 入箱后按创建时间升序取回且内容保真
    /// </summary>
    [Fact]
    public async Task 入箱后按创建时间升序取回且内容保真()
    {
        await using var fixture = await CreateFixtureAsync();
        var outbox = await fixture.CreateClientAsync();
        var baseTime = BaseTime(fixture);
        var later = NewEvent(baseTime.AddSeconds(2), "Order.Paid", [2, 3]);
        var earlier = NewEvent(baseTime.AddSeconds(1), "Order.Created", [1]);
        await outbox.EnqueueAsync(later);
        await outbox.EnqueueAsync(earlier);

        var waiting = await outbox.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal([earlier.Id, later.Id], waiting.Select(item => item.Id));
        Assert.Equal("Order.Created", waiting[0].EventName);
        Assert.Equal([1], waiting[0].EventData);
        Assert.Equal([2, 3], waiting[1].EventData);
    }

    /// <summary>
    /// 取回数量不超过上限
    /// </summary>
    [Fact]
    public async Task 取回数量不超过上限()
    {
        await using var fixture = await CreateFixtureAsync();
        var outbox = await fixture.CreateClientAsync();
        var baseTime = BaseTime(fixture);
        for (var index = 0; index < 3; index++)
        {
            await outbox.EnqueueAsync(NewEvent(baseTime.AddSeconds(index)));
        }

        var waiting = await outbox.GetWaitingEventsAsync(2, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, waiting.Count);
    }

    /// <summary>
    /// 上限非正时返回空且不影响后续取回
    /// </summary>
    [Fact]
    public async Task 上限非正时返回空()
    {
        await using var fixture = await CreateFixtureAsync();
        var outbox = await fixture.CreateClientAsync();
        var info = NewEvent(BaseTime(fixture));
        await outbox.EnqueueAsync(info);

        var none = await outbox.GetWaitingEventsAsync(0, cancellationToken: TestContext.Current.CancellationToken);
        var waiting = await outbox.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(none);
        Assert.Equal([info.Id], waiting.Select(item => item.Id));
    }

    /// <summary>
    /// 单条与批量删除后不再取回
    /// </summary>
    [Fact]
    public async Task 删除后不再取回()
    {
        await using var fixture = await CreateFixtureAsync();
        var outbox = await fixture.CreateClientAsync();
        var baseTime = BaseTime(fixture);
        var first = NewEvent(baseTime.AddSeconds(1));
        var second = NewEvent(baseTime.AddSeconds(2));
        var third = NewEvent(baseTime.AddSeconds(3));
        await outbox.EnqueueAsync(first);
        await outbox.EnqueueAsync(second);
        await outbox.EnqueueAsync(third);
        await outbox.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken);

        await outbox.DeleteAsync(first.Id);
        await outbox.DeleteManyAsync([second.Id]);
        await ContractRequirements.ReleaseClaimsAsync(fixture);
        var waiting = await outbox.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal([third.Id], waiting.Select(item => item.Id));
    }

    /// <summary>
    /// 已取消的令牌抛出取消异常
    /// </summary>
    [Fact]
    public async Task 已取消的令牌抛出取消异常()
    {
        await using var fixture = await CreateFixtureAsync();
        var outbox = await fixture.CreateClientAsync();
        await outbox.EnqueueAsync(NewEvent(BaseTime(fixture)));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => outbox.GetWaitingEventsAsync(10, null, cancellation.Token));
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
        var info = NewEvent(BaseTime(fixture));
        await writer.EnqueueAsync(info);

        var reader = await fixture.CreateClientAsync();
        var waiting = await reader.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal([info.Id], waiting.Select(item => item.Id));
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
            await first.EnqueueAsync(NewEvent(baseTime.AddSeconds(index)));
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
        var info = NewEvent(BaseTime(fixture));
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
            await writer.EnqueueAsync(NewEvent(baseTime.AddSeconds(index)));
        }

        var clients = new List<IEventOutbox>();
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

    private static DateTime BaseTime(IProviderContractFixture<IEventOutbox> fixture)
    {
        return ContractRequirements.TruncateToSeconds(fixture.UtcNow.AddMinutes(-10));
    }

    private static OutgoingEventInfo NewEvent(DateTime createdTime, string eventName = "Order.Created", byte[]? eventData = null)
    {
        return new OutgoingEventInfo(Guid.NewGuid(), eventName, eventData ?? [1], createdTime);
    }
}
