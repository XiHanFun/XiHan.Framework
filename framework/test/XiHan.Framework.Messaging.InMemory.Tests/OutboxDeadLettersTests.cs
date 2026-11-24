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
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Persistence.InMemory;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Messaging.InMemory.Tests;

/// <summary>
/// 发件箱死信的批量重投（RFC-0009「发件箱」，ADR-0272）
/// </summary>
/// <remarks>
/// 逐项结果与入参同序：死信行回到待投递，不存在的行、不在死信里的行与重复的标识各自的结果；空集合不触存储；存储失败、提交失败与取消。以内存持久化存行、可推进的时钟；存储失败用按脚本失败的工作单元与仓储
/// </remarks>
public sealed class OutboxDeadLettersTests
{
    /// <summary>
    /// 主题
    /// </summary>
    private const string Topic = "orders";

    /// <summary>
    /// 时钟起点
    /// </summary>
    private static readonly DateTimeOffset _start = new(2026, 11, 24, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Redrive_PutsDeadLettersBackForDispatch_AndReportsEachItemInInputOrder()
    {
        await using var host = await StartAsync();
        var clock = new FakeTimeProvider(_start.AddHours(1));
        using var logs = new LogCapture();
        var dead = Dead("dead-1", 0, attempts: 3, lastError: "[MESSAGING_UNAVAILABLE_001] The message broker is unavailable: nacked");
        var pending = Row("pending", 1);
        var backingOff = Row("backing-off", 2, attempts: 1, nextAttemptAt: _start.AddDays(1));
        var dispatched = Row("dispatched", 3, attempts: 1, dispatchedAt: _start.AddMinutes(1));
        var deadToo = Dead("dead-2", 4, attempts: 10, lastError: "[MESSAGING_UNAVAILABLE_001] The message broker is unavailable: nacked");
        await SeedAsync(host, dead, pending, backingOff, dispatched, deadToo);
        var missing = Guid.CreateVersion7();
        var deadLetters = new OutboxDeadLetters(host.Services, new OutboxOptions(), clock, logs);

        var results = await deadLetters.RedriveManyAsync([dead.Id, missing, pending.Id, dispatched.Id, deadToo.Id, backingOff.Id], CancellationToken.None);

        // 逐项结果与入参同序：死信行成功，不存在的行、不在死信里的行（待投递、已投递、退避中）各有各的错误码
        Assert.Equal(
            [null, MessagingErrors.OutboxMessageNotFound, MessagingErrors.OutboxNotDeadLettered, MessagingErrors.OutboxNotDeadLettered, null, MessagingErrors.OutboxNotDeadLettered],
            results.Select(static r => r.Error?.Code));
        Assert.Equal(missing.ToString(), results[1].Error?.Details?["id"]);
        Assert.Equal(pending.Id.ToString(), results[2].Error?.Details?["id"]);

        // 死信行回到刚写入时的状态：马上到期、尝试次数从头算；失败原因留着供排查
        var rows = await RowsAsync(host);
        foreach (var id in (string[])["dead-1", "dead-2"])
        {
            Assert.Null(rows[id].DeadLetteredAt);
            Assert.Null(rows[id].DispatchedAt);
            Assert.Equal(clock.GetUtcNow(), rows[id].NextAttemptAt);
            Assert.Equal(0, rows[id].Attempts);
            Assert.StartsWith("[MESSAGING_UNAVAILABLE_001]", rows[id].LastError, StringComparison.Ordinal);
        }

        // 不在死信里的行原样不动
        Assert.Equal(0, rows["pending"].Attempts);
        Assert.Equal(_start, rows["pending"].NextAttemptAt);
        Assert.Equal(1, rows["backing-off"].Attempts);
        Assert.Equal(_start.AddDays(1), rows["backing-off"].NextAttemptAt);
        Assert.Equal(1, rows["dispatched"].Attempts);
        Assert.Equal(_start.AddMinutes(1), rows["dispatched"].DispatchedAt);
        Assert.Equal(
            [(LogLevel.Information, typeof(OutboxDeadLetters).FullName, "Put 2 dead-lettered outbox message(s) back for dispatch out of 6 requested.")],
            logs.Entries.Select(static e => (e.Level, (string?)e.Category, e.Message)));

        // 下一轮投递循环照常发出：与首次投递的消息同一批（按创建时间），退避中的那条还没到期
        var broker = new ScriptedBroker();
        var dispatcher = new OutboxDispatcher(host.Services, broker, new OutboxOptions(), clock, NullLoggerFactory.Instance);
        Assert.Equal(3, await dispatcher.RunOnceAsync(CancellationToken.None));
        Assert.Equal(["dead-1,pending,dead-2"], broker.Calls);
        rows = await RowsAsync(host);
        Assert.Equal(1, rows["dead-1"].Attempts);
        Assert.Equal(clock.GetUtcNow(), rows["dead-1"].DispatchedAt);
        Assert.Null(rows["dead-1"].LastError);
    }

    [Fact]
    public async Task Redrive_RepeatedIds_SeeTheEffectOfTheirFirstOccurrence()
    {
        await using var host = await StartAsync();
        var clock = new FakeTimeProvider(_start.AddHours(1));
        var dead = Dead("dead", 0, attempts: 3);
        var pending = Row("pending", 1);
        await SeedAsync(host, dead, pending);
        var missing = Guid.CreateVersion7();
        var deadLetters = new OutboxDeadLetters(host.Services, new OutboxOptions(), clock, NullLoggerFactory.Instance);

        var results = await deadLetters.RedriveManyAsync([dead.Id, pending.Id, dead.Id, missing, pending.Id, missing, dead.Id], CancellationToken.None);

        // 第一次重投成功之后行已经不在死信里；不在死信里的与不存在的，再来几次结论相同
        Assert.Equal(
            [
                null,
                MessagingErrors.OutboxNotDeadLettered,
                MessagingErrors.OutboxNotDeadLettered,
                MessagingErrors.OutboxMessageNotFound,
                MessagingErrors.OutboxNotDeadLettered,
                MessagingErrors.OutboxMessageNotFound,
                MessagingErrors.OutboxNotDeadLettered,
            ],
            results.Select(static r => r.Error?.Code));
        var row = (await RowsAsync(host))["dead"];
        Assert.Null(row.DeadLetteredAt);
        Assert.Equal(0, row.Attempts);
        Assert.Equal(clock.GetUtcNow(), row.NextAttemptAt);

        // 再重投一遍同一批：都已经不在死信里，什么也不改
        clock.Advance(TimeSpan.FromMinutes(1));
        var again = await deadLetters.RedriveManyAsync([dead.Id], CancellationToken.None);
        Assert.Equal(MessagingErrors.OutboxNotDeadLettered, Assert.Single(again).Error?.Code);
        Assert.Equal(_start.AddHours(1), (await RowsAsync(host))["dead"].NextAttemptAt);
    }

    [Fact]
    public async Task Redrive_OfASingleId_IsTheBatchOfOne()
    {
        await using var host = await StartAsync();
        var clock = new FakeTimeProvider(_start.AddHours(1));
        var dead = Dead("dead", 0, attempts: 3);
        await SeedAsync(host, dead);
        var deadLetters = new OutboxDeadLetters(host.Services, new OutboxOptions(), clock, NullLoggerFactory.Instance);

        var result = Assert.Single(await deadLetters.RedriveManyAsync([dead.Id], CancellationToken.None));

        Assert.True(result.IsSuccess, result.ToString());
        Assert.Null((await RowsAsync(host))["dead"].DeadLetteredAt);
    }

    [Fact]
    public async Task Redrive_OfAnUndecodableRow_SendsItStraightBackToTheDeadLetters()
    {
        await using var host = await StartAsync();
        var clock = new FakeTimeProvider(_start.AddHours(1));
        var broken = Dead("unused", 0, attempts: 1);
        broken.Payload = Encoding.UTF8.GetBytes("not a cloud event");
        await SeedAsync(host, broken);
        var deadLetters = new OutboxDeadLetters(host.Services, new OutboxOptions(), clock, NullLoggerFactory.Instance);
        var dispatcher = new OutboxDispatcher(host.Services, new ScriptedBroker(), new OutboxOptions(), clock, NullLoggerFactory.Instance);

        Assert.True(Assert.Single(await deadLetters.RedriveManyAsync([broken.Id], CancellationToken.None)).IsSuccess);
        Assert.Equal(0, await dispatcher.RunOnceAsync(CancellationToken.None));

        // 载荷解不开的行重投也发不出去：下一轮直接回到死信，不会反复占着批次
        var row = (await RowsAsync(host))["undecodable"];
        Assert.Equal(clock.GetUtcNow(), row.DeadLetteredAt);
        Assert.Equal(1, row.Attempts);
    }

    [Fact]
    public async Task Redrive_OfAnEmptyList_ReturnsEmpty_WithoutTouchingTheStore()
    {
        var unitOfWork = new ScriptedUnitOfWork();
        await using var services = Scripted(unitOfWork, new ScriptedRepository<OutboxMessage, Guid>());
        var deadLetters = new OutboxDeadLetters(services, new OutboxOptions(), new FakeTimeProvider(_start), NullLoggerFactory.Instance);

        Assert.Empty(await deadLetters.RedriveManyAsync([], CancellationToken.None));
        Assert.Empty(unitOfWork.Calls);
        await Assert.ThrowsAsync<ArgumentNullException>("ids", async () => await deadLetters.RedriveManyAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Redrive_WhenTheStoreFails_ThrowsTheStorageException_AfterRollingBack()
    {
        // 数据库断开：读发件箱抛出
        var failure = new TimeoutException("The outbox table cannot be read.");
        var unitOfWork = new ScriptedUnitOfWork();
        await using var services = Scripted(unitOfWork, new ScriptedRepository<OutboxMessage, Guid> { Failure = failure });
        var deadLetters = new OutboxDeadLetters(services, new OutboxOptions(), new FakeTimeProvider(_start), NullLoggerFactory.Instance);

        // 标识没有着落、唯一的库又没能看：说不清它存不存在，整体抛出，不报告成「不存在」
        var thrown = await Assert.ThrowsAsync<TimeoutException>(async () => await deadLetters.RedriveManyAsync([Guid.CreateVersion7()], CancellationToken.None));

        Assert.Same(failure, thrown);
        Assert.Equal(["begin", "rollback", "dispose"], unitOfWork.Calls);
        Assert.False(unitOfWork.Options.IsReadOnly);
    }

    [Fact]
    public async Task Redrive_WhenTheCommitFails_ThrowsItAsAStorageFailure()
    {
        var dead = Dead("dead", 0, attempts: 3);
        var unitOfWork = new ScriptedUnitOfWork { CommitResult = Error.Literal("PERSISTENCE_CONFLICT_001", "Concurrency conflict.") };
        await using var services = Scripted(unitOfWork, new ScriptedRepository<OutboxMessage, Guid> { Found = dead });
        var deadLetters = new OutboxDeadLetters(services, new OutboxOptions(), new FakeTimeProvider(_start), NullLoggerFactory.Instance);

        // 提交返回失败时这个库里的改动已经回滚：不能把那一行报告成已重投
        var thrown = await Assert.ThrowsAsync<FrameworkException>(async () => await deadLetters.RedriveManyAsync([dead.Id], CancellationToken.None));

        Assert.Equal("PERSISTENCE_CONFLICT_001", thrown.Code);
        Assert.Equal(["begin", "commit", "dispose"], unitOfWork.Calls);
    }

    [Fact]
    public async Task Redrive_WhenWritingARowBackFails_ReportsThatFailureForTheItem_AndItsRepeats()
    {
        var dead = Dead("dead", 0, attempts: 3);
        var rejected = Error.Literal("PERSISTENCE_NOTFOUND_001", "The entity does not exist.");
        var unitOfWork = new ScriptedUnitOfWork();
        await using var services = Scripted(unitOfWork, new ScriptedRepository<OutboxMessage, Guid> { Found = dead, WriteResult = rejected });
        var deadLetters = new OutboxDeadLetters(services, new OutboxOptions(), new FakeTimeProvider(_start), NullLoggerFactory.Instance);

        var results = await deadLetters.RedriveManyAsync([dead.Id, dead.Id], CancellationToken.None);

        // 仓储逐项返回的失败原样作为该项的结果；行没有被改动，重复项的结论相同
        Assert.Equal(["PERSISTENCE_NOTFOUND_001", "PERSISTENCE_NOTFOUND_001"], results.Select(static r => r.Error?.Code));
        Assert.Equal(["begin", "commit", "dispose"], unitOfWork.Calls);
    }

    [Fact]
    public async Task Redrive_WhenCancelledBeforehand_Throws_WithoutTouchingTheStore()
    {
        var unitOfWork = new ScriptedUnitOfWork();
        await using var services = Scripted(unitOfWork, new ScriptedRepository<OutboxMessage, Guid>());
        var deadLetters = new OutboxDeadLetters(services, new OutboxOptions(), new FakeTimeProvider(_start), NullLoggerFactory.Instance);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await deadLetters.RedriveManyAsync([Guid.CreateVersion7()], cancelled.Token));

        // 进入存储之前检查取消
        Assert.Empty(unitOfWork.Calls);
    }

    /// <summary>
    /// 启动只有内存持久化与发件箱仓储的宿主
    /// </summary>
    /// <remarks>
    /// 不装配发件箱特性，没有后台循环与本测试争抢
    /// </remarks>
    private static Task<TestHost> StartAsync()
        => TestHost.StartAsync(static b => b.UseFeature<InMemoryPersistenceFeature>(), services: static services => services.AddRepository<OutboxMessage, Guid>());

    /// <summary>
    /// 只有按脚本应答的工作单元与发件箱仓储的容器
    /// </summary>
    private static ServiceProvider Scripted(ScriptedUnitOfWork unitOfWork, ScriptedRepository<OutboxMessage, Guid> repository)
        => new ServiceCollection()
            .AddScoped<IUnitOfWork>(_ => unitOfWork)
            .AddScoped<IRepository<OutboxMessage, Guid>>(_ => repository)
            .BuildServiceProvider();

    /// <summary>
    /// 一条可解码的发件箱行
    /// </summary>
    /// <remarks>
    /// 创建时间按序号递增，决定投递顺序；下一次投递时间缺省是时钟起点
    /// </remarks>
    private static OutboxMessage Row(string id, int order, int attempts = 0, DateTimeOffset? nextAttemptAt = null, DateTimeOffset? dispatchedAt = null) => new()
    {
        Id = Guid.CreateVersion7(),
        Topic = Topic,
        Payload = CloudEventsFormat.Encode(new MessageEnvelope { Id = id, Source = "/tests", Type = "orders.placed" }),
        CreatedAt = _start.AddMilliseconds(order),
        NextAttemptAt = nextAttemptAt ?? _start,
        DispatchedAt = dispatchedAt,
        Attempts = attempts,
    };

    /// <summary>
    /// 一条已经进死信的发件箱行
    /// </summary>
    private static OutboxMessage Dead(string id, int order, int attempts, string? lastError = null)
    {
        var row = Row(id, order, attempts);
        row.DeadLetteredAt = _start.AddMinutes(30);
        row.LastError = lastError;
        return row;
    }

    /// <summary>
    /// 在一次调用里写入行
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
    /// 读出全部行，按事件 ID 索引
    /// </summary>
    /// <remarks>
    /// 按载荷解码取事件 ID，解不开的记作 <c>undecodable</c>
    /// </remarks>
    private static async Task<Dictionary<string, OutboxMessage>> RowsAsync(TestHost host)
    {
        IReadOnlyList<OutboxMessage> rows = [];
        (await host.InvokeAsync(async ctx => rows = await ctx.Services.GetRequiredService<IRepository<OutboxMessage, Guid>>().ListAsync(EntityQuery.All<OutboxMessage>(), ctx.CancellationToken))).ThrowIfFailure();
        return rows.ToDictionary(static r => CloudEventsFormat.Decode(r.Payload).TryGetValue(out var envelope) ? envelope.Id : "undecodable", StringComparer.Ordinal);
    }
}
