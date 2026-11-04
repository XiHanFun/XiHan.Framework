// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.CloudEvents;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Outbox;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;
using Xunit;

namespace XiHan.Framework.Messaging.Conformance;

/// <summary>
/// 发件箱投递循环在持久化 Provider 上的一致性套件
/// </summary>
/// <remarks>
/// 出处：RFC-0009「发件箱」，ADR-0064、ADR-0108。继承并实现 <see cref="CreateFeature"/>（提供 <c>persistence.relational@1</c> 的持久化特性），全部用例随之执行；内存持久化与数据库 Provider 跑同一套用例，行为一致。套件登记 <see cref="OutboxMessage"/> 的仓储与可推进的时钟，用脚本代理逐轮调用 <see cref="OutboxDispatcher.RunOnceAsync"/>：不装配发件箱特性、不起后台循环、不等真实时间。行按载荷里的事件 ID 区分；每条行单独写入、写入之间推进时钟，创建时间各不相同，投递顺序在任何数据库上都确定
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class OutboxConformance
{
    /// <summary>
    /// 套件使用的主题
    /// </summary>
    public const string OrdersTopic = "conformance.outbox.orders";

    /// <summary>
    /// 套件使用的第二个主题
    /// </summary>
    public const string ShipmentsTopic = "conformance.outbox.shipments";

    /// <summary>
    /// 时钟起点
    /// </summary>
    private static readonly DateTimeOffset _start = new(2026, 11, 3, 8, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 宿主配置
    /// </summary>
    /// <remarks>
    /// Provider 需要的连接与建表开关在此提供
    /// </remarks>
    protected virtual IEnumerable<KeyValuePair<string, string?>> Configuration => [];

    /// <summary>
    /// 创建被测持久化 Provider 的特性
    /// </summary>
    /// <remarks>
    /// 每个用例调用一次
    /// </remarks>
    protected abstract IFeature CreateFeature();

    /// <summary>
    /// 用例 1：一直抛出的消息按退避进死信，同批的其他消息照常发出
    /// </summary>
    /// <remarks>
    /// 代理对某条消息一直抛出：每次计一次尝试、按退避推后、到上限进死信。毒消息与两条好消息同一主题、同一批：首次按主题整批发布，整批抛出时这一批每条各计一次尝试，本轮余下的（另一个主题）留到下一轮；重试逐条单独发布，毒消息之外的随即发出
    /// </remarks>
    [Fact]
    public async Task ThrowingMessage_IsRetriedWithBackoff_AndDeadLettered_WithoutHoldingBackOthers()
    {
        var clock = new FakeTimeProvider(_start);
        await using var host = await StartAsync(clock).ConfigureAwait(false);
        await SeedAsync(host, clock, OrdersTopic, "ok-1").ConfigureAwait(false);
        await SeedAsync(host, clock, OrdersTopic, "poison").ConfigureAwait(false);
        await SeedAsync(host, clock, OrdersTopic, "ok-2").ConfigureAwait(false);
        await SeedAsync(host, clock, ShipmentsTopic, "shipped").ConfigureAwait(false);
        var broker = new ScriptedOutboxBroker(static (_, envelopes) => envelopes.Any(static e => e.Id == "poison")
            ? throw new FrameworkException(MessagingErrors.BrokerUnavailable, "The scripted broker cannot publish 'poison'.")
            : ScriptedOutboxBroker.Succeeded(envelopes));
        var dispatcher = Dispatcher(host, broker, clock, new OutboxOptions { BatchSize = 10, MaxAttempts = 3, RetryDelay = TimeSpan.FromSeconds(10), MaxRetryDelay = TimeSpan.FromSeconds(15), DispatchedRetention = TimeSpan.FromHours(1) });
        var t0 = clock.GetUtcNow();

        // 第 1 轮：订单主题整批抛出，三条各计一次尝试并退避 10 秒；本轮不再发布，发货主题留到下一轮、不计尝试
        Assert.Equal(0, await dispatcher.RunOnceAsync(CancellationToken.None).ConfigureAwait(false));
        var rows = await RowsAsync(host).ConfigureAwait(false);
        foreach (var id in (string[])["ok-1", "poison", "ok-2"])
        {
            Assert.Equal(1, rows[id].Attempts);
            Assert.Equal(t0.AddSeconds(10), rows[id].NextAttemptAt);
            Assert.Null(rows[id].DispatchedAt);
            Assert.Null(rows[id].DeadLetteredAt);
            Assert.Equal(MessagingErrors.BrokerUnavailable, ErrorCodeOf(rows[id]));
        }

        Assert.Equal(0, rows["shipped"].Attempts);
        Assert.Null(rows["shipped"].DispatchedAt);

        // 第 2 轮（时钟未动）：退避中的不占批次，发货主题照常发出
        Assert.Equal(1, await dispatcher.RunOnceAsync(CancellationToken.None).ConfigureAwait(false));
        rows = await RowsAsync(host).ConfigureAwait(false);
        Assert.Equal(t0, rows["shipped"].DispatchedAt);

        // 退避到期：重试逐条发布，排在毒消息前面的发出，毒消息再次抛出（第 2 次，退避翻倍后受上限 15 秒约束），本轮余下的留到下一轮
        clock.Advance(TimeSpan.FromSeconds(10));
        var t1 = clock.GetUtcNow();
        Assert.Equal(1, await dispatcher.RunOnceAsync(CancellationToken.None).ConfigureAwait(false));
        rows = await RowsAsync(host).ConfigureAwait(false);
        Assert.Equal(t1, rows["ok-1"].DispatchedAt);
        Assert.Null(rows["ok-1"].LastError);
        Assert.Equal(2, rows["poison"].Attempts);
        Assert.Equal(t1.AddSeconds(15), rows["poison"].NextAttemptAt);
        Assert.Null(rows["ok-2"].DispatchedAt);
        Assert.Equal(1, rows["ok-2"].Attempts);

        // 同一时刻的下一轮：毒消息在退避中，同批剩下的那条照常发出；再下一轮没有可投递的
        Assert.Equal(1, await dispatcher.RunOnceAsync(CancellationToken.None).ConfigureAwait(false));
        Assert.Equal(0, await dispatcher.RunOnceAsync(CancellationToken.None).ConfigureAwait(false));
        rows = await RowsAsync(host).ConfigureAwait(false);
        Assert.Equal(t1, rows["ok-2"].DispatchedAt);
        Assert.Equal(2, rows["ok-2"].Attempts);

        // 第 3 次仍抛出即用尽：进死信，此后不再发布
        clock.Advance(TimeSpan.FromSeconds(15));
        var t2 = clock.GetUtcNow();
        Assert.Equal(0, await dispatcher.RunOnceAsync(CancellationToken.None).ConfigureAwait(false));
        clock.Advance(TimeSpan.FromMinutes(10));
        Assert.Equal(0, await dispatcher.RunOnceAsync(CancellationToken.None).ConfigureAwait(false));
        rows = await RowsAsync(host).ConfigureAwait(false);
        Assert.Equal(3, rows["poison"].Attempts);
        Assert.Equal(t2, rows["poison"].DeadLetteredAt);
        Assert.Null(rows["poison"].DispatchedAt);
        Assert.Equal(MessagingErrors.BrokerUnavailable, ErrorCodeOf(rows["poison"]));
        Assert.Contains("cannot publish 'poison'", rows["poison"].LastError, StringComparison.Ordinal);
        Assert.Equal(["ok-1,poison,ok-2", "shipped", "ok-1", "poison", "ok-2", "poison"], broker.Calls);
    }

    /// <summary>
    /// 用例 2：代理返回失败与代理抛出遵循同一套尝试上限、退避与死信规则
    /// </summary>
    /// <remarks>
    /// 一条一直被拒（失败结果），一条一直抛出，两条的尝试次数、下一次投递时间与进死信的时间逐轮相同，失败原因都带错误码
    /// </remarks>
    [Fact]
    public async Task FailedAndThrownPublishes_FollowTheSameRetryRules()
    {
        var clock = new FakeTimeProvider(_start);
        await using var host = await StartAsync(clock).ConfigureAwait(false);
        await SeedAsync(host, clock, OrdersTopic, "nacked").ConfigureAwait(false);
        await SeedAsync(host, clock, ShipmentsTopic, "thrown").ConfigureAwait(false);
        var broker = new ScriptedOutboxBroker(static (topic, envelopes) => topic == OrdersTopic
            ? [.. envelopes.Select(static _ => Result.Failure(Error.Unavailable(MessagingErrors.BrokerUnavailable, MessagingMessages.BrokerUnavailable, MessageArguments.Of(("reason", "nacked")))))]
            : throw new FrameworkException(MessagingErrors.BrokerUnavailable, "The scripted broker threw."));
        var dispatcher = Dispatcher(host, broker, clock, new OutboxOptions { MaxAttempts = 3, RetryDelay = TimeSpan.FromSeconds(10), MaxRetryDelay = TimeSpan.FromSeconds(15) });
        var t0 = clock.GetUtcNow();

        Assert.Equal(0, await dispatcher.RunOnceAsync(CancellationToken.None).ConfigureAwait(false));
        await AssertSameStateAsync(host, attempts: 1, nextAttemptAt: t0.AddSeconds(10), deadLetteredAt: null).ConfigureAwait(false);

        clock.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(0, await dispatcher.RunOnceAsync(CancellationToken.None).ConfigureAwait(false));
        await AssertSameStateAsync(host, attempts: 2, nextAttemptAt: t0.AddSeconds(25), deadLetteredAt: null).ConfigureAwait(false);

        clock.Advance(TimeSpan.FromSeconds(15));
        Assert.Equal(0, await dispatcher.RunOnceAsync(CancellationToken.None).ConfigureAwait(false));
        clock.Advance(TimeSpan.FromMinutes(10));
        Assert.Equal(0, await dispatcher.RunOnceAsync(CancellationToken.None).ConfigureAwait(false));
        await AssertSameStateAsync(host, attempts: 3, nextAttemptAt: t0.AddSeconds(25), deadLetteredAt: t0.AddSeconds(25)).ConfigureAwait(false);
        Assert.Equal(["nacked", "thrown", "nacked", "thrown", "nacked", "thrown"], broker.Calls);
    }

    /// <summary>
    /// 用例 3：调用方取消让本轮回滚，不计尝试、不进死信
    /// </summary>
    /// <remarks>
    /// 取消即宿主停止。之后照常投递，本轮已发出的再发一次（至少一次）
    /// </remarks>
    [Fact]
    public async Task Cancellation_RollsTheRoundBack_WithoutCountingAttempts()
    {
        var clock = new FakeTimeProvider(_start);
        await using var host = await StartAsync(clock).ConfigureAwait(false);
        await SeedAsync(host, clock, OrdersTopic, "first").ConfigureAwait(false);
        await SeedAsync(host, clock, ShipmentsTopic, "second").ConfigureAwait(false);
        using var stopping = new CancellationTokenSource();
        var broker = new ScriptedOutboxBroker((topic, envelopes) =>
        {
            if (topic == OrdersTopic)
            {
                return ScriptedOutboxBroker.Succeeded(envelopes);
            }

            // 发到第二个主题时宿主开始停止
            stopping.Cancel();
            throw new OperationCanceledException(stopping.Token);
        });
        var dispatcher = Dispatcher(host, broker, clock, new OutboxOptions { MaxAttempts = 1 });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dispatcher.RunOnceAsync(stopping.Token)).ConfigureAwait(false);
        var rows = await RowsAsync(host).ConfigureAwait(false);
        foreach (var row in rows.Values)
        {
            Assert.Equal(0, row.Attempts);
            Assert.Null(row.DispatchedAt);
            Assert.Null(row.DeadLetteredAt);
            Assert.Null(row.LastError);
        }

        // MaxAttempts = 1 也没有被取消用掉：恢复后两条都发出，第一条是第二次发出
        broker.Respond = static (_, envelopes) => ScriptedOutboxBroker.Succeeded(envelopes);
        Assert.Equal(2, await dispatcher.RunOnceAsync(CancellationToken.None).ConfigureAwait(false));
        rows = await RowsAsync(host).ConfigureAwait(false);
        Assert.All(rows.Values, static row => Assert.Equal(1, row.Attempts));
        Assert.All(rows.Values, row => Assert.Equal(clock.GetUtcNow(), row.DispatchedAt));
        Assert.Equal(["first", "second", "first", "second"], broker.Calls);
    }

    /// <summary>
    /// 用例 4：不是调用方取消引起的取消异常是一次失败的尝试
    /// </summary>
    /// <remarks>
    /// 代理客户端自己的超时也以 <see cref="OperationCanceledException"/> 抛出，令牌没有取消，按失败计入、退避、到上限进死信
    /// </remarks>
    [Fact]
    public async Task OperationCanceled_NotRequestedByTheCaller_CountsAsAFailedAttempt()
    {
        var clock = new FakeTimeProvider(_start);
        await using var host = await StartAsync(clock).ConfigureAwait(false);
        await SeedAsync(host, clock, OrdersTopic, "timed-out").ConfigureAwait(false);
        var broker = new ScriptedOutboxBroker(static (_, _) => throw new OperationCanceledException("The scripted broker timed out."));
        var dispatcher = Dispatcher(host, broker, clock, new OutboxOptions { MaxAttempts = 2, RetryDelay = TimeSpan.FromSeconds(5) });
        var t0 = clock.GetUtcNow();

        Assert.Equal(0, await dispatcher.RunOnceAsync(CancellationToken.None).ConfigureAwait(false));
        var row = (await RowsAsync(host).ConfigureAwait(false))["timed-out"];
        Assert.Equal(1, row.Attempts);
        Assert.Equal(t0.AddSeconds(5), row.NextAttemptAt);
        Assert.Equal(MessagingErrors.BrokerUnavailable, ErrorCodeOf(row));

        clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(0, await dispatcher.RunOnceAsync(CancellationToken.None).ConfigureAwait(false));
        row = (await RowsAsync(host).ConfigureAwait(false))["timed-out"];
        Assert.Equal(2, row.Attempts);
        Assert.Equal(clock.GetUtcNow(), row.DeadLetteredAt);
    }

    /// <summary>
    /// 启动装配了被测 Provider、发件箱仓储与给定时钟的宿主
    /// </summary>
    /// <remarks>
    /// 时钟也是仓储记创建时间用的时间源
    /// </remarks>
    private async Task<TestHost> StartAsync(FakeTimeProvider clock)
        => await TestHost.StartAsync(
            builder => builder.UseFeature(CreateFeature()),
            Configuration,
            services =>
            {
                services.AddSingleton<TimeProvider>(clock);
                services.AddRepository<OutboxMessage, Guid>();
            }).ConfigureAwait(false);

    /// <summary>
    /// 以给定代理、时钟与选项创建投递循环
    /// </summary>
    private static OutboxDispatcher Dispatcher(TestHost host, ScriptedOutboxBroker broker, FakeTimeProvider clock, OutboxOptions options)
        => new(host.Services, broker, options, clock, NullLoggerFactory.Instance);

    /// <summary>
    /// 在一次调用里写入一条可解码的发件箱行
    /// </summary>
    /// <remarks>
    /// 写入前推进时钟 1 毫秒：创建时间（仓储按时钟写入）各不相同，投递顺序确定；下一次投递时间等于创建时间，与发件箱发布者写入的一致
    /// </remarks>
    private static async Task SeedAsync(TestHost host, FakeTimeProvider clock, string topic, string id)
    {
        clock.Advance(TimeSpan.FromMilliseconds(1));
        var now = clock.GetUtcNow();
        var message = new OutboxMessage
        {
            Id = Guid.CreateVersion7(),
            Topic = topic,
            Payload = CloudEventsFormat.Encode(new MessageEnvelope { Id = id, Source = "/conformance", Type = "conformance.outbox" }),
            CreatedAt = now,
            NextAttemptAt = now,
        };
        (await host.InvokeAsync(async ctx =>
        {
            foreach (var added in await ctx.Services.GetRequiredService<IRepository<OutboxMessage, Guid>>().AddRangeAsync([message], ctx.CancellationToken).ConfigureAwait(false))
            {
                added.ThrowIfFailure();
            }
        }).ConfigureAwait(false)).ThrowIfFailure();
    }

    /// <summary>
    /// 读出全部行，按载荷里的事件 ID 索引
    /// </summary>
    private static async Task<Dictionary<string, OutboxMessage>> RowsAsync(TestHost host)
    {
        IReadOnlyList<OutboxMessage> rows = [];
        (await host.InvokeAsync(async ctx => rows = await ctx.Services.GetRequiredService<IRepository<OutboxMessage, Guid>>().ListAsync(EntityQuery.All<OutboxMessage>(), ctx.CancellationToken).ConfigureAwait(false)).ConfigureAwait(false)).ThrowIfFailure();
        return rows.ToDictionary(static r => CloudEventsFormat.Decode(r.Payload).ValueOrThrow().Id, StringComparer.Ordinal);
    }

    /// <summary>
    /// 断言失败结果的那条与抛出的那条处在同一状态
    /// </summary>
    private static async Task AssertSameStateAsync(TestHost host, int attempts, DateTimeOffset nextAttemptAt, DateTimeOffset? deadLetteredAt)
    {
        var rows = await RowsAsync(host).ConfigureAwait(false);
        foreach (var row in (OutboxMessage[])[rows["nacked"], rows["thrown"]])
        {
            Assert.Equal(attempts, row.Attempts);
            Assert.Equal(nextAttemptAt, row.NextAttemptAt);
            Assert.Equal(deadLetteredAt, row.DeadLetteredAt);
            Assert.Null(row.DispatchedAt);
            Assert.Equal(MessagingErrors.BrokerUnavailable, ErrorCodeOf(row));
        }
    }

    /// <summary>
    /// 行里记下的失败原因（<c>[错误码] 文案</c>）的错误码
    /// </summary>
    private static string? ErrorCodeOf(OutboxMessage row)
        => row.LastError is { } error && error.StartsWith('[') ? error[1..error.IndexOf(']', StringComparison.Ordinal)] : null;
}
