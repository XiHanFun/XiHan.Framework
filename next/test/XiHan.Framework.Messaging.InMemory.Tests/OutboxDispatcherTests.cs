// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.CloudEvents;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Outbox;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.InMemory;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Messaging.InMemory.Tests;

/// <summary>
/// 发件箱投递循环的尝试上限、退避、死信与保留期（RFC-0009「发件箱」，ADR-0064）。
/// </summary>
/// <remarks>
/// 代理抛出与返回失败同样计入见 ADR-0108。以内存持久化存行、脚本代理给结果、可推进的时钟逐轮调用 <see cref="OutboxDispatcher.RunOnceAsync"/>，不起后台循环、不等真实时间。行用载荷里的事件 ID 区分。
/// </remarks>
public sealed class OutboxDispatcherTests
{
    /// <summary>
    /// 主题。
    /// </summary>
    private const string Topic = "orders";

    /// <summary>
    /// 第二个主题。
    /// </summary>
    private const string ShipmentsTopic = "shipments";

    /// <summary>
    /// 时钟起点。
    /// </summary>
    private static readonly DateTimeOffset _start = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PermanentFailures_AreDeadLettered_WithBackoff_AndDoNotBlockLaterMessages()
    {
        await using var host = await StartAsync();
        var clock = new FakeTimeProvider(_start);
        var broker = new ScriptedBroker { Outcome = static e => e.Id == "fail" ? Error.Unavailable(MessagingErrors.BrokerUnavailable, MessagingMessages.BrokerUnavailable, MessageArguments.Of(("reason", "nacked"))) : Result.Success() };
        var options = new OutboxOptions { BatchSize = 2, MaxAttempts = 3, RetryDelay = TimeSpan.FromSeconds(10), MaxRetryDelay = TimeSpan.FromSeconds(15), DispatchedRetention = TimeSpan.FromHours(1) };
        var dispatcher = new OutboxDispatcher(host.Services, broker, options, clock, NullLoggerFactory.Instance);
        await SeedAsync(host, Row("undecodable", Encoding.UTF8.GetBytes("not a cloud event"), 0), Row("fail", 1), Row("ok-1", 2), Row("ok-2", 3));

        // 第 1 轮取到前两条：无法解码的直接进死信，发布失败的推后 10 秒
        Assert.Equal(0, await dispatcher.RunOnceAsync(CancellationToken.None));
        var rows = await RowsAsync(host);
        Assert.Equal(_start, rows["undecodable"].DeadLetteredAt);
        Assert.Equal(MessagingErrors.InvalidEnvelope, ErrorCodeOf(rows["undecodable"]));
        Assert.Equal(1, rows["fail"].Attempts);
        Assert.Null(rows["fail"].DeadLetteredAt);
        Assert.Equal(_start.AddSeconds(10), rows["fail"].NextAttemptAt);

        // 第 2 轮（时钟未动）：死信与退避中的不占批次，后面两条照常投递
        Assert.Equal(2, await dispatcher.RunOnceAsync(CancellationToken.None));
        rows = await RowsAsync(host);
        Assert.Equal(_start, rows["ok-1"].DispatchedAt);
        Assert.Equal(_start, rows["ok-2"].DispatchedAt);
        Assert.Equal(["fail", "ok-1", "ok-2"], broker.Published);

        // 退避到期才重试：翻倍后受上限 15 秒约束
        clock.Advance(TimeSpan.FromSeconds(9));
        Assert.Equal(0, await dispatcher.RunOnceAsync(CancellationToken.None));
        Assert.Equal(3, broker.Published.Count);
        clock.Advance(TimeSpan.FromSeconds(1));
        await dispatcher.RunOnceAsync(CancellationToken.None);
        rows = await RowsAsync(host);
        Assert.Equal(2, rows["fail"].Attempts);
        Assert.Equal(clock.GetUtcNow().AddSeconds(15), rows["fail"].NextAttemptAt);

        // 第 3 次仍失败即用尽：进死信，此后不再发布
        clock.Advance(TimeSpan.FromSeconds(15));
        await dispatcher.RunOnceAsync(CancellationToken.None);
        clock.Advance(TimeSpan.FromMinutes(10));
        await dispatcher.RunOnceAsync(CancellationToken.None);
        rows = await RowsAsync(host);
        Assert.Equal(3, rows["fail"].Attempts);
        Assert.Equal(_start.AddSeconds(25), rows["fail"].DeadLetteredAt);
        Assert.Equal(MessagingErrors.BrokerUnavailable, ErrorCodeOf(rows["fail"]));
        Assert.Equal(["fail", "ok-1", "ok-2", "fail", "fail"], broker.Published);

        // 超过保留期后已投递的行被删，死信行留着
        clock.Advance(TimeSpan.FromHours(1));
        await dispatcher.RunOnceAsync(CancellationToken.None);
        rows = await RowsAsync(host);
        Assert.Equal(["fail", "undecodable"], rows.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task DispatchedRows_AreDeletedRightAway_WhenTheRetentionIsZero_AndPurgedInBatches()
    {
        await using var host = await StartAsync();
        var clock = new FakeTimeProvider(_start);
        var broker = new ScriptedBroker();
        // 先按 1 小时保留投递 3 条，再改成 0：新投递的当场删除，旧的已投递行每轮最多删一批
        var keep = new OutboxDispatcher(host.Services, broker, new OutboxOptions { BatchSize = 2, DispatchedRetention = TimeSpan.FromHours(1) }, clock, NullLoggerFactory.Instance);
        await SeedAsync(host, Row("a", 0), Row("b", 1), Row("c", 2));
        Assert.Equal(2, await keep.RunOnceAsync(CancellationToken.None));
        Assert.Equal(1, await keep.RunOnceAsync(CancellationToken.None));
        Assert.Equal(3, (await RowsAsync(host)).Count);

        var drop = new OutboxDispatcher(host.Services, broker, new OutboxOptions { BatchSize = 2, DispatchedRetention = TimeSpan.Zero }, clock, NullLoggerFactory.Instance);
        // 列加在已有数据的表上时旧行的 NextAttemptAt 为空：视为已到期，照样取到
        var legacy = Row("d", 3);
        legacy.NextAttemptAt = null;
        await SeedAsync(host, legacy);
        Assert.Equal(1, await drop.RunOnceAsync(CancellationToken.None));
        var rows = await RowsAsync(host);
        Assert.DoesNotContain("d", rows.Keys);
        Assert.Single(rows);
        Assert.Equal(0, await drop.RunOnceAsync(CancellationToken.None));
        Assert.Empty(await RowsAsync(host));
        Assert.Equal(["a", "b", "c", "d"], broker.Published);
    }

    [Fact]
    public async Task ThrowingMessage_UnderTheDispatchLoop_IsDeadLettered_AndTheRestOfItsBatchIsDispatched()
    {
        await using var host = await StartAsync();
        var clock = new FakeTimeProvider(_start);
        var logs = new LogCapture();
        var broker = new ScriptedBroker { Throws = static e => e.Id == "poison" ? new FrameworkException(MessagingErrors.BrokerUnavailable, "Message 'poison' exceeds the broker's size limit.") : null };
        var options = new OutboxOptions { MaxAttempts = 3, RetryDelay = TimeSpan.FromSeconds(10), MaxRetryDelay = TimeSpan.FromSeconds(15) };
        var dispatcher = new OutboxDispatcher(host.Services, broker, options, clock, logs);
        // 毒消息与两条好消息同一主题、同一批，另一个主题的一条排在后面
        await SeedAsync(host, Row("ok-1", 0), Row("poison", 1), Row("ok-2", 2), Row("shipped", ShipmentsTopic, 3));

        // 照 RunAsync 的做法逐轮投递：单轮异常记下后继续，每轮之间时钟走一个轮询间隔；两分钟远超退避的总和（10 + 15 秒）
        var failedRounds = 0;
        for (var round = 0; round < 120; round++)
        {
            try
            {
                await dispatcher.RunOnceAsync(CancellationToken.None);
            }
            catch (FrameworkException)
            {
                failedRounds++;
            }

            clock.Advance(options.PollInterval);
        }

        // 毒消息按与失败结果相同的规则用尽尝试进死信；同一批的好消息与排在后面的消息都已发出，没有一轮整体失败。
        // 先整体比一次，失败信息里一眼看全（毒消息尝试次数、毒消息进死信、ok-1 / ok-2 / shipped 已发出、整体失败的轮数）：
        // 修复前是 (0, False, False, False, False, 120)——毒消息从不计次、同批与排在后面的都被拖住、每轮都回滚
        var rows = await RowsAsync(host);
        Assert.Equal(
            (3, true, true, true, true, 0),
            (rows["poison"].Attempts, rows["poison"].DeadLetteredAt is not null, rows["ok-1"].DispatchedAt is not null, rows["ok-2"].DispatchedAt is not null, rows["shipped"].DispatchedAt is not null, failedRounds));
        Assert.Null(rows["poison"].DispatchedAt);
        Assert.Equal(MessagingErrors.BrokerUnavailable, ErrorCodeOf(rows["poison"]));
        Assert.Equal(3, broker.Calls.Count(static c => c.Split(',').Contains("poison")));

        // 每次抛出记一条告警（不带载荷），进死信记一条错误
        var entries = logs.Entries.Where(static e => e.Category == typeof(OutboxDispatcher).FullName).ToList();
        Assert.Equal(3, entries.Count(static e => e.Level == LogLevel.Warning && e.Message.Contains("threw", StringComparison.Ordinal)));
        Assert.Single(entries, static e => e.Level == LogLevel.Error && e.Message.Contains("dead-lettered", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UnreachableBroker_CountsAnAttemptOnlyForTheCallThatThrew_AndDefersTheRestOfTheRound()
    {
        await using var host = await StartAsync();
        var clock = new FakeTimeProvider(_start);
        var broker = new ScriptedBroker { Unreachable = true };
        var dispatcher = new OutboxDispatcher(host.Services, broker, new OutboxOptions { MaxAttempts = 3, RetryDelay = TimeSpan.FromSeconds(5) }, clock, NullLoggerFactory.Instance);
        await SeedAsync(host, Row("a", 0), Row("b", ShipmentsTopic, 1));

        // 代理不可达时每轮只有一次调用抛出：它的消息计一次尝试并退避，本轮余下的不发布、不计尝试
        Assert.Equal(0, await dispatcher.RunOnceAsync(CancellationToken.None));
        var rows = await RowsAsync(host);
        Assert.Equal(1, rows["a"].Attempts);
        Assert.Equal(_start.AddSeconds(5), rows["a"].NextAttemptAt);
        Assert.Equal(0, rows["b"].Attempts);
        Assert.Equal(["a"], broker.Calls);

        Assert.Equal(0, await dispatcher.RunOnceAsync(CancellationToken.None));
        Assert.Equal(0, await dispatcher.RunOnceAsync(CancellationToken.None));
        rows = await RowsAsync(host);
        Assert.Equal(1, rows["b"].Attempts);
        Assert.Equal(["a", "b"], broker.Calls);

        // 代理恢复后退避到期的照常投递，尝试次数没有用尽
        broker.Unreachable = false;
        clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(2, await dispatcher.RunOnceAsync(CancellationToken.None));
        rows = await RowsAsync(host);
        Assert.All(rows.Values, static row => Assert.Equal(2, row.Attempts));
        Assert.All(rows.Values, static row => Assert.Null(row.DeadLetteredAt));
        Assert.All(rows.Values, row => Assert.Equal(clock.GetUtcNow(), row.DispatchedAt));
    }

    [Fact]
    public async Task ThrownExceptions_AreRecordedWithoutTheirMessage_UnlessTheyAreFrameworkExceptions()
    {
        await using var host = await StartAsync();
        var clock = new FakeTimeProvider(_start);
        // 框架异常的消息是框架自己写的、照常记下；别的异常的消息可能带连接串、主机名等，只记类型名
        var broker = new ScriptedBroker
        {
            Throws = static e => e.Id switch
            {
                "framework" => new FrameworkException(MessagingErrors.BrokerUnavailable, "Failed to publish to topic 'orders': the broker closed the channel."),
                "foreign" => new InvalidOperationException("Host=db.internal;Password=hunter2"),
                _ => null,
            },
        };
        var dispatcher = new OutboxDispatcher(host.Services, broker, new OutboxOptions { MaxAttempts = 1 }, clock, NullLoggerFactory.Instance);
        await SeedAsync(host, Row("framework", 0), Row("foreign", ShipmentsTopic, 1));

        // 第一轮第一条抛出、第二条留到下一轮
        await dispatcher.RunOnceAsync(CancellationToken.None);
        await dispatcher.RunOnceAsync(CancellationToken.None);
        var rows = await RowsAsync(host);
        Assert.All(rows.Values, static row => Assert.NotNull(row.DeadLetteredAt));
        Assert.All(rows.Values, static row => Assert.Equal(MessagingErrors.BrokerUnavailable, ErrorCodeOf(row)));
        Assert.Contains("the broker closed the channel", rows["framework"].LastError, StringComparison.Ordinal);
        Assert.Contains(typeof(InvalidOperationException).FullName!, rows["foreign"].LastError, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", rows["foreign"].LastError, StringComparison.Ordinal);
    }

    [Fact]
    public void Options_RetryDelayDoublesUpToTheCap()
    {
        var options = new OutboxOptions { RetryDelay = TimeSpan.FromSeconds(5), MaxRetryDelay = TimeSpan.FromSeconds(30) };

        Assert.Equal(TimeSpan.FromSeconds(5), options.RetryDelayAfter(1));
        Assert.Equal(TimeSpan.FromSeconds(10), options.RetryDelayAfter(2));
        Assert.Equal(TimeSpan.FromSeconds(20), options.RetryDelayAfter(3));
        Assert.Equal(TimeSpan.FromSeconds(30), options.RetryDelayAfter(4));
        Assert.Equal(TimeSpan.FromSeconds(30), options.RetryDelayAfter(int.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => options.RetryDelayAfter(0));
    }

    /// <summary>
    /// 启动只有内存持久化与发件箱仓储的宿主。
    /// </summary>
    /// <remarks>
    /// 不装配发件箱特性，没有后台循环与本测试争抢。
    /// </remarks>
    private static Task<TestHost> StartAsync()
        => TestHost.StartAsync(static b => b.UseFeature<InMemoryPersistenceFeature>(), services: static services => services.AddRepository<OutboxMessage, Guid>());

    /// <summary>
    /// 一条可解码的发件箱行。
    /// </summary>
    private static OutboxMessage Row(string id, int order) => Row(id, Topic, order);

    /// <summary>
    /// 一条给定主题、可解码的发件箱行。
    /// </summary>
    private static OutboxMessage Row(string id, string topic, int order)
        => Row(id, CloudEventsFormat.Encode(new MessageEnvelope { Id = id, Source = "/tests", Type = "orders.placed" }), order, topic);

    /// <summary>
    /// 一条给定载荷的发件箱行。
    /// </summary>
    /// <remarks>
    /// 创建时间按序号递增，决定投递顺序。
    /// </remarks>
    private static OutboxMessage Row(string id, byte[] payload, int order, string topic = Topic) => new()
    {
        Id = Guid.CreateVersion7(),
        Topic = topic,
        Payload = payload,
        CreatedAt = _start.AddMilliseconds(order),
        NextAttemptAt = _start,
    };

    /// <summary>
    /// 在一次调用里写入行。
    /// </summary>
    private static async Task SeedAsync(TestHost host, params OutboxMessage[] rows)
        => (await host.InvokeAsync(async ctx =>
        {
            foreach (var added in await ctx.Services.GetRequiredService<IRepository<OutboxMessage, Guid>>().AddRangeAsync(rows, ctx.CancellationToken))
            {
                added.ThrowIfFailure();
            }
        })).ThrowIfFailure();

    /// <summary>
    /// 读出全部行，按事件 ID 索引。
    /// </summary>
    /// <remarks>
    /// 按载荷解码取事件 ID，解不开的记作 <c>undecodable</c>。
    /// </remarks>
    private static async Task<Dictionary<string, OutboxMessage>> RowsAsync(TestHost host)
    {
        IReadOnlyList<OutboxMessage> rows = [];
        (await host.InvokeAsync(async ctx => rows = await ctx.Services.GetRequiredService<IRepository<OutboxMessage, Guid>>().ListAsync(EntityQuery.All<OutboxMessage>(), ctx.CancellationToken))).ThrowIfFailure();
        return rows.ToDictionary(static r => CloudEventsFormat.Decode(r.Payload).TryGetValue(out var envelope) ? envelope.Id : "undecodable", StringComparer.Ordinal);
    }

    /// <summary>
    /// 行里记下的失败原因（<c>[错误码] 文案</c>）的错误码。
    /// </summary>
    private static string? ErrorCodeOf(OutboxMessage row) => row.LastError is { } error && error.StartsWith('[') ? error[1..error.IndexOf(']', StringComparison.Ordinal)] : null;
}
