// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Conformance.Fixtures;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 差异日志：<see cref="SqlSugarPersistenceFeature.DiffLog"/> 开启的实体，仓储的插入、更新、软删除、物理删除与批量方法（含分表实体）都经 SqlSugar 差异日志，
/// 回调拿到操作类型、主键与前后值，业务数据是 <see cref="SqlSugarDiffLogData"/>，并在调用方的执行流里同步触发；没开启的实体不触发。SQLite 必跑，PostgreSQL 按环境变量跑。
/// </summary>
public sealed class SqlSugarDiffLogTests : IDisposable
{
    /// <summary>
    /// 2026 年 1 月 10 日（UTC）。
    /// </summary>
    private static readonly DateTime _january = new(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// 2026 年 2 月 10 日（UTC）。
    /// </summary>
    private static readonly DateTime _february = new(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// 本测试建出的临时数据库，结束时删除。
    /// </summary>
    private readonly List<IDisposable> _databases = [];

    /// <summary>
    /// 删除临时数据库。
    /// </summary>
    public void Dispose()
    {
        foreach (var database in _databases)
        {
            database.Dispose();
        }
    }

    [Theory]
    [InlineData(DbType.Sqlite)]
    [InlineData(DbType.PostgreSQL)]
    public async Task EnabledEntity_LogsAddUpdateAndSoftDelete_InTheCallersFlowAndScope(DbType dbType)
    {
        var sink = new DiffLogSink();
        await using var host = await StartHostAsync(dbType, sink, static type => type == typeof(ConformanceOrder));
        var id = Guid.CreateVersion7();

        // 插入：写后值是新行，主键列有标记；业务数据带实体类型与仓储所在作用域，回调看得到调用方设置的 AsyncLocal
        IServiceProvider? scope = null;
        (await RunAsync(host, async ctx =>
        {
            scope = ctx.Services;
            DiffLogSink.Ambient = "add";
            return await Orders(ctx).AddAsync(new ConformanceOrder { Id = id, Name = "first", Amount = 1 }, ctx.CancellationToken);
        })).ThrowIfFailure();
        var (insert, insertAmbient) = Assert.Single(sink.Events);
        Assert.Equal((DiffType.insert, "add"), (insert.DiffType, insertAmbient));
        var data = Assert.IsType<SqlSugarDiffLogData>(insert.BusinessData);
        Assert.Equal(typeof(ConformanceOrder), data.EntityType);
        Assert.Same(scope, data.Services);
        Assert.Equal("first", Text(insert.AfterData, nameof(ConformanceOrder.Name)));
        Assert.True(Column(insert.AfterData, nameof(ConformanceOrder.Id)).IsPrimaryKey);
        Assert.Equal(id.ToString(), Text(insert.AfterData, nameof(ConformanceOrder.Id)), ignoreCase: true);

        // 更新：写前值与写后值
        sink.Clear();
        (await RunAsync(host, async ctx =>
        {
            var order = (await Orders(ctx).FindAsync(id, ctx.CancellationToken)).ValueOrThrow();
            order.Name = "second";
            DiffLogSink.Ambient = "update";
            return await Orders(ctx).UpdateAsync(order, ctx.CancellationToken);
        })).ThrowIfFailure();
        var (update, updateAmbient) = Assert.Single(sink.Events);
        Assert.Equal((DiffType.update, "update"), (update.DiffType, updateAmbient));
        Assert.Equal(("first", "second"), (Text(update.BeforeData, nameof(ConformanceOrder.Name)), Text(update.AfterData, nameof(ConformanceOrder.Name))));

        // 软删除是一次更新：删除标记前后不同
        sink.Clear();
        (await RunAsync(host, async ctx => await Orders(ctx).RemoveAsync((await Orders(ctx).FindAsync(id, ctx.CancellationToken)).ValueOrThrow(), ctx.CancellationToken))).ThrowIfFailure();
        var (tombstone, _) = Assert.Single(sink.Events);
        Assert.Equal(DiffType.update, tombstone.DiffType);
        Assert.NotEqual(Text(tombstone.BeforeData, nameof(ConformanceOrder.IsDeleted)), Text(tombstone.AfterData, nameof(ConformanceOrder.IsDeleted)));
    }

    [Theory]
    [InlineData(DbType.Sqlite)]
    [InlineData(DbType.PostgreSQL)]
    public async Task Batches_AndHardDeletes_AreLogged_WhileOtherEntitiesStayQuiet(DbType dbType)
    {
        var sink = new DiffLogSink();
        await using var host = await StartHostAsync(dbType, sink, static type => type == typeof(ConformanceItem));

        // 批量插入、批量更新、批量物理删除：各一条事件，行数与批量一致
        (await RunAsync(host, async ctx => Fold(await Items(ctx).AddRangeAsync([new ConformanceItem { Id = 1, Label = "a" }, new ConformanceItem { Id = 2, Label = "b" }], ctx.CancellationToken)))).ThrowIfFailure();
        (await RunAsync(host, async ctx => Fold(await Items(ctx).UpdateRangeAsync([new ConformanceItem { Id = 1, Label = "a2" }, new ConformanceItem { Id = 2, Label = "b2" }], ctx.CancellationToken)))).ThrowIfFailure();
        (await RunAsync(host, async ctx => Fold(await Items(ctx).RemoveRangeAsync([new ConformanceItem { Id = 1 }, new ConformanceItem { Id = 2 }], ctx.CancellationToken)))).ThrowIfFailure();
        Assert.Equal([DiffType.insert, DiffType.update, DiffType.delete], sink.Events.Select(static e => e.Model.DiffType));
        var (inserted, updated, deleted) = (sink.Events[0].Model, sink.Events[1].Model, sink.Events[2].Model);
        Assert.Equal(["a", "b"], Texts(inserted.AfterData, nameof(ConformanceItem.Label)));
        Assert.Equal(["a", "b"], Texts(updated.BeforeData, nameof(ConformanceItem.Label)));
        Assert.Equal(["a2", "b2"], Texts(updated.AfterData, nameof(ConformanceItem.Label)));
        Assert.Equal(["a2", "b2"], Texts(deleted.BeforeData, nameof(ConformanceItem.Label)));

        // 没开启的实体：增、改、删都不触发
        sink.Clear();
        var id = Guid.CreateVersion7();
        (await RunAsync(host, ctx => Orders(ctx).AddAsync(new ConformanceOrder { Id = id, Name = "quiet" }, ctx.CancellationToken))).ThrowIfFailure();
        (await RunAsync(host, async ctx => await Orders(ctx).RemoveAsync((await Orders(ctx).FindAsync(id, ctx.CancellationToken)).ValueOrThrow(), ctx.CancellationToken))).ThrowIfFailure();
        Assert.Empty(sink.Events);
    }

    [Theory]
    [InlineData(DbType.Sqlite)]
    [InlineData(DbType.PostgreSQL)]
    public async Task SplitTableEntity_IsLogged_AcrossMonthlyTables(DbType dbType)
    {
        var sink = new DiffLogSink();
        await using var host = await StartHostAsync(dbType, sink, static type => type == typeof(SplitTableRecord));
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();

        // 两个月各一条：分表插入按表分组，写后值覆盖两条
        (await RunAsync(host, async ctx => Fold(await Splits(ctx).AddRangeAsync(
        [
            new SplitTableRecord { Id = first, RecordedAt = _january, Text = "jan" },
            new SplitTableRecord { Id = second, RecordedAt = _february, Text = "feb" },
        ], ctx.CancellationToken)))).ThrowIfFailure();
        Assert.All(sink.Events, static e => Assert.Equal(DiffType.insert, e.Model.DiffType));
        Assert.Equal(["feb", "jan"], sink.Events.SelectMany(static e => Texts(e.Model.AfterData, nameof(SplitTableRecord.Text))).Order(StringComparer.Ordinal));

        // 更新二月那条：写前值取自它所在的表
        sink.Clear();
        (await RunAsync(host, async ctx =>
        {
            var record = (await Splits(ctx).FindAsync(second, ctx.CancellationToken)).ValueOrThrow();
            record.Text = "feb-2";
            return await Splits(ctx).UpdateAsync(record, ctx.CancellationToken);
        })).ThrowIfFailure();
        var (update, _) = Assert.Single(sink.Events);
        Assert.Equal(("feb", "feb-2"), (Text(update.BeforeData, nameof(SplitTableRecord.Text)), Text(update.AfterData, nameof(SplitTableRecord.Text))));

        // 物理删除两条：分表删除按表各一条事件，写前值覆盖两条，删完表里没有了
        sink.Clear();
        (await RunAsync(host, async ctx => Fold(await Splits(ctx).RemoveRangeAsync(
        [
            new SplitTableRecord { Id = first, RecordedAt = _january },
            new SplitTableRecord { Id = second, RecordedAt = _february },
        ], ctx.CancellationToken)))).ThrowIfFailure();
        Assert.Equal(2, sink.Events.Count);
        Assert.All(sink.Events, static e => Assert.Equal(DiffType.delete, e.Model.DiffType));
        Assert.Equal(["feb-2", "jan"], sink.Events.SelectMany(static e => Texts(e.Model.BeforeData, nameof(SplitTableRecord.Text))).Order(StringComparer.Ordinal));
        (await RunAsync(host, async ctx =>
        {
            Assert.Equal(0, await Splits(ctx).CountAsync(EntityQuery.All<SplitTableRecord>(), ctx.CancellationToken));
            return Result.Success();
        })).ThrowIfFailure();
    }

    /// <summary>
    /// 启动宿主：给定数据库（PostgreSQL 没有配置时跳过）、差异日志回调接到 <paramref name="sink"/>、按 <paramref name="diffLog"/> 开启的实体，以及订单、条目与分表三种实体。
    /// </summary>
    private Task<TestHost> StartHostAsync(DbType dbType, DiffLogSink sink, Func<Type, bool> diffLog)
    {
        IEnumerable<KeyValuePair<string, string?>> configuration;
        if (dbType == DbType.PostgreSQL)
        {
            var postgres = PostgresTestServer.CreateDatabase();
            _databases.Add(postgres);
            configuration = postgres.Configuration;
        }
        else
        {
            var sqlite = new SqliteDatabase();
            _databases.Add(sqlite);
            configuration = sqlite.Configuration;
        }

        var feature = new SqlSugarPersistenceFeature((binding, config) =>
        {
            config.DbType = dbType;
            config.ConnectionString = binding.ValueOrThrow().Value;
            config.AopEvents = new AopEvents { OnDiffLogEvent = sink.Record };
        })
        {
            DiffLog = diffLog,
        };

        return TestHost.StartAsync(
            builder => builder.UseFeature(feature),
            configuration,
            static services =>
            {
                services.AddSingleton(TimeProvider.System);
                services.AddRepository<ConformanceOrder, Guid>();
                services.AddRepository<ConformanceItem, int>();
                services.AddRepository<SplitTableRecord, Guid>();
            });
    }

    /// <summary>
    /// 订单仓储（软删除）。
    /// </summary>
    private static IRepository<ConformanceOrder, Guid> Orders(InvocationContext context) => context.Services.GetRequiredService<IRepository<ConformanceOrder, Guid>>();

    /// <summary>
    /// 条目仓储（物理删除）。
    /// </summary>
    private static IRepository<ConformanceItem, int> Items(InvocationContext context) => context.Services.GetRequiredService<IRepository<ConformanceItem, int>>();

    /// <summary>
    /// 分表实体仓储。
    /// </summary>
    private static IRepository<SplitTableRecord, Guid> Splits(InvocationContext context) => context.Services.GetRequiredService<IRepository<SplitTableRecord, Guid>>();

    /// <summary>
    /// 第一行里名为 <paramref name="name"/> 的列（不区分大小写）。
    /// </summary>
    private static DiffLogColumnInfo Column(List<DiffLogTableInfo>? rows, string name)
    {
        Assert.NotNull(rows);
        return Assert.Single(rows[0].Columns, column => string.Equals(column.ColumnName, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 第一行里某列的文本值。
    /// </summary>
    private static string? Text(List<DiffLogTableInfo>? rows, string name) => Column(rows, name).Value?.ToString();

    /// <summary>
    /// 每一行里某列的文本值，按字典序。
    /// </summary>
    private static List<string?> Texts(List<DiffLogTableInfo>? rows, string name)
    {
        Assert.NotNull(rows);
        return rows.Select(row => Assert.Single(row.Columns, column => string.Equals(column.ColumnName, name, StringComparison.OrdinalIgnoreCase)).Value?.ToString()).Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// 逐项结果折叠成一个：第一个失败即整体失败。
    /// </summary>
    private static Result Fold(IReadOnlyList<Result> results) => results.FirstOrDefault(static r => r.IsFailure, Result.Success());

    /// <summary>
    /// 在一次完整管道调用内执行 <paramref name="action"/>，其返回值作为调用结果。
    /// </summary>
    private static async Task<Result> RunAsync(TestHost host, Func<InvocationContext, Task<Result>> action)
        => await host.InvokeAsync(async context => context.SetOutcome(await action(context)));
}
