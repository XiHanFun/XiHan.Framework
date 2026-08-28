// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using XiHan.Framework.Bot.Telegram.Abstractions;
using XiHan.Framework.Bot.Telegram.Stores;

namespace XiHan.Framework.Bot.Telegram.Tests.Stores;

/// <summary>
/// <see cref="InMemoryTelegramUpdateDeduplicator"/> 进程内幂等去重器测试
/// </summary>
/// <remarks>
/// 幂等器是「首次占位成功」的语义（TryAdd），不是「查了再写」：
/// Webhook 重发与轮询重复投递可能并发到达，只有真正原子的占位才能保证同一条 Update 只被处理一次。
/// 因此除了常规用例，这里写了真并发用例断言 N 个线程同时占位只有一个成功。
/// 30 分钟的条目 TTL 依赖真实时间，不做等待验证。
/// </remarks>
public class InMemoryTelegramUpdateDeduplicatorTests
{
    /// <summary>
    /// 首次标记成功，重复标记失败
    /// </summary>
    [Fact]
    public async Task TryMarkProcessedAsync_FirstCallSucceedsSecondFails()
    {
        var deduplicator = new InMemoryTelegramUpdateDeduplicator();

        Assert.True(await deduplicator.TryMarkProcessedAsync("main-bot", 1, TestContext.Current.CancellationToken));
        Assert.False(await deduplicator.TryMarkProcessedAsync("main-bot", 1, TestContext.Current.CancellationToken));
        Assert.False(await deduplicator.TryMarkProcessedAsync("main-bot", 1, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 不同 UpdateId 相互独立
    /// </summary>
    [Fact]
    public async Task TryMarkProcessedAsync_DifferentUpdateIdsAreIndependent()
    {
        var deduplicator = new InMemoryTelegramUpdateDeduplicator();

        Assert.True(await deduplicator.TryMarkProcessedAsync("main-bot", 1, TestContext.Current.CancellationToken));
        Assert.True(await deduplicator.TryMarkProcessedAsync("main-bot", 2, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 不同机器人相互独立：同一个 UpdateId 在两个机器人上都算首次
    /// </summary>
    /// <remarks>
    /// Telegram 的 update_id 是按机器人各自递增的，两个机器人撞号是常态，
    /// 键里必须带机器人名称，否则一个机器人会把另一个机器人的消息挡掉。
    /// </remarks>
    [Fact]
    public async Task TryMarkProcessedAsync_DifferentBotsAreIndependent()
    {
        var deduplicator = new InMemoryTelegramUpdateDeduplicator();

        Assert.True(await deduplicator.TryMarkProcessedAsync("bot-a", 1, TestContext.Current.CancellationToken));
        Assert.True(await deduplicator.TryMarkProcessedAsync("bot-b", 1, TestContext.Current.CancellationToken));
        Assert.False(await deduplicator.TryMarkProcessedAsync("bot-a", 1, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 机器人名称按精确匹配区分（大小写不同视为不同机器人）
    /// </summary>
    [Fact]
    public async Task TryMarkProcessedAsync_BotNameIsCaseSensitive()
    {
        var deduplicator = new InMemoryTelegramUpdateDeduplicator();

        Assert.True(await deduplicator.TryMarkProcessedAsync("main-bot", 1, TestContext.Current.CancellationToken));
        Assert.True(await deduplicator.TryMarkProcessedAsync("MAIN-BOT", 1, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 回滚标记后同一条 Update 可以重新被处理（at-least-once）
    /// </summary>
    [Fact]
    public async Task TryUnmarkAsync_AllowsReprocessing()
    {
        var deduplicator = new InMemoryTelegramUpdateDeduplicator();
        Assert.True(await deduplicator.TryMarkProcessedAsync("main-bot", 1, TestContext.Current.CancellationToken));
        Assert.False(await deduplicator.TryMarkProcessedAsync("main-bot", 1, TestContext.Current.CancellationToken));

        await deduplicator.TryUnmarkAsync("main-bot", 1, TestContext.Current.CancellationToken);

        Assert.True(await deduplicator.TryMarkProcessedAsync("main-bot", 1, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 回滚从未标记过的 Update 是空操作，不抛异常
    /// </summary>
    [Fact]
    public async Task TryUnmarkAsync_WhenNeverMarked_IsNoOp()
    {
        var deduplicator = new InMemoryTelegramUpdateDeduplicator();

        await deduplicator.TryUnmarkAsync("main-bot", 999, TestContext.Current.CancellationToken);

        Assert.True(await deduplicator.TryMarkProcessedAsync("main-bot", 999, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 回滚只影响指定的 Update，不牵连其它条目
    /// </summary>
    [Fact]
    public async Task TryUnmarkAsync_OnlyAffectsTargetEntry()
    {
        var deduplicator = new InMemoryTelegramUpdateDeduplicator();
        _ = await deduplicator.TryMarkProcessedAsync("main-bot", 1, TestContext.Current.CancellationToken);
        _ = await deduplicator.TryMarkProcessedAsync("main-bot", 2, TestContext.Current.CancellationToken);

        await deduplicator.TryUnmarkAsync("main-bot", 1, TestContext.Current.CancellationToken);

        Assert.True(await deduplicator.TryMarkProcessedAsync("main-bot", 1, TestContext.Current.CancellationToken));
        Assert.False(await deduplicator.TryMarkProcessedAsync("main-bot", 2, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 并发标记同一条 Update 时有且只有一个线程占位成功
    /// </summary>
    [Fact]
    public async Task TryMarkProcessedAsync_ConcurrentSameUpdate_OnlyOneSucceeds()
    {
        const int threadCount = 64;
        var deduplicator = new InMemoryTelegramUpdateDeduplicator();
        var results = new ConcurrentBag<bool>();

        var tasks = Enumerable.Range(0, threadCount)
            .Select(_ => Task.Run(async () =>
                results.Add(await deduplicator.TryMarkProcessedAsync("main-bot", 42, CancellationToken.None))))
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(threadCount, results.Count);
        Assert.Equal(1, results.Count(x => x));
    }

    /// <summary>
    /// 并发标记不同 Update 时全部成功
    /// </summary>
    [Fact]
    public async Task TryMarkProcessedAsync_ConcurrentDistinctUpdates_AllSucceed()
    {
        const int updateCount = 128;
        var deduplicator = new InMemoryTelegramUpdateDeduplicator();
        var results = new ConcurrentBag<bool>();

        var tasks = Enumerable.Range(0, updateCount)
            .Select(updateId => Task.Run(async () =>
                results.Add(await deduplicator.TryMarkProcessedAsync("main-bot", updateId, CancellationToken.None))))
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(updateCount, results.Count(x => x));
    }

    /// <summary>
    /// 标记与回滚并发执行不抛异常，结束后状态自洽
    /// </summary>
    [Fact]
    public async Task MarkAndUnmark_ConcurrentMix_DoesNotThrow()
    {
        const int iterations = 200;
        var deduplicator = new InMemoryTelegramUpdateDeduplicator();

        var marking = Task.Run(async () =>
        {
            for (var index = 0; index < iterations; index++)
            {
                _ = await deduplicator.TryMarkProcessedAsync("main-bot", index, CancellationToken.None);
            }
        });

        var unmarking = Task.Run(async () =>
        {
            for (var index = 0; index < iterations; index++)
            {
                await deduplicator.TryUnmarkAsync("main-bot", index, CancellationToken.None);
            }
        });

        await Task.WhenAll(marking, unmarking);

        // 结束后再标记一条全新的 Update 仍然按首次处理
        Assert.True(await deduplicator.TryMarkProcessedAsync("main-bot", iterations + 1, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 默认实现挂在 ITelegramUpdateDeduplicator 抽象上，可被分布式实现整体替换
    /// </summary>
    [Fact]
    public void Type_ImplementsDeduplicatorAbstraction()
    {
        Assert.IsAssignableFrom<ITelegramUpdateDeduplicator>(new InMemoryTelegramUpdateDeduplicator());
    }

    /// <summary>
    /// 不传取消令牌时按默认令牌工作
    /// </summary>
    [Fact]
    public async Task TryMarkProcessedAsync_WithoutCancellationToken_Works()
    {
        var deduplicator = new InMemoryTelegramUpdateDeduplicator();

        Assert.True(await deduplicator.TryMarkProcessedAsync("main-bot", 1));
        Assert.False(await deduplicator.TryMarkProcessedAsync("main-bot", 1));
    }
}
