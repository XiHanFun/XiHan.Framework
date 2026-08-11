// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Caching.Distributed;
using XiHan.Framework.Caching.Distributed.Abstracts;
using XiHan.Framework.Caching.Extensions.DependencyInjection;
using XiHan.Framework.Caching.Options;

namespace XiHan.Framework.Caching.Tests;

/// <summary>
/// 进程内延迟队列回退实现的测试
/// </summary>
/// <remarks>
/// 覆盖延迟队列的到期语义，以及「Redis 未启用时 <see cref="IRedisDelayQueue{T}"/> 仍可解析」这一注册契约。
/// </remarks>
public class InMemoryDelayQueueTests
{
    /// <summary>
    /// 未到期的消息不会被取出，但计入队列总数
    /// </summary>
    [Fact]
    public async Task DequeueDueAsync_ItemNotDue_ReturnsEmptyButStaysQueued()
    {
        var token = TestContext.Current.CancellationToken;
        var queue = new InMemoryDelayQueue<TestMessage>();
        await queue.EnqueueAsync(new TestMessage(1), TimeSpan.FromMinutes(5), token);

        var due = await queue.DequeueDueAsync(10, token);

        Assert.Empty(due);
        Assert.Equal(1, await queue.CountAsync(token));
    }

    /// <summary>
    /// 到期消息取出后即从队列移除，不会被重复领取
    /// </summary>
    [Fact]
    public async Task DequeueDueAsync_ItemDue_IsRemovedAfterDequeue()
    {
        var token = TestContext.Current.CancellationToken;
        var queue = new InMemoryDelayQueue<TestMessage>();
        await queue.EnqueueAsync(new TestMessage(1), TimeSpan.Zero, token);

        var first = await queue.DequeueDueAsync(10, token);
        var second = await queue.DequeueDueAsync(10, token);

        Assert.Equal([1], first.Select(message => message.Id));
        Assert.Empty(second);
        Assert.Equal(0, await queue.CountAsync(token));
    }

    /// <summary>
    /// 取出条数受 count 限制，同一到期时刻按入队先后取出
    /// </summary>
    [Fact]
    public async Task DequeueDueAsync_RespectsCountAndEnqueueOrder()
    {
        var token = TestContext.Current.CancellationToken;
        var queue = new InMemoryDelayQueue<TestMessage>();
        var dueTime = DateTimeOffset.UtcNow.AddSeconds(-1);
        await queue.EnqueueAtAsync(new TestMessage(1), dueTime, token);
        await queue.EnqueueAtAsync(new TestMessage(2), dueTime, token);
        await queue.EnqueueAtAsync(new TestMessage(3), dueTime, token);

        var due = await queue.DequeueDueAsync(2, token);

        Assert.Equal([1, 2], due.Select(message => message.Id));
        Assert.Equal(1, await queue.CountAsync(token));
    }

    /// <summary>
    /// 到期时刻更早的消息先被取出，与入队顺序无关
    /// </summary>
    [Fact]
    public async Task DequeueDueAsync_OrdersByDueTimeBeforeEnqueueOrder()
    {
        var token = TestContext.Current.CancellationToken;
        var queue = new InMemoryDelayQueue<TestMessage>();
        await queue.EnqueueAtAsync(new TestMessage(1), DateTimeOffset.UtcNow.AddSeconds(-1), token);
        await queue.EnqueueAtAsync(new TestMessage(2), DateTimeOffset.UtcNow.AddSeconds(-30), token);

        var due = await queue.DequeueDueAsync(10, token);

        Assert.Equal([2, 1], due.Select(message => message.Id));
    }

    /// <summary>
    /// count 小于等于零时返回空集合且不消费消息
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task DequeueDueAsync_NonPositiveCount_ReturnsEmpty(int count)
    {
        var token = TestContext.Current.CancellationToken;
        var queue = new InMemoryDelayQueue<TestMessage>();
        await queue.EnqueueAsync(new TestMessage(1), TimeSpan.Zero, token);

        var due = await queue.DequeueDueAsync(count, token);

        Assert.Empty(due);
        Assert.Equal(1, await queue.CountAsync(token));
    }

    /// <summary>
    /// Redis 未启用时延迟队列仍可从容器解析，落到进程内回退实现
    /// </summary>
    [Fact]
    public void AddXiHanCaching_RedisDisabled_ResolvesInMemoryDelayQueue()
    {
        var services = new ServiceCollection();
        services.AddXiHanCaching(BuildConfiguration(redisEnabled: false));
        using var provider = services.BuildServiceProvider();

        var queue = provider.GetService<IRedisDelayQueue<TestMessage>>();

        Assert.IsType<InMemoryDelayQueue<TestMessage>>(queue);
    }

    /// <summary>
    /// Redis 启用且配置了连接串时，注册被替换为 Redis 实现
    /// </summary>
    [Fact]
    public void AddXiHanCaching_RedisEnabled_ReplacesWithRedisDelayQueue()
    {
        var services = new ServiceCollection();
        services.AddXiHanCaching(BuildConfiguration(redisEnabled: true));

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(IRedisDelayQueue<>));

        Assert.Equal(typeof(RedisDelayQueue<>), descriptor.ImplementationType);
    }

    private static IConfiguration BuildConfiguration(bool redisEnabled)
    {
        var settings = new Dictionary<string, string?>
        {
            [$"{XiHanRedisCacheOptions.SectionName}:IsEnabled"] = redisEnabled ? "true" : "false"
        };

        if (redisEnabled)
        {
            settings[$"{XiHanRedisCacheOptions.SectionName}:Configuration"] = "localhost:6379";
        }

        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }

    private sealed record TestMessage(long Id);
}
