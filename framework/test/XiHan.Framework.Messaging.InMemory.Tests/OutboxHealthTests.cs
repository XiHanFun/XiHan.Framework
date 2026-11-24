// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Kernel.Hosting;
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
/// 健康项 <c>messaging.outbox.backlog</c>（ADR-0272）
/// </summary>
/// <remarks>
/// 装配内存持久化、内存消息与发件箱特性的宿主，时钟可推进：未投递的行数、最老一条的等待时间与死信行数各自超过阈值时 Degraded 并带对应的错误码，回到阈值之内恢复 Ready，条目的数据是最近一次数出的数字。用例写入的未投递行都在退避中（下一次投递时间在很久以后），后台的投递循环不会把它们发走；每次断言之前显式探一次，不等探测周期
/// </remarks>
public sealed class OutboxHealthTests
{
    /// <summary>
    /// 时钟起点
    /// </summary>
    private static readonly DateTimeOffset _start = new(2026, 11, 24, 8, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 等待条件成立的上限
    /// </summary>
    private static readonly TimeSpan _patience = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task EmptyOutbox_IsReady_AndReportsZeroes()
    {
        var clock = new FakeTimeProvider(_start);
        await using var host = await StartAsync(clock);

        // 启动时已经同步探过一次：宿主就绪时条目就是真实状态
        var status = Status(host);

        Assert.Equal(LifecycleState.Ready, status.State);
        Assert.Null(status.Error);
        Assert.Equal(_start, status.CheckedAt);
        Assert.Equal(0L, status.Data["pending"]);
        Assert.Null(status.Data["oldestPendingSeconds"]);
        Assert.Equal(0L, status.Data["deadLettered"]);
        Assert.Equal(1, status.Data["databases"]);
        using var feature = new OutboxFeature();
        Assert.Contains(OutboxFeature.HealthName, feature.Manifest.Contributes?.Health ?? []);
    }

    [Fact]
    public async Task PendingRowsOverTheThreshold_Degrade_AndGoingBackUnderItRecovers()
    {
        var clock = new FakeTimeProvider(_start);
        await using var host = await StartAsync(clock, ("HealthMaxPending", "2"));
        var rows = new[] { Pending("a"), Pending("b"), Pending("c") };
        await SeedAsync(host, rows[0], rows[1]);

        // 恰好等于阈值不算超过
        var atLimit = await ProbeAsync(host);
        Assert.Equal(LifecycleState.Ready, atLimit.State);
        Assert.Equal(2L, atLimit.Data["pending"]);

        await SeedAsync(host, rows[2]);
        var over = await ProbeAsync(host);
        Assert.Equal(LifecycleState.Degraded, over.State);
        Assert.Equal(MessagingErrors.OutboxBacklog, over.Error?.Code);
        Assert.Equal(MessagingMessages.OutboxBacklog, over.Error?.MessageKey);
        Assert.Equal(3L, over.Error?.Details?["pending"]);
        Assert.Equal(2, over.Error?.Details?["maxPending"]);
        Assert.Equal(3L, over.Data["pending"]);
        // 条目降级不影响应用接收调用
        Assert.True(host.App.Health().Ready);

        await RemoveAsync(host, rows[2]);
        Assert.Equal(LifecycleState.Ready, (await ProbeAsync(host)).State);
    }

    [Fact]
    public async Task OldestPendingRowWaitingTooLong_Degrades()
    {
        var clock = new FakeTimeProvider(_start);
        await using var host = await StartAsync(clock, ("HealthMaxPendingAgeSeconds", "60"));
        await SeedAsync(host, Pending("stuck"));

        clock.Advance(TimeSpan.FromSeconds(60));
        var atLimit = await ProbeAsync(host);
        Assert.Equal(LifecycleState.Ready, atLimit.State);
        Assert.Equal(60L, atLimit.Data["oldestPendingSeconds"]);

        // 只有一条、远没到条数阈值：等得太久同样是积压
        clock.Advance(TimeSpan.FromSeconds(1));
        var over = await ProbeAsync(host);
        Assert.Equal(LifecycleState.Degraded, over.State);
        Assert.Equal(MessagingErrors.OutboxBacklog, over.Error?.Code);
        Assert.Equal(61L, over.Error?.Details?["oldestSeconds"]);
        Assert.Equal(60L, over.Error?.Details?["maxOldestSeconds"]);
        Assert.Equal(1L, over.Data["pending"]);
        Assert.Equal(61L, over.Data["oldestPendingSeconds"]);
    }

    [Fact]
    public async Task DeadLetters_DegradeByDefault_AndRedrivingThemRecovers()
    {
        var clock = new FakeTimeProvider(_start);
        await using var host = await StartAsync(clock);
        var dead = DeadLettered("dead");
        await SeedAsync(host, dead);

        // 缺省阈值是 0：有死信就降级
        var degraded = await ProbeAsync(host);
        Assert.Equal(LifecycleState.Degraded, degraded.State);
        Assert.Equal(MessagingErrors.OutboxDeadLettered, degraded.Error?.Code);
        Assert.Equal(MessagingMessages.OutboxDeadLettered, degraded.Error?.MessageKey);
        Assert.Equal(1L, degraded.Error?.Details?["deadLettered"]);
        Assert.Equal(0, degraded.Error?.Details?["maxDeadLettered"]);
        Assert.Equal(1L, degraded.Data["deadLettered"]);
        Assert.Equal(0L, degraded.Data["pending"]);

        // 重投入口与健康项来自同一个特性：重投之后行不再是死信，投递循环的下一轮把它发出
        var results = await host.Services.GetRequiredService<OutboxDeadLetters>().RedriveManyAsync([dead.Id], CancellationToken.None);
        Assert.True(Assert.Single(results).IsSuccess);
        clock.Advance(TimeSpan.FromSeconds(1));
        await WaitUntilAsync(async () => (await RowsAsync(host)).Single().DispatchedAt is not null);

        var recovered = await ProbeAsync(host);
        Assert.Equal(LifecycleState.Ready, recovered.State);
        Assert.Equal(0L, recovered.Data["deadLettered"]);
        Assert.Equal(0L, recovered.Data["pending"]);
    }

    [Fact]
    public async Task DeadLetters_UpToTheConfiguredThreshold_AreTolerated()
    {
        var clock = new FakeTimeProvider(_start);
        await using var host = await StartAsync(clock, ("HealthMaxDeadLettered", "1"));
        await SeedAsync(host, DeadLettered("first"));

        var atLimit = await ProbeAsync(host);
        Assert.Equal(LifecycleState.Ready, atLimit.State);
        Assert.Equal(1L, atLimit.Data["deadLettered"]);

        await SeedAsync(host, DeadLettered("second"));
        var over = await ProbeAsync(host);
        Assert.Equal(LifecycleState.Degraded, over.State);
        Assert.Equal(MessagingErrors.OutboxDeadLettered, over.Error?.Code);
        Assert.Equal(2L, over.Error?.Details?["deadLettered"]);
        Assert.Equal(1, over.Error?.Details?["maxDeadLettered"]);
    }

    [Fact]
    public async Task BacklogAndDeadLettersBothOverTheirThresholds_ReportTheBacklog_AndKeepBothNumbers()
    {
        var clock = new FakeTimeProvider(_start);
        await using var host = await StartAsync(clock, ("HealthMaxPending", "0"));
        await SeedAsync(host, Pending("waiting"), DeadLettered("dead"));

        var status = await ProbeAsync(host);

        // 消息正在发不出去比已经进了死信更急：错误报告积压，死信的数字仍在数据里
        Assert.Equal(LifecycleState.Degraded, status.State);
        Assert.Equal(MessagingErrors.OutboxBacklog, status.Error?.Code);
        Assert.Equal(1L, status.Data["pending"]);
        Assert.Equal(1L, status.Data["deadLettered"]);
    }

    [Fact]
    public async Task DispatchedRows_AreNeitherPendingNorDeadLettered()
    {
        var clock = new FakeTimeProvider(_start);
        await using var host = await StartAsync(clock, ("HealthMaxPending", "0"));
        var dispatched = Pending("done");
        dispatched.DispatchedAt = _start;
        await SeedAsync(host, dispatched);

        var status = await ProbeAsync(host);

        // 保留期内的已投递行不算积压
        Assert.Equal(LifecycleState.Ready, status.State);
        Assert.Equal(0L, status.Data["pending"]);
        Assert.Null(status.Data["oldestPendingSeconds"]);
    }

    /// <summary>
    /// 启动装配了内存持久化、内存消息与发件箱特性的宿主
    /// </summary>
    /// <remarks>
    /// 时钟同时驱动投递循环的轮询、健康项的探测周期与仓储记的时间；<paramref name="settings"/> 是 <c>XiHan:Messaging:Outbox</c> 下的键值。探测间隔配成一天：用例推进时钟时不会触发后台的周期探测，条目只随显式的探测变化
    /// </remarks>
    private static Task<TestHost> StartAsync(FakeTimeProvider clock, params (string Key, string Value)[] settings)
        => TestHost.StartAsync(
            static b => b.UseFeature<InMemoryPersistenceFeature>().UseFeature<InMemoryMessagingFeature>().UseFeature<OutboxFeature>(),
            [
                new($"{OutboxOptions.ConfigurationSection}:HealthProbeIntervalSeconds", "86400"),
                .. settings.Select(static s => new KeyValuePair<string, string?>($"{OutboxOptions.ConfigurationSection}:{s.Key}", s.Value)),
            ],
            services => services.AddSingleton<TimeProvider>(clock));

    /// <summary>
    /// 健康项此刻缓存的状态
    /// </summary>
    private static ContributorStatus Status(TestHost host)
        => Assert.Single(host.App.Health().Contributions, static c => c.Name == OutboxFeature.HealthName);

    /// <summary>
    /// 立即探一次，返回探测之后的状态
    /// </summary>
    private static async Task<ContributorStatus> ProbeAsync(TestHost host)
    {
        await host.Services.GetRequiredKeyedService<PeriodicHealthContributor>(OutboxFeature.HealthName).ProbeAsync(CancellationToken.None);
        return Status(host);
    }

    /// <summary>
    /// 一条退避中的未投递行
    /// </summary>
    /// <remarks>
    /// 下一次投递时间在一年以后：后台的投递循环取不到它，它一直算未投递
    /// </remarks>
    private static OutboxMessage Pending(string id) => new()
    {
        Id = Guid.CreateVersion7(),
        Topic = "orders",
        Payload = CloudEventsFormat.Encode(new MessageEnvelope { Id = id, Source = "/tests", Type = "orders.placed" }),
        CreatedAt = _start,
        NextAttemptAt = _start.AddDays(365),
        Attempts = 1,
    };

    /// <summary>
    /// 一条已经进死信的行
    /// </summary>
    private static OutboxMessage DeadLettered(string id)
    {
        var row = Pending(id);
        row.NextAttemptAt = _start;
        row.DeadLetteredAt = _start;
        row.Attempts = 10;
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
    /// 在一次调用里删除行
    /// </summary>
    private static async Task RemoveAsync(TestHost host, params OutboxMessage[] rows)
        => (await host.InvokeAsync(async ctx =>
        {
            foreach (var removed in await ctx.Services.GetRequiredService<IRepository<OutboxMessage, Guid>>().RemoveRangeAsync(rows, ctx.CancellationToken))
            {
                removed.ThrowIfFailure();
            }
        })).ThrowIfFailure();

    /// <summary>
    /// 轮询到条件成立，超时失败
    /// </summary>
    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!await condition())
        {
            Assert.True(stopwatch.Elapsed < _patience, "The condition was not met in time.");
            await Task.Delay(20);
        }
    }

    /// <summary>
    /// 读出全部行
    /// </summary>
    private static async Task<IReadOnlyList<OutboxMessage>> RowsAsync(TestHost host)
    {
        IReadOnlyList<OutboxMessage> rows = [];
        (await host.InvokeAsync(async ctx => rows = await ctx.Services.GetRequiredService<IRepository<OutboxMessage, Guid>>().ListAsync(EntityQuery.All<OutboxMessage>(), ctx.CancellationToken))).ThrowIfFailure();
        return rows;
    }
}
