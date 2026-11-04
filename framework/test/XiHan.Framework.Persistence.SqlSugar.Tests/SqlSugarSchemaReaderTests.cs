// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.DataSources;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.Schema;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// SqlSugar 的库结构读取与实体目录
/// </summary>
/// <remarks>
/// SQLite 上的列属性与限制（没有注释、主键归一为不可空、长度与精度取自声明）、分表折叠、实体目录的表名与索引、运行时数据源读外部库与读取失败；PostgreSQL 上的注释、identity、serial 缺省值与大小写混合的表名（<c>XIHAN_TEST_POSTGRES</c>）；MySQL、Oracle、SQL Server 上的类型名、长度只给字符与二进制类型、精度与小数位、主键、自增、注释、缺省值、大小写混合的表名、分表折叠与分页总数（<c>XIHAN_TEST_MYSQL</c>、<c>XIHAN_TEST_ORACLE</c>、<c>XIHAN_TEST_SQLSERVER</c>）
/// </remarks>
public sealed class SqlSugarSchemaReaderTests : IDisposable
{
    /// <summary>
    /// 应用自己的库
    /// </summary>
    private readonly SqliteDatabase _application = new();

    /// <summary>
    /// 作为外部库的另一个 SQLite 文件
    /// </summary>
    private readonly SqliteDatabase _external = new();

    /// <summary>
    /// 测试结束后删除两个临时库
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

    [Fact]
    public async Task MySql_ReadsTypesKeysCommentsAndDefaults_FoldsSplitTables_AndPages()
    {
        using var database = MySqlTestServer.CreateDatabase();
        await using var host = await StartHostAsync(static services => services.AddRepository<SplitTableRecord, Guid>());
        var definition = new DataSourceDefinition("mysql", DatabaseTypes.MySql, database.ConnectionString);
        var client = (await host.Services.GetRequiredService<DataSourceRegistry>().GetOrAddAsync(definition, TestContext.Current.CancellationToken)).ValueOrThrow().GetSqlSugarClient();
        await client.Ado.ExecuteCommandAsync("""
            CREATE TABLE probe_items (id bigint NOT NULL AUTO_INCREMENT PRIMARY KEY, name varchar(50) NOT NULL DEFAULT 'x' COMMENT '名称', amount decimal(18,2) NULL, note text, flag tinyint(1) NOT NULL DEFAULT 0, created datetime(6) NULL, code char(8), payload varbinary(16)) COMMENT='项目表';
            CREATE TABLE `LegacyOrder` (`Id` int NOT NULL PRIMARY KEY, `Title` varchar(20));
            CREATE TABLE split_records_20260101 (Id char(36) PRIMARY KEY, Text varchar(10));
            CREATE TABLE split_records_20260201 (Id char(36) PRIMARY KEY, Text varchar(10), Extra int);
            """);

        var batch = (await Reader(host).GetTablesAsync(definition, ["probe_items", "legacyorder", "split_records"], TestContext.Current.CancellationToken)).ValueOrThrow();
        var items = batch[0].ValueOrThrow();
        var columns = items.Columns.ToDictionary(static c => c.Name);

        Assert.Equal("项目表", items.Table.Comment);
        Assert.Equal("名称", columns["name"].Comment);
        Assert.Equal(("bigint", true, true, false, (int?)null), (columns["id"].DataType, columns["id"].IsPrimaryKey, columns["id"].IsIdentity, columns["id"].IsNullable, columns["id"].Length));
        Assert.Equal(("varchar", (int?)50, false, "x"), (columns["name"].DataType, columns["name"].Length, columns["name"].IsNullable, columns["name"].DefaultValue));
        Assert.Equal(("decimal", (int?)18, (int?)2, (int?)null, true), (columns["amount"].DataType, columns["amount"].Precision, columns["amount"].Scale, columns["amount"].Length, columns["amount"].IsNullable));
        // 长度只给字符与二进制类型：显示宽度（tinyint(1)）与小数秒精度（datetime(6)）不是长度
        Assert.Null(columns["note"].Length);
        Assert.Equal(("tinyint", (int?)null, "0"), (columns["flag"].DataType, columns["flag"].Length, columns["flag"].DefaultValue));
        Assert.Equal(("datetime", (int?)null, (int?)null), (columns["created"].DataType, columns["created"].Length, columns["created"].Precision));
        Assert.Equal(8, columns["code"].Length);
        Assert.Equal(16, columns["payload"].Length);
        AssertLegacyAndSplit(batch, "LegacyOrder", "Title", "Extra");
        await AssertPagingAsync(host, definition, ["LegacyOrder", "probe_items", "split_records"], "项目");
        await host.Services.GetRequiredService<DataSourceRegistry>().RemoveAsync(["mysql"], TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Oracle_ReadsTypesKeysCommentsAndDefaults_FoldsSplitTables_AndPages()
    {
        using var schema = OracleTestServer.CreateSchema();
        await using var host = await StartHostAsync(static services => services.AddRepository<SplitTableRecord, Guid>());
        var definition = new DataSourceDefinition("oracle", DatabaseTypes.Oracle, schema.ConnectionString);
        var client = (await host.Services.GetRequiredService<DataSourceRegistry>().GetOrAddAsync(definition, TestContext.Current.CancellationToken)).ValueOrThrow().GetSqlSugarClient();
        foreach (var statement in new[]
        {
            "CREATE TABLE probe_items (id NUMBER(19) GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY, name VARCHAR2(50) DEFAULT 'x' NOT NULL, amount NUMBER(18,2), note CLOB, created TIMESTAMP, code CHAR(8), nname NVARCHAR2(30), payload RAW(16), cnt NUMBER)",
            "COMMENT ON TABLE probe_items IS '项目表'",
            "COMMENT ON COLUMN probe_items.name IS '名称'",
            "CREATE TABLE \"LegacyOrder\" (\"Id\" NUMBER(10) PRIMARY KEY, \"Title\" VARCHAR2(20))",
            "CREATE TABLE split_records_20260101 (Id CHAR(36) PRIMARY KEY, Text VARCHAR2(10))",
            "CREATE TABLE split_records_20260201 (Id CHAR(36) PRIMARY KEY, Text VARCHAR2(10), Extra NUMBER(10))",
        })
        {
            await client.Ado.ExecuteCommandAsync(statement);
        }

        var batch = (await Reader(host).GetTablesAsync(definition, ["probe_items", "LegacyOrder", "split_records"], TestContext.Current.CancellationToken)).ValueOrThrow();
        var items = batch[0].ValueOrThrow();
        var columns = items.Columns.ToDictionary(static c => c.Name);

        // 不加引号的名字在 Oracle 里是大写；类型名是数据库的（不含长度与精度），不是 CLR 类型名
        Assert.Equal("PROBE_ITEMS", items.Table.Name);
        Assert.Equal("项目表", items.Table.Comment);
        Assert.Equal(["ID", "NAME", "AMOUNT", "NOTE", "CREATED", "CODE", "NNAME", "PAYLOAD", "CNT"], items.Columns.Select(static c => c.Name));
        Assert.Equal(Enumerable.Range(0, 9), items.Columns.Select(static c => c.Ordinal));
        Assert.Equal(("NUMBER", true, true, false, (int?)19, (int?)0), (columns["ID"].DataType, columns["ID"].IsPrimaryKey, columns["ID"].IsIdentity, columns["ID"].IsNullable, columns["ID"].Precision, columns["ID"].Scale));
        Assert.Contains("nextval", columns["ID"].DefaultValue, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(("VARCHAR2", (int?)50, false, "'x'", "名称"), (columns["NAME"].DataType, columns["NAME"].Length, columns["NAME"].IsNullable, columns["NAME"].DefaultValue, columns["NAME"].Comment));
        Assert.Equal(("NUMBER", (int?)18, (int?)2, (int?)null, true), (columns["AMOUNT"].DataType, columns["AMOUNT"].Precision, columns["AMOUNT"].Scale, columns["AMOUNT"].Length, columns["AMOUNT"].IsNullable));
        Assert.Equal(("CLOB", (int?)null), (columns["NOTE"].DataType, columns["NOTE"].Length));
        Assert.Equal(("TIMESTAMP", (int?)null, (int?)null), (columns["CREATED"].DataType, columns["CREATED"].Length, columns["CREATED"].Precision));
        Assert.Equal(("CHAR", (int?)8), (columns["CODE"].DataType, columns["CODE"].Length));
        // NVARCHAR2 的长度是字符数，不是字节数
        Assert.Equal(("NVARCHAR2", (int?)30), (columns["NNAME"].DataType, columns["NNAME"].Length));
        Assert.Equal(("RAW", (int?)16), (columns["PAYLOAD"].DataType, columns["PAYLOAD"].Length));
        // 不带精度的 NUMBER 没有精度与小数位
        Assert.Equal(("NUMBER", (int?)null, (int?)null), (columns["CNT"].DataType, columns["CNT"].Precision, columns["CNT"].Scale));
        Assert.All(items.Columns.Where(static c => c.Name != "NAME"), static c => Assert.Null(c.Comment));
        AssertLegacyAndSplit(batch, "LegacyOrder", "Title", "EXTRA");
        await AssertPagingAsync(host, definition, ["LegacyOrder", "PROBE_ITEMS", "split_records"], "项目");
        await host.Services.GetRequiredService<DataSourceRegistry>().RemoveAsync(["oracle"], TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SqlServer_ReadsTypesKeysCommentsAndDefaults_FoldsSplitTables_AndPages()
    {
        using var database = SqlServerTestServer.CreateDatabase();
        await using var host = await StartHostAsync(static services => services.AddRepository<SplitTableRecord, Guid>());
        var definition = new DataSourceDefinition("mssql", DatabaseTypes.SqlServer, database.ConnectionString);
        var client = (await host.Services.GetRequiredService<DataSourceRegistry>().GetOrAddAsync(definition, TestContext.Current.CancellationToken)).ValueOrThrow().GetSqlSugarClient();
        await client.Ado.ExecuteCommandAsync("""
            CREATE TABLE probe_items (id bigint IDENTITY(1,1) PRIMARY KEY, name nvarchar(50) NOT NULL DEFAULT 'x', amount decimal(18,2) NULL, note nvarchar(max), created datetime2, code char(8), flag bit NOT NULL DEFAULT 0, price money, payload varbinary(16));
            EXEC sp_addextendedproperty 'MS_Description', N'项目表', 'SCHEMA', 'dbo', 'TABLE', 'probe_items';
            EXEC sp_addextendedproperty 'MS_Description', N'名称', 'SCHEMA', 'dbo', 'TABLE', 'probe_items', 'COLUMN', 'name';
            CREATE TABLE [LegacyOrder] ([Id] int PRIMARY KEY, [Title] varchar(20));
            CREATE TABLE split_records_20260101 (Id char(36) PRIMARY KEY, Text varchar(10));
            CREATE TABLE split_records_20260201 (Id char(36) PRIMARY KEY, Text varchar(10), Extra int);
            """);

        var batch = (await Reader(host).GetTablesAsync(definition, ["probe_items", "legacyorder", "split_records"], TestContext.Current.CancellationToken)).ValueOrThrow();
        var items = batch[0].ValueOrThrow();
        var columns = items.Columns.ToDictionary(static c => c.Name);

        Assert.Equal("项目表", items.Table.Comment);
        Assert.Equal("名称", columns["name"].Comment);
        Assert.Equal(("bigint", true, true, false, (int?)null), (columns["id"].DataType, columns["id"].IsPrimaryKey, columns["id"].IsIdentity, columns["id"].IsNullable, columns["id"].Length));
        Assert.Equal(("nvarchar", (int?)50, false, "('x')"), (columns["name"].DataType, columns["name"].Length, columns["name"].IsNullable, columns["name"].DefaultValue));
        Assert.Equal(((int?)18, (int?)2, (int?)null), (columns["amount"].Precision, columns["amount"].Scale, columns["amount"].Length));
        // nvarchar(max) 没有上限；日期、bit 的长度不是长度
        Assert.Null(columns["note"].Length);
        Assert.Equal(("datetime2", (int?)null), (columns["created"].DataType, columns["created"].Length));
        Assert.Equal(("bit", (int?)null, "((0))"), (columns["flag"].DataType, columns["flag"].Length, columns["flag"].DefaultValue));
        Assert.Equal(("money", (int?)19, (int?)4), (columns["price"].DataType, columns["price"].Precision, columns["price"].Scale));
        Assert.Equal(8, columns["code"].Length);
        Assert.Equal(16, columns["payload"].Length);
        AssertLegacyAndSplit(batch, "LegacyOrder", "Title", "Extra");
        await AssertPagingAsync(host, definition, ["LegacyOrder", "probe_items", "split_records"], "项目");
        await host.Services.GetRequiredService<DataSourceRegistry>().RemoveAsync(["mssql"], TestContext.Current.CancellationToken);
        SqlServerTestServer.ClearPools();
    }

    /// <summary>
    /// 核对大小写混合的表与折叠后的分表
    /// </summary>
    /// <remarks>
    /// <paramref name="batch"/> 依次是项目表、大小写混合的表、分表的逻辑名；分表的列取最新的物理表（多一列 <paramref name="extra"/>）
    /// </remarks>
    private static void AssertLegacyAndSplit(IReadOnlyList<Result<TableSchema>> batch, string legacy, string title, string extra)
    {
        var legacySchema = batch[1].ValueOrThrow();
        Assert.Equal(legacy, legacySchema.Table.Name);
        Assert.Equal(20, Assert.Single(legacySchema.Columns, c => c.Name == title).Length);
        Assert.Single(legacySchema.Columns, static c => c.IsPrimaryKey);
        var split = batch[2].ValueOrThrow();
        Assert.Equal("split_records", split.Table.Name);
        Assert.True(split.Table.IsSplit);
        Assert.Equal(2, split.Table.PhysicalTables.Count);
        Assert.EndsWith("20260201", split.Table.PhysicalTables[0], StringComparison.Ordinal);
        Assert.Contains(split.Columns, c => c.Name == extra);
    }

    /// <summary>
    /// 核对表列表的排序、分页总数与按注释的关键字
    /// </summary>
    /// <remarks>
    /// <paramref name="names"/> 是库里按名字（不区分大小写）排好的全部表，分表已折叠
    /// </remarks>
    private static async Task AssertPagingAsync(TestHost host, DataSourceDefinition definition, string[] names, string commentKeyword)
    {
        var all = (await Reader(host).ListTablesAsync(definition, new TableListQuery(), TestContext.Current.CancellationToken)).ValueOrThrow();
        var page = (await Reader(host).ListTablesAsync(definition, new TableListQuery { Skip = 1, Take = 1 }, TestContext.Current.CancellationToken)).ValueOrThrow();
        var commented = (await Reader(host).ListTablesAsync(definition, new TableListQuery { Keyword = commentKeyword }, TestContext.Current.CancellationToken)).ValueOrThrow();

        Assert.Equal(names, all.Items.Select(static t => t.Name));
        Assert.Equal(names.Length, all.Total);
        Assert.Equal((names.Length, names[1]), (page.Total, Assert.Single(page.Items).Name));
        Assert.Equal((1, names[1]), (commented.Total, Assert.Single(commented.Items).Name));
    }

    /// <summary>
    /// 启动以应用库装配的宿主，可再登记实体
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
    /// 取库结构读取器
    /// </summary>
    private static ISchemaReader Reader(TestHost host) => host.Services.GetRequiredService<ISchemaReader>();

    /// <summary>
    /// 取实体目录
    /// </summary>
    private static IEntityCatalog Catalog(TestHost host) => host.Services.GetRequiredService<IEntityCatalog>();

    /// <summary>
    /// 在应用库上执行一条语句（独立作用域，不经管道）
    /// </summary>
    private static async Task ExecuteAsync(TestHost host, string sql)
    {
        await using var scope = host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISqlSugarClient>().Ado.ExecuteCommandAsync(sql);
    }

    /// <summary>
    /// 经仓储写入一条分表记录
    /// </summary>
    private static async Task AddAsync(TestHost host, SplitTableRecord record)
        => (await host.InvokeAsync(async context => context.SetOutcome(await context.Services.GetRequiredService<IRepository<SplitTableRecord, Guid>>().AddAsync(record, context.CancellationToken)))).ThrowIfFailure();
}
