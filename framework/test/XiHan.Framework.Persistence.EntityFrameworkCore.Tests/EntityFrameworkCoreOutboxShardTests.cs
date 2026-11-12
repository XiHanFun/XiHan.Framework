// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Messaging.Abstractions.CloudEvents;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Outbox;
using XiHan.Framework.MultiTenancy.Abstractions.Sharding;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.MultiTenancy.InMemory;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// 发件箱投递覆盖租户分片库（ADR-0211）
/// </summary>
/// <remarks>
/// EF Core 一个 DbContext 一个连接：带分片的调用里发件箱行连同业务写入一起落进租户库，投递器要连到那个库才发得出去
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
    /// 在以 <paramref name="tenant"/> 开的调用里写一条发件箱行
    /// </summary>
    /// <remarks>
    /// 与 <see cref="OutboxPublisher"/> 在调用的工作单元里做的一样：行随调用的事务提交。创建时间按序号递增，投递顺序确定
    /// </remarks>
    private static async Task AddAsync(TestHost host, string? tenant, string eventId, int order)
    {
        var now = DateTimeOffset.UtcNow;
        var row = new OutboxMessage
        {
            Id = Guid.CreateVersion7(),
            Topic = "orders",
            Payload = CloudEventsFormat.Encode(new MessageEnvelope { Id = eventId, Source = "/tests", Type = "orders.placed" }),
            CreatedAt = now.AddMinutes(order - 10),
            NextAttemptAt = now.AddMinutes(-10),
        };
        var baggage = tenant is null ? null : new Dictionary<string, string> { [BaggageKeys.Tenant] = tenant };
        (await host.InvokeAsync(async context => context.SetOutcome(await context.Services.GetRequiredService<IRepository<OutboxMessage, Guid>>().AddAsync(row, context.CancellationToken)), baggage: baggage)).ThrowIfFailure();
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
