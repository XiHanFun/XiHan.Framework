// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using SqlSugar;
using XiHan.Framework.Messaging.Abstractions.CloudEvents;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Outbox;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 发件箱表在 SqlSugar 上的映射与投递循环的查询（ADR-0064）。
/// </summary>
/// <remarks>
/// 主题与失败原因的列长、到期判定里的可空比较（下一次投递时间为空视为到期）、死信不再取到、超过保留期的已投递行被删，在 SQLite 上走一遍真实的 SQL 翻译。
/// </remarks>
public sealed class SqlSugarOutboxTests : IDisposable
{
    /// <summary>
    /// 时钟起点。
    /// </summary>
    private static readonly DateTimeOffset _start = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 本测试实例独占的临时 SQLite 数据库。
    /// </summary>
    private readonly SqliteDatabase _database = new();

    /// <summary>
    /// 测试结束后删除临时数据库文件。
    /// </summary>
    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task OutboxTable_DeclaresColumnLengths_AndTheDispatcherQueriesTranslate()
    {
        var clock = new FakeTimeProvider(_start);
        await using var host = await TestHost.StartAsync(
            static builder => builder.UseFeature(SqliteDatabase.CreateFeature()),
            _database.Configuration,
            services =>
            {
                services.AddSingleton<TimeProvider>(clock);
                services.AddRepository<OutboxMessage, Guid>();
            });

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var columns = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>().EntityMaintenance.GetEntityInfo<OutboxMessage>().Columns;
            Assert.Equal(128, columns.Single(static c => c.PropertyName == nameof(OutboxMessage.Topic)).Length);
            Assert.Equal(StaticConfig.CodeFirst_BigString, columns.Single(static c => c.PropertyName == nameof(OutboxMessage.LastError)).DataType);
            Assert.True(columns.Single(static c => c.PropertyName == nameof(OutboxMessage.NextAttemptAt)).IsNullable);
        }

        // 一条旧行（下一次投递时间为空）、一条会失败的、一条退避到明天的
        var legacy = Row("legacy", 0);
        legacy.NextAttemptAt = null;
        var later = Row("later", 2);
        later.NextAttemptAt = _start.AddDays(1);
        (await host.InvokeAsync(async ctx =>
        {
            foreach (var added in await Repository(ctx).AddRangeAsync([legacy, Row("fail", 1), later], ctx.CancellationToken))
            {
                added.ThrowIfFailure();
            }
        })).ThrowIfFailure();

        var broker = new FailingTopicBroker();
        var dispatcher = new OutboxDispatcher(host.Services, broker, new OutboxOptions { MaxAttempts = 1, DispatchedRetention = TimeSpan.FromHours(1) }, clock, NullLoggerFactory.Instance);

        // 第 1 轮：旧行投递，会失败的用尽一次尝试进死信，明天的不取
        Assert.Equal(1, await dispatcher.RunOnceAsync(CancellationToken.None));
        Assert.Equal(["legacy", "fail"], broker.Published);
        var rows = await RowsAsync(host);
        Assert.Equal(_start, rows["legacy"].DispatchedAt);
        Assert.Equal(_start, rows["fail"].DeadLetteredAt);
        Assert.True(rows["fail"].LastError!.Length > 600);
        Assert.Null(rows["later"].DispatchedAt);

        // 第 2 轮：死信不再取到；过了保留期旧行被删；到了明天的照常投递
        clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal(1, await dispatcher.RunOnceAsync(CancellationToken.None));
        Assert.Equal(["legacy", "fail", "later"], broker.Published);
        rows = await RowsAsync(host);
        Assert.Equal(["fail", "later"], rows.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(_start.AddDays(1), rows["later"].DispatchedAt);
    }

    /// <summary>
    /// 一条可解码的发件箱行。
    /// </summary>
    /// <remarks>
    /// 创建时间按序号递增，下一次投递时间为起点。
    /// </remarks>
    private static OutboxMessage Row(string id, int order) => new()
    {
        Id = Guid.CreateVersion7(),
        Topic = "orders",
        Payload = CloudEventsFormat.Encode(new MessageEnvelope { Id = id, Source = "/tests", Type = "orders.placed" }),
        CreatedAt = _start.AddMilliseconds(order),
        NextAttemptAt = _start,
    };

    /// <summary>
    /// 调用作用域里的发件箱仓储。
    /// </summary>
    private static IRepository<OutboxMessage, Guid> Repository(InvocationContext context) => context.Services.GetRequiredService<IRepository<OutboxMessage, Guid>>();

    /// <summary>
    /// 读出全部行，按载荷里的事件 ID 索引。
    /// </summary>
    private static async Task<Dictionary<string, OutboxMessage>> RowsAsync(TestHost host)
    {
        IReadOnlyList<OutboxMessage> rows = [];
        (await host.InvokeAsync(async ctx => rows = await Repository(ctx).ListAsync(EntityQuery.All<OutboxMessage>(), ctx.CancellationToken))).ThrowIfFailure();
        return rows.ToDictionary(static r => CloudEventsFormat.Decode(r.Payload).ValueOrThrow().Id, StringComparer.Ordinal);
    }
}
