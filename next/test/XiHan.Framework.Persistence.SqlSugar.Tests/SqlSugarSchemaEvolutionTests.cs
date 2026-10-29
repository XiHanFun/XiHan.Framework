// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using SqlSugar;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// <c>InitTables</c> 给已有行的表加列（ADR-0040）。
/// </summary>
/// <remarks>
/// PostgreSQL 上非可空的数值 / 枚举 / 布尔 / Guid 列靠约定配的 CLR 缺省值回填旧行；SQLite 由 SqlSugar 加成可空列，旧行读回 CLR 缺省值。两种数据库都要求：加列不失败、旧行读回 CLR 缺省值、新行原样往返、再次建表不失败。
/// </remarks>
public sealed class SqlSugarSchemaEvolutionTests
{
    /// <summary>
    /// PostgreSQL 客户端的 ConfigId。
    /// </summary>
    /// <remarks>
    /// SqlSugar 按类型与 ConfigId 缓存实体映射，两种数据库用不同的 ConfigId 才不会串用对方的列定义。
    /// </remarks>
    private const string PostgresConfigId = "evolution-pg";

    /// <summary>
    /// SQLite 客户端的 ConfigId。
    /// </summary>
    private const string SqliteConfigId = "evolution-sqlite";

    [Fact]
    public void Conventions_GiveNonNullableValueColumnsAClrDefault_OnPostgreSqlOnly()
    {
        // 只取映射、不连库：连接串不会被打开
        using var postgres = Client(DbType.PostgreSQL, "Host=localhost;Database=unused", PostgresConfigId);
        var columns = postgres.EntityMaintenance.GetEntityInfo<EvolvingRecordV2>().Columns.ToDictionary(static c => c.PropertyName, StringComparer.Ordinal);
        Assert.Equal("0", columns[nameof(EvolvingRecordV2.Kind)].DefaultValue);
        Assert.Equal("0", columns[nameof(EvolvingRecordV2.Count)].DefaultValue);
        Assert.Equal("0", columns[nameof(EvolvingRecordV2.Total)].DefaultValue);
        Assert.Equal("0", columns[nameof(EvolvingRecordV2.Amount)].DefaultValue);
        Assert.Equal("false", columns[nameof(EvolvingRecordV2.Enabled)].DefaultValue);
        Assert.Equal(Guid.Empty.ToString(), columns[nameof(EvolvingRecordV2.Ref)].DefaultValue);
        // 主键、非可空字符串、可空列不配缺省值
        Assert.Null(columns[nameof(EvolvingRecordV2.Id)].DefaultValue);
        Assert.Null(columns[nameof(EvolvingRecordV2.Name)].DefaultValue);
        Assert.Null(columns[nameof(EvolvingRecordV2.Note)].DefaultValue);
        Assert.Null(columns[nameof(EvolvingRecordV2.At)].DefaultValue);

        // SQLite 一个都不配：SqlSugar 在 SQLite 上加列本来就是可空列，配了缺省值反而走它的重建列流程、布尔列读回失败
        using var sqlite = Client(DbType.Sqlite, "Data Source=unused.db", SqliteConfigId);
        Assert.All(sqlite.EntityMaintenance.GetEntityInfo<EvolvingRecordV2>().Columns, static c => Assert.Null(c.DefaultValue));
    }

    [Fact]
    public void InitTables_AddsNonNullableColumns_ToAPopulatedTable_OnSqlite()
    {
        using var database = new SqliteDatabase();
        using var client = Client(DbType.Sqlite, database.ConnectionString, SqliteConfigId);
        EvolveAndVerify(client);
    }

    [Fact]
    public void InitTables_AddsNonNullableColumns_ToAPopulatedTable_OnPostgreSql()
    {
        using var database = PostgresTestServer.CreateDatabase();
        using var client = Client(DbType.PostgreSQL, database.ConnectionString, PostgresConfigId);
        EvolveAndVerify(client);
    }

    [Fact]
    public void DictionaryUpdate_KeepsColumnsNamedLikeDictionaryMembers()
    {
        // SqlSugar 对承载字典更新的 Dictionary<string, object> 也调用实体服务：约定若把它的 Count（没有公开 setter）标成忽略列，
        // 字典里名为 Count 的列会被丢掉、更新无列可写而报"need Primary key"。加列回填就是这样一次字典更新
        using var database = new SqliteDatabase();
        using var client = Client(DbType.Sqlite, database.ConnectionString, SqliteConfigId);
        client.CodeFirst.InitTables<EvolvingRecordV2>();
        var id = Guid.CreateVersion7();
        client.Insertable(new EvolvingRecordV2 { Id = id, Name = "row" }).ExecuteCommand();

        var count = client.EntityMaintenance.GetDbColumnName<EvolvingRecordV2>(nameof(EvolvingRecordV2.Count));
        var updated = client.Updateable(new Dictionary<string, object> { [count] = 3 }).AS("evolving_record").Where($"{count} = 0").ExecuteCommand();

        Assert.Equal(1, updated);
        Assert.Equal(3, client.Queryable<EvolvingRecordV2>().InSingle(id).Count);
    }

    /// <summary>
    /// 按协议约定配置的客户端。
    /// </summary>
    private static SqlSugarClient Client(DbType dbType, string connectionString, string configId)
        => new(SqlSugarConventions.Apply(new ConnectionConfig { DbType = dbType, ConnectionString = connectionString, ConfigId = configId, IsAutoCloseConnection = true }));

    /// <summary>
    /// 先按 V1 建表并写一行，再按 V2 建表。
    /// </summary>
    /// <remarks>
    /// 旧行读回 CLR 缺省值，新行原样往返，再次建表不失败。
    /// </remarks>
    private static void EvolveAndVerify(SqlSugarClient client)
    {
        var old = Guid.CreateVersion7();
        client.CodeFirst.InitTables<EvolvingRecordV1>();
        client.Insertable(new EvolvingRecordV1 { Id = old, Name = "old" }).ExecuteCommand();

        client.CodeFirst.InitTables<EvolvingRecordV2>();
        var evolved = client.Queryable<EvolvingRecordV2>().InSingle(old);
        Assert.Equal("old", evolved.Name);
        Assert.Equal(((EvolvingKind)0, 0, 0L, 0m, false, Guid.Empty), (evolved.Kind, evolved.Count, evolved.Total, evolved.Amount, evolved.Enabled, evolved.Ref));
        Assert.Null(evolved.Note);
        Assert.Null(evolved.At);

        // 缺省值只作用于建表与加列：写入照常带全部列的值
        var fresh = new EvolvingRecordV2
        {
            Id = Guid.CreateVersion7(),
            Name = "new",
            Kind = EvolvingKind.Two,
            Count = 7,
            Total = 9_000_000_000,
            Amount = 12.5m,
            Enabled = true,
            Ref = Guid.CreateVersion7(),
            Note = "note",
            At = new DateTimeOffset(2026, 10, 28, 8, 0, 0, TimeSpan.Zero),
        };
        client.Insertable(fresh).ExecuteCommand();
        var read = client.Queryable<EvolvingRecordV2>().InSingle(fresh.Id);
        Assert.Equal((fresh.Kind, fresh.Count, fresh.Total, fresh.Amount, fresh.Enabled, fresh.Ref, fresh.Note), (read.Kind, read.Count, read.Total, read.Amount, read.Enabled, read.Ref, read.Note));
        Assert.Equal(fresh.At, read.At);

        // 已经演进过的表再建一次不失败、不丢行
        client.CodeFirst.InitTables<EvolvingRecordV2>();
        Assert.Equal(2, client.Queryable<EvolvingRecordV2>().Count());
    }
}
