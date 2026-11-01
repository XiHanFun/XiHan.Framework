// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.DataSources;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.Schema;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// SqlSugar 的库结构读取与实体目录。
/// </summary>
/// <remarks>
/// SQLite 上的列属性与限制（没有注释、主键归一为不可空、长度与精度取自声明）、分表折叠、实体目录的表名与索引、运行时数据源读外部库与读取失败；PostgreSQL 上的注释、identity、serial 缺省值与大小写混合的表名（<c>XIHAN_TEST_POSTGRES</c>）。
/// </remarks>
public sealed class SqlSugarSchemaReaderTests : IDisposable
{
    /// <summary>
    /// 应用自己的库。
    /// </summary>
    private readonly SqliteDatabase _application = new();

    /// <summary>
    /// 作为外部库的另一个 SQLite 文件。
    /// </summary>
    private readonly SqliteDatabase _external = new();

    /// <summary>
    /// 测试结束后删除两个临时库。
    /// </summary>
    public void Dispose()
    {
        _application.Dispose();
        _external.Dispose();
    }

    [Fact]
    public async Task Sqlite_Columns_ReportDeclaredTypesLengthsPrecisionAndKeys()
    {
        await using var host = await StartHostAsync();
        await ExecuteAsync(host, "CREATE TABLE probe_items (id INTEGER PRIMARY KEY AUTOINCREMENT, name varchar(50) NOT NULL DEFAULT 'x', amount decimal(18,2) NULL, note TEXT, created datetime)");

        var schema = (await Reader(host).GetTableAsync(null, "probe_items", TestContext.Current.CancellationToken)).ValueOrThrow();
        var columns = schema.Columns.ToDictionary(static c => c.Name);

        Assert.Null(schema.Table.Comment);
        Assert.False(schema.Table.IsSplit);
        Assert.Equal(["id", "name", "amount", "note", "created"], schema.Columns.Select(static c => c.Name));
        // INTEGER PRIMARY KEY 由 SQLite 报告为可空，按主键归一为不可空
        Assert.True(columns["id"].IsPrimaryKey);
        Assert.True(columns["id"].IsIdentity);
        Assert.False(columns["id"].IsNullable);
        Assert.Equal(50, columns["name"].Length);
        Assert.False(columns["name"].IsNullable);
        Assert.Equal("'x'", columns["name"].DefaultValue);
        Assert.Equal(18, columns["amount"].Precision);
        Assert.Equal(2, columns["amount"].Scale);
        Assert.Null(columns["amount"].Length);
        Assert.True(columns["note"].IsNullable);
        Assert.Null(columns["note"].Length);
        Assert.Null(columns["note"].Precision);
        Assert.All(schema.Columns, static c => Assert.Null(c.Comment));
    }

    [Fact]
    public async Task SplitTables_AreFoldedIntoOneLogicalTable_ByTheRegisteredTemplate()
    {
        await using var host = await StartHostAsync(static services => services.AddRepository<SplitTableRecord, Guid>());
        await AddAsync(host, new SplitTableRecord { Id = Guid.CreateVersion7(), RecordedAt = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc), Text = "jan" });
        await AddAsync(host, new SplitTableRecord { Id = Guid.CreateVersion7(), RecordedAt = new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc), Text = "feb" });

        var page = (await Reader(host).ListTablesAsync(null, new TableListQuery { Keyword = "split_records" }, TestContext.Current.CancellationToken)).ValueOrThrow();
        var logical = Assert.Single(page.Items);
        var schema = (await Reader(host).GetTableAsync(null, "split_records", TestContext.Current.CancellationToken)).ValueOrThrow();
        var physical = await Reader(host).GetTableAsync(null, "split_records_20260101", TestContext.Current.CancellationToken);
        var mapping = Assert.Single(await Catalog(host).ListAsync(TestContext.Current.CancellationToken));

        Assert.Equal("split_records", logical.Name);
        Assert.True(logical.IsSplit);
        Assert.Contains("split_records_20260101", logical.PhysicalTables);
        Assert.Contains("split_records_20260201", logical.PhysicalTables);
        Assert.Equal([.. logical.PhysicalTables.OrderDescending(StringComparer.OrdinalIgnoreCase)], logical.PhysicalTables);
        Assert.Contains(schema.Columns, static c => c.Name == nameof(SplitTableRecord.Text));
        Assert.Equal(PersistenceErrors.TableNotFound, physical.Error?.Code);
        Assert.Equal("split_records", mapping.Table);
        Assert.True(mapping.IsSplit);
        Assert.Empty(mapping.Indexes);
    }

    [Fact]
    public async Task EntityCatalog_UsesTheMappedTableName_AndTheDeclaredIndexes()
    {
        await using var host = await StartHostAsync(static services => services.AddRepository<IndexedRecord, Guid>());

        var mapping = Assert.Single(await Catalog(host).ListAsync(TestContext.Current.CancellationToken));
        var schema = (await Reader(host).GetTableAsync(null, mapping.Table, TestContext.Current.CancellationToken)).ValueOrThrow();

        Assert.Equal("indexed_record", mapping.Table);
        Assert.Equal(EntityIndexes.Resolve(typeof(IndexedRecord), "indexed_record").Select(static i => i.Name), mapping.Indexes.Select(static i => i.Name));
        Assert.Equal(64, Assert.Single(schema.Columns, static c => c.Name == nameof(IndexedRecord.Code)).Length);
        Assert.Equal(mapping.Columns.Select(static c => c.Column).Order(StringComparer.OrdinalIgnoreCase), schema.Columns.Select(static c => c.Name).Order(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NamedDataSource_ReadsTheExternalDatabase_NotTheApplicationOne()
    {
        await using var host = await StartHostAsync(static services => services.AddRepository<IndexedRecord, Guid>());
        var definition = new DataSourceDefinition("legacy", DatabaseTypes.Sqlite, _external.ConnectionString);
        var client = (await host.Services.GetRequiredService<DataSourceRegistry>().GetOrAddAsync(definition, TestContext.Current.CancellationToken)).ValueOrThrow().GetSqlSugarClient();
        await client.Ado.ExecuteCommandAsync("CREATE TABLE \"LegacyOrder\" (\"Id\" INTEGER PRIMARY KEY, \"Title\" nvarchar(20) NOT NULL)");

        var page = (await Reader(host).ListTablesAsync(definition, new TableListQuery(), TestContext.Current.CancellationToken)).ValueOrThrow();
        var batch = (await Reader(host).GetTablesAsync(definition, ["legacyorder", "indexed_record"], TestContext.Current.CancellationToken)).ValueOrThrow();

        Assert.Equal(["LegacyOrder"], page.Items.Select(static t => t.Name));
        // 名字不区分大小写地唯一匹配；应用库的表不在外部库里
        Assert.Equal(20, Assert.Single(batch[0].ValueOrThrow().Columns, static c => c.Name == "Title").Length);
        Assert.Equal(PersistenceErrors.TableNotFound, batch[1].Error?.Code);
        Assert.Equal("legacy", batch[1].Error!.Details!["dataSource"]);
    }

    [Fact]
    public async Task UnreachableDataSource_IsUnavailable_WithoutTheSecret()
    {
        await using var host = await StartHostAsync();
        var missing = Path.Combine(Path.GetTempPath(), $"xihan-missing-{Guid.NewGuid():N}", "x.db");
        var definition = new DataSourceDefinition("down", DatabaseTypes.Sqlite, $"Data Source={missing};Mode=ReadOnly;Password=Schema-Secret-3");

        var listed = await Reader(host).ListTablesAsync(definition, new TableListQuery(), TestContext.Current.CancellationToken);
        var read = await Reader(host).GetTablesAsync(definition, ["anything"], TestContext.Current.CancellationToken);

        Assert.Equal(PersistenceErrors.DataSourceUnavailable, listed.Error?.Code);
        Assert.Equal(PersistenceMessages.SchemaReadFailed, listed.Error!.MessageKey);
        Assert.Equal("down", listed.Error.Details!["name"]);
        Assert.DoesNotContain("Schema-Secret-3", listed.Error.Message, StringComparison.Ordinal);
        Assert.Equal(PersistenceErrors.DataSourceUnavailable, read.Error?.Code);
    }

    [Fact]
    public async Task PostgreSql_ReadsComments_IdentityAndSerial_AndMixedCaseNames()
    {
        using var database = PostgresTestServer.CreateDatabase();
        await using var host = await StartHostAsync();
        var definition = new DataSourceDefinition("pg", DatabaseTypes.PostgreSql, database.ConnectionString);
        var client = (await host.Services.GetRequiredService<DataSourceRegistry>().GetOrAddAsync(definition, TestContext.Current.CancellationToken)).ValueOrThrow().GetSqlSugarClient();
        await client.Ado.ExecuteCommandAsync("""
            CREATE TABLE probe_items (id bigserial PRIMARY KEY, name varchar(50) NOT NULL DEFAULT 'x', amount numeric(18,2), created timestamptz);
            COMMENT ON TABLE probe_items IS '项目表';
            COMMENT ON COLUMN probe_items.name IS '名称';
            CREATE TABLE "LegacyOrder" ("Id" int GENERATED ALWAYS AS IDENTITY PRIMARY KEY, "Title" varchar(20));
            """);

        var batch = (await Reader(host).GetTablesAsync(definition, ["probe_items", "LegacyOrder"], TestContext.Current.CancellationToken)).ValueOrThrow();
        var items = batch[0].ValueOrThrow();
        var columns = items.Columns.ToDictionary(static c => c.Name);
        var legacy = batch[1].ValueOrThrow();

        Assert.Equal("项目表", items.Table.Comment);
        Assert.Equal("名称", columns["name"].Comment);
        Assert.Equal("int8", columns["id"].DataType);
        Assert.True(columns["id"].IsIdentity);
        Assert.StartsWith("nextval(", columns["id"].DefaultValue, StringComparison.Ordinal);
        Assert.Equal(50, columns["name"].Length);
        Assert.Equal(18, columns["amount"].Precision);
        Assert.Equal(2, columns["amount"].Scale);
        Assert.True(columns["created"].IsNullable);
        Assert.Equal("LegacyOrder", legacy.Table.Name);
        Assert.True(Assert.Single(legacy.Columns, static c => c.Name == "Id").IsIdentity);
        await host.Services.GetRequiredService<DataSourceRegistry>().RemoveAsync(["pg"], TestContext.Current.CancellationToken);
        PostgresTestServer.ClearPools();
    }

    /// <summary>
    /// 启动以应用库装配的宿主，可再登记实体。
    /// </summary>
    private Task<TestHost> StartHostAsync(Action<IServiceCollection>? configure = null) => TestHost.StartAsync(
        static builder => builder.UseFeature(SqliteDatabase.CreateFeature()),
        _application.Configuration,
        services =>
        {
            services.AddSingleton(TimeProvider.System);
            configure?.Invoke(services);
        });

    /// <summary>
    /// 取库结构读取器。
    /// </summary>
    private static ISchemaReader Reader(TestHost host) => host.Services.GetRequiredService<ISchemaReader>();

    /// <summary>
    /// 取实体目录。
    /// </summary>
    private static IEntityCatalog Catalog(TestHost host) => host.Services.GetRequiredService<IEntityCatalog>();

    /// <summary>
    /// 在应用库上执行一条语句（独立作用域，不经管道）。
    /// </summary>
    private static async Task ExecuteAsync(TestHost host, string sql)
    {
        await using var scope = host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISqlSugarClient>().Ado.ExecuteCommandAsync(sql);
    }

    /// <summary>
    /// 经仓储写入一条分表记录。
    /// </summary>
    private static async Task AddAsync(TestHost host, SplitTableRecord record)
        => (await host.InvokeAsync(async context => context.SetOutcome(await context.Services.GetRequiredService<IRepository<SplitTableRecord, Guid>>().AddAsync(record, context.CancellationToken)))).ThrowIfFailure();
}
