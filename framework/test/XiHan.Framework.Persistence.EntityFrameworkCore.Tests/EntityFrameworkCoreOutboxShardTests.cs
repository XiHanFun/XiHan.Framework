// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.CloudEvents;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Outbox;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.MultiTenancy.Abstractions.Sharding;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.MultiTenancy.InMemory;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// 发件箱投递、死信重投与健康项覆盖租户分片库（ADR-0211、ADR-0272）
/// </summary>
/// <remarks>
/// EF Core 一个 DbContext 一个连接：带分片的调用里发件箱行连同业务写入一起落进租户库，投递器要连到那个库才发得出去；死信重投要到那个库里找行，健康项要把各库的行数加起来
/// </remarks>
public sealed class EntityFrameworkCoreOutboxShardTests : IDisposable
{
    /// <summary>
    /// 应用默认库
    /// </summary>
    private readonly SqliteDatabase _default = new();

    /// <summary>
    /// 租户 <c>acme</c> 与 <c>globex</c> 共用的分片库
    /// </summary>
    private readonly SqliteDatabase _shard = new();

    /// <summary>
    /// 另一个分片库
    /// </summary>
    /// <remarks>
    /// 用例按需给它建表或不建
    /// </remarks>
    private readonly SqliteDatabase _other = new();

    /// <summary>
    /// 删除临时数据库文件
    /// </summary>
    public void Dispose()
    {
        _default.Dispose();
        _shard.Dispose();
        _other.Dispose();
    }

    [Fact]
    public async Task Dispatcher_DeliversMessagesWrittenIntoTenantShards()
    {
        await using var host = await StartAsync();
        await AddAsync(host, null, "platform", 0);
        await AddAsync(host, "beta", "beta", 1);
        await AddAsync(host, "acme", "acme", 2);
        await AddAsync(host, "globex", "globex", 3);

        // 先确认行物理上的落点：带分片的租户写进分片库，共库的租户与平台写进默认库
        Assert.Equal(["acme", "globex"], await EventsAsync(_shard.ConnectionString));
        Assert.Equal(["beta", "platform"], await EventsAsync(_default.ConnectionString));

        var broker = new RecordingOutboxBroker();
        var dispatcher = new OutboxDispatcher(host.Services, broker, new OutboxOptions(), TimeProvider.System, NullLoggerFactory.Instance);

        // 默认库先投递，再投递分片库；两个租户共用一个分片库，只连一次、每条只发一次
        Assert.Equal(4, await dispatcher.RunOnceAsync(CancellationToken.None));
        Assert.Equal(["platform", "beta", "acme", "globex"], broker.Published);
        Assert.Empty(await EventsAsync(_shard.ConnectionString, undeliveredOnly: true));
        Assert.Empty(await EventsAsync(_default.ConnectionString, undeliveredOnly: true));

        Assert.Equal(0, await dispatcher.RunOnceAsync(CancellationToken.None));
        Assert.Equal(4, broker.Published.Count);
    }

    [Fact]
    public async Task Dispatcher_LeavesShardsAlone_WhenIncludeShardsIsOff()
    {
        await using var host = await StartAsync();
        await AddAsync(host, null, "platform", 0);
        await AddAsync(host, "acme", "acme", 1);

        var broker = new RecordingOutboxBroker();
        var dispatcher = new OutboxDispatcher(host.Services, broker, new OutboxOptions { IncludeShards = false }, TimeProvider.System, NullLoggerFactory.Instance);

        Assert.Equal(1, await dispatcher.RunOnceAsync(CancellationToken.None));
        Assert.Equal(["platform"], broker.Published);
        Assert.Equal(["acme"], await EventsAsync(_shard.ConnectionString, undeliveredOnly: true));
    }

    [Fact]
    public async Task Dispatcher_KeepsDeliveringOtherDatabases_WhenOneShardFails()
    {
        // initech 的分片库没有发件箱表：它那一轮失败，默认库与 acme 的分片库照常投递并提交，全部轮完之后再抛出
        await using var host = await StartAsync(new KeyValuePair<string, string?>("XiHan:MultiTenancy:Tenants:initech:Properties:" + TenantShardKeys.ConnectionString, _other.ConnectionString));
        await AddAsync(host, null, "platform", 0);
        await AddAsync(host, "acme", "acme", 1);

        var broker = new RecordingOutboxBroker();
        var dispatcher = new OutboxDispatcher(host.Services, broker, new OutboxOptions(), TimeProvider.System, NullLoggerFactory.Instance);

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => dispatcher.RunOnceAsync(CancellationToken.None));
        Assert.IsNotType<AggregateException>(failure);
        Assert.Equal(["platform", "acme"], broker.Published);
        Assert.Empty(await EventsAsync(_shard.ConnectionString, undeliveredOnly: true));
        Assert.Empty(await EventsAsync(_default.ConnectionString, undeliveredOnly: true));
    }

    [Fact]
    public async Task Dispatcher_PicksUpNewShards_AfterTheRefreshInterval()
    {
        await using (var otherContext = CreateContext(_other.ConnectionString))
        {
            await otherContext.Database.EnsureCreatedAsync();
        }

        await using var host = await StartAsync();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var broker = new RecordingOutboxBroker();
        var dispatcher = new OutboxDispatcher(host.Services, broker, new OutboxOptions { ShardRefreshInterval = TimeSpan.FromMinutes(1) }, clock, NullLoggerFactory.Instance);

        // 第 1 轮读出分片清单：只有 acme 与 globex 共用的库
        Assert.Equal(0, await dispatcher.RunOnceAsync(CancellationToken.None));

        // 之后新建一个带分片的租户，消息写进它的库
        var administration = host.Services.GetRequiredService<ITenantAdministration>();
        var created = await administration.UpsertAsync([new TenantInfo { Id = "initech", Properties = new Dictionary<string, string> { [TenantShardKeys.ConnectionString] = _other.ConnectionString } }], CancellationToken.None);
        Assert.All(created, static r => r.ThrowIfFailure());
        await AddAsync(host, "initech", "initech", 0);

        // 清单还没到重读的时刻：新库不在其中
        clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(0, await dispatcher.RunOnceAsync(CancellationToken.None));
        Assert.Empty(broker.Published);

        // 过了重读间隔：新库纳入投递
        clock.Advance(TimeSpan.FromSeconds(31));
        Assert.Equal(1, await dispatcher.RunOnceAsync(CancellationToken.None));
        Assert.Equal(["initech"], broker.Published);
    }

    [Fact]
    public async Task DeadLetters_AreRedriven_InWhicheverDatabaseHoldsThem()
    {
        await using var host = await StartAsync();
        var platform = await AddAsync(host, null, "platform", 0, deadLettered: true);
        var acme = await AddAsync(host, "acme", "acme", 1, deadLettered: true);
        var globex = await AddAsync(host, "globex", "globex", 2);
        var unknown = Guid.CreateVersion7();
        var deadLetters = new OutboxDeadLetters(host.Services, new OutboxOptions(), TimeProvider.System, NullLoggerFactory.Instance);

        var results = await deadLetters.RedriveManyAsync([acme, unknown, platform, globex], CancellationToken.None);

        // 默认库与分片库里的死信都找得到；分片库里那条待投递的行不是死信；哪个库里都没有的是不存在。结果与入参同序
        Assert.Equal(
            [null, MessagingErrors.OutboxMessageNotFound, null, MessagingErrors.OutboxNotDeadLettered],
            results.Select(static r => r.Error?.Code));

        // 两个库里的行都回到待投递，投递器照常发出
        var broker = new RecordingOutboxBroker();
        var dispatcher = new OutboxDispatcher(host.Services, broker, new OutboxOptions(), TimeProvider.System, NullLoggerFactory.Instance);
        Assert.Equal(3, await dispatcher.RunOnceAsync(CancellationToken.None));
        Assert.Equal(["platform", "acme", "globex"], broker.Published);
    }

    [Fact]
    public async Task Redrive_LeavesShardsAlone_WhenIncludeShardsIsOff()
    {
        await using var host = await StartAsync();
        var platform = await AddAsync(host, null, "platform", 0, deadLettered: true);
        var acme = await AddAsync(host, "acme", "acme", 1, deadLettered: true);
        var deadLetters = new OutboxDeadLetters(host.Services, new OutboxOptions { IncludeShards = false }, TimeProvider.System, NullLoggerFactory.Instance);

        var results = await deadLetters.RedriveManyAsync([platform, acme], CancellationToken.None);

        // 与投递循环覆盖同一批库：关掉分片之后只看默认库，分片库里的行按不存在报告
        Assert.Equal([null, MessagingErrors.OutboxMessageNotFound], results.Select(static r => r.Error?.Code));
    }

    [Fact]
    public async Task Redrive_WhenAShardCannotBeRead_StillRedrivesRowsFoundElsewhere_AndThrowsOnlyForRowsItCannotAccountFor()
    {
        // aaa 的分片库没有发件箱表，按租户标识排在 acme 的分片库之前
        await using var host = await StartAsync(new KeyValuePair<string, string?>("XiHan:MultiTenancy:Tenants:aaa:Properties:" + TenantShardKeys.ConnectionString, _other.ConnectionString));
        var acme = await AddAsync(host, "acme", "acme", 0, deadLettered: true);
        using var logs = new LogCapture();
        var deadLetters = new OutboxDeadLetters(host.Services, new OutboxOptions(), TimeProvider.System, logs);

        // 要找的行在后面的库里找到了：坏掉的库不挡它，只记一条带异常的警告
        var results = await deadLetters.RedriveManyAsync([acme], CancellationToken.None);
        Assert.True(Assert.Single(results).IsSuccess, results[0].ToString());
        Assert.Single(logs.Entries, static e => e.Category == typeof(OutboxDeadLetters).FullName && e.Level == LogLevel.Warning && e.Exception is not null);

        // 有标识哪个库里都没找到，而有一个库没能看：说不清它存不存在，抛出那个库的异常
        var failure = await Assert.ThrowsAnyAsync<Exception>(async () => await deadLetters.RedriveManyAsync([Guid.CreateVersion7()], CancellationToken.None));
        Assert.IsNotType<AggregateException>(failure);
        Assert.IsNotAssignableFrom<OperationCanceledException>(failure);
    }

    [Fact]
    public async Task HealthItem_AddsUpTheDefaultDatabaseAndEveryShard()
    {
        await using var host = await StartWithOutboxAsync();
        await AddAsync(host, null, "platform", 0);
        await AddAsync(host, "acme", "acme", 1);
        await AddAsync(host, "globex", "globex", 2, deadLettered: true);

        var status = await ProbeAsync(host);

        // 默认库一条未投递、分片库一条未投递加一条死信：两个库相加。创建时间由仓储按时钟写入，最老的一条刚写入不久
        Assert.Equal(2L, status.Data["pending"]);
        Assert.Equal(1L, status.Data["deadLettered"]);
        Assert.Equal(2, status.Data["databases"]);
        Assert.InRange(Assert.IsType<long>(status.Data["oldestPendingSeconds"]), 0L, 300L);
        Assert.Equal(LifecycleState.Degraded, status.State);
        Assert.Equal(MessagingErrors.OutboxDeadLettered, status.Error?.Code);
    }

    [Fact]
    public async Task HealthItem_IsDegradedWithoutNumbers_WhenAShardCannotBeRead()
    {
        // initech 的分片库没有发件箱表：数不全就不报数
        await using var host = await StartWithOutboxAsync(new KeyValuePair<string, string?>("XiHan:MultiTenancy:Tenants:initech:Properties:" + TenantShardKeys.ConnectionString, _other.ConnectionString));
        await AddAsync(host, null, "platform", 0);

        var status = await ProbeAsync(host);

        Assert.Equal(LifecycleState.Degraded, status.State);
        Assert.Equal(KernelErrors.HealthProbeFailed, status.Error?.Code);
        Assert.Empty(status.Data);
        // 条目降级不影响应用接收调用
        Assert.True(host.App.Health().Ready);
    }

    /// <summary>
    /// 启动宿主
    /// </summary>
    /// <remarks>
    /// 分片库的表结构由应用管理：启动只在默认库建表，这里先用独立上下文给 acme 与 globex 共用的分片库建发件箱表
    /// </remarks>
    private async Task<TestHost> StartAsync(params KeyValuePair<string, string?>[] extra)
    {
        await using (var shardContext = CreateContext(_shard.ConnectionString))
        {
            await shardContext.Database.EnsureCreatedAsync();
        }

        return await TestHost.StartAsync(
            static b => b.UseFeature<InMemoryMultiTenancyFeature>().UseFeature(new EntityFrameworkCorePersistenceFeature<OutboxDbContext>(static (binding, options) => options.UseSqlite(binding.ValueOrThrow().Value))),
            [
                .. _default.Configuration,
                new("XiHan:MultiTenancy:Tenants:acme:Properties:" + TenantShardKeys.ConnectionString, _shard.ConnectionString),
                new("XiHan:MultiTenancy:Tenants:globex:Properties:" + TenantShardKeys.ConnectionString, _shard.ConnectionString),
                new("XiHan:MultiTenancy:Tenants:beta:DisplayName", "Beta"),
                .. extra,
            ],
            static services => services.AddRepository<OutboxMessage, Guid>());
    }

    /// <summary>
    /// 启动装配了发件箱特性的宿主
    /// </summary>
    /// <remarks>
    /// 队列能力由一个只记录的代理提供。投递循环的轮询间隔配成一小时：启动时那一轮跑完之后不再投递，用例写入的行保持原样
    /// </remarks>
    private async Task<TestHost> StartWithOutboxAsync(params KeyValuePair<string, string?>[] extra)
    {
        await using (var shardContext = CreateContext(_shard.ConnectionString))
        {
            await shardContext.Database.EnsureCreatedAsync();
        }

        var broker = new StubFeature(TestManifests.Create("xihan.test.outbox-broker", provides: [$"{MessagingCapabilities.Queue}@{MessagingCapabilities.Version}"]))
        {
            OnConfigure = static context => context.Services.AddSingleton<IMessageBroker>(new RecordingOutboxBroker()),
        };
        return await TestHost.StartAsync(
            b => b.UseFeature<InMemoryMultiTenancyFeature>()
                .UseFeature(new EntityFrameworkCorePersistenceFeature<OutboxDbContext>(static (binding, options) => options.UseSqlite(binding.ValueOrThrow().Value)))
                .UseFeature(broker)
                .UseFeature<OutboxFeature>(),
            [
                .. _default.Configuration,
                new("XiHan:MultiTenancy:Tenants:acme:Properties:" + TenantShardKeys.ConnectionString, _shard.ConnectionString),
                new("XiHan:MultiTenancy:Tenants:globex:Properties:" + TenantShardKeys.ConnectionString, _shard.ConnectionString),
                new("XiHan:MultiTenancy:Tenants:beta:DisplayName", "Beta"),
                new($"{OutboxOptions.ConfigurationSection}:PollIntervalSeconds", "3600"),
                .. extra,
            ]);
    }

    /// <summary>
    /// 立即探一次发件箱的健康项，返回探测之后的状态
    /// </summary>
    private static async Task<ContributorStatus> ProbeAsync(TestHost host)
    {
        await host.Services.GetRequiredKeyedService<PeriodicHealthContributor>(OutboxFeature.HealthName).ProbeAsync(CancellationToken.None);
        return Assert.Single(host.App.Health().Contributions, static c => c.Name == OutboxFeature.HealthName);
    }

    /// <summary>
    /// 在以 <paramref name="tenant"/> 开的调用里写一条发件箱行
    /// </summary>
    /// <remarks>
    /// 与 <see cref="OutboxPublisher"/> 在调用的工作单元里做的一样：行随调用的事务提交。创建时间按序号递增，投递顺序确定。<paramref name="deadLettered"/> 为真时写入一条已经进死信的行。返回行的标识
    /// </remarks>
    private static async Task<Guid> AddAsync(TestHost host, string? tenant, string eventId, int order, bool deadLettered = false)
    {
        var now = DateTimeOffset.UtcNow;
        var row = new OutboxMessage
        {
            Id = Guid.CreateVersion7(),
            Topic = "orders",
            Payload = CloudEventsFormat.Encode(new MessageEnvelope { Id = eventId, Source = "/tests", Type = "orders.placed" }),
            CreatedAt = now.AddMinutes(order - 10),
            NextAttemptAt = now.AddMinutes(-10),
            DeadLetteredAt = deadLettered ? now.AddMinutes(-5) : null,
            Attempts = deadLettered ? 10 : 0,
        };
        var baggage = tenant is null ? null : new Dictionary<string, string> { [BaggageKeys.Tenant] = tenant };
        (await host.InvokeAsync(async context => context.SetOutcome(await context.Services.GetRequiredService<IRepository<OutboxMessage, Guid>>().AddAsync(row, context.CancellationToken)), baggage: baggage)).ThrowIfFailure();
        return row.Id;
    }

    /// <summary>
    /// 直接读一个库里的发件箱行，返回事件 ID（按字典序）
    /// </summary>
    private static async Task<List<string>> EventsAsync(string connectionString, bool undeliveredOnly = false)
    {
        await using var context = CreateContext(connectionString);
        var rows = await context.Set<OutboxMessage>().ToListAsync();
        return [.. rows.Where(r => !undeliveredOnly || r.DispatchedAt is null).Select(static r => CloudEventsFormat.Decode(r.Payload).ValueOrThrow().Id).Order(StringComparer.Ordinal)];
    }

    /// <summary>
    /// 创建指向给定连接串的独立上下文
    /// </summary>
    private static OutboxDbContext CreateContext(string connectionString)
        => new(new DbContextOptionsBuilder<OutboxDbContext>().UseSqlite(connectionString).Options);
}
