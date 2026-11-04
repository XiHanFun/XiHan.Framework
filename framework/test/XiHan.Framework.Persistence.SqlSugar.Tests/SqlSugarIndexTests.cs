// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using SqlSugar;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// <c>[EntityIndex]</c> 到 SqlSugar 索引的映射（ADR-0072、ADR-0081）
/// </summary>
/// <remarks>
/// 经约定映射为 SqlSugar 索引，<c>InitTables</c> 建出（RFC-0005「索引声明」）
/// 名字按映射出的表名推出；唯一索引真实拒绝重复；软删除实体的唯一索引是只约束未删除行的部分唯一索引，由 <see cref="SqlSugarConventions.InitTables"/> 补建（SQLite、PostgreSQL、SQL Server），同一编码可以反复删除；再次建表不重复建；不支持部分索引的数据库在建表前与启动时失败；分表实体不支持
/// </remarks>
public sealed class SqlSugarIndexTests
{
    /// <summary>
    /// SQLite 客户端的 ConfigId
    /// </summary>
    /// <remarks>
    /// SqlSugar 按类型与 ConfigId 缓存实体映射，几种数据库用不同的 ConfigId 才不会串用对方的列定义
    /// </remarks>
    private const string SqliteConfigId = "index-sqlite";

    /// <summary>
    /// PostgreSQL 客户端的 ConfigId
    /// </summary>
    private const string PostgresConfigId = "index-pg";

    /// <summary>
    /// SQL Server 客户端的 ConfigId
    /// </summary>
    private const string SqlServerConfigId = "index-mssql";

    /// <summary>
    /// MySQL 客户端的 ConfigId（只取映射、不连库）
    /// </summary>
    private const string MySqlConfigId = "index-mysql";

    [Fact]
    public void Conventions_MapDeclaredIndexes_ToAscendingSugarIndexes_ExceptPartialOnes()
    {
        // 只取映射、不连库
        using var client = Client(DbType.Sqlite, "Data Source=unused.db", SqliteConfigId);

        var indexes = client.EntityMaintenance.GetEntityInfo<IndexedRecord>().Indexs;
        Assert.Equal(
            [("IX_indexed_record_Ca_At", false, "Category,At"), ("UX_indexed_record_TeId_Co", true, "TenantId,Code")],
            indexes.Select(static i => (i.IndexName, i.IsUnique, string.Join(',', i.IndexFields.Keys))).OrderBy(static i => i.IndexName, StringComparer.Ordinal));
        Assert.All(indexes.SelectMany(static i => i.IndexFields.Values), static order => Assert.Equal(OrderByType.Asc, order));

        // 软删除实体的部分唯一索引不交给 SqlSugar：它写不出条件，交给它就成了约束已删除行的普通唯一索引
        Assert.Empty(client.EntityMaintenance.GetEntityInfo<SoftDeletedIndexedRecord>().Indexs ?? []);
        // 显式把删除标记写进列的是普通唯一索引，照常交给 SqlSugar
        var explicitIndex = Assert.Single(client.EntityMaintenance.GetEntityInfo<ExplicitDeletedIndexedRecord>().Indexs);
        Assert.Equal(("UX_explicit_deleted_indexed_record_Co_IsDe", true, "Code,IsDeleted"), (explicitIndex.IndexName, explicitIndex.IsUnique, string.Join(',', explicitIndex.IndexFields.Keys)));
    }

    [Fact]
    public void InitTables_CreatesDeclaredIndexes_OnceOnSqlite()
    {
        using var database = new SqliteDatabase();
        using var client = Client(DbType.Sqlite, database.ConnectionString, SqliteConfigId);

        SqlSugarConventions.InitTables(client, typeof(IndexedRecord), typeof(SoftDeletedIndexedRecord), typeof(ExplicitDeletedIndexedRecord));
        // 再建一次：按名字查到已有的索引就跳过，不报"已存在"
        SqlSugarConventions.InitTables(client, typeof(IndexedRecord), typeof(SoftDeletedIndexedRecord), typeof(ExplicitDeletedIndexedRecord));

        Assert.Equal(
            [("IX_indexed_record_Ca_At", false, "Category,At"), ("UX_indexed_record_TeId_Co", true, "TenantId,Code")],
            SqliteSchema.Indexes(client, "indexed_record").Select(static i => (i.Name, i.IsUnique, string.Join(',', i.Columns))));
        // 部分唯一索引：列只有编码，条件是未删除
        Assert.Equal(
            [("UX_soft_deleted_indexed_record_Co", true, "Code")],
            SqliteSchema.Indexes(client, "soft_deleted_indexed_record").Select(static i => (i.Name, i.IsUnique, string.Join(',', i.Columns))));
        Assert.EndsWith("WHERE `IsDeleted` = 0", SqliteSchema.Sql(client, "UX_soft_deleted_indexed_record_Co"), StringComparison.Ordinal);
        // 显式写了删除标记的：普通唯一索引，没有条件
        Assert.Equal(
            [("UX_explicit_deleted_indexed_record_Co_IsDe", true, "Code,IsDeleted")],
            SqliteSchema.Indexes(client, "explicit_deleted_indexed_record").Select(static i => (i.Name, i.IsUnique, string.Join(',', i.Columns))));
        Assert.DoesNotContain("WHERE", SqliteSchema.Sql(client, "UX_explicit_deleted_indexed_record_Co_IsDe"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UniqueIndex_RejectsDuplicateKeys_OnSqlite()
    {
        using var database = new SqliteDatabase();
        using var client = Client(DbType.Sqlite, database.ConnectionString, SqliteConfigId);
        SqlSugarConventions.InitTables(client, typeof(IndexedRecord));

        client.Insertable(Record("acme", "A")).ExecuteCommand();
        // 另一个租户的同一编码不冲突
        client.Insertable(Record("other", "A")).ExecuteCommand();

        var error = Assert.ThrowsAny<Exception>(() => client.Insertable(Record("acme", "A")).ExecuteCommand());
        Assert.Contains("UNIQUE", error.Message, StringComparison.Ordinal);
        Assert.Equal(2, client.Queryable<IndexedRecord>().Count());
    }

    [Fact]
    public void PartialUniqueIndex_ConstrainsUndeletedRowsOnly_OnSqlite()
    {
        using var database = new SqliteDatabase();
        using var client = Client(DbType.Sqlite, database.ConnectionString, SqliteConfigId);
        SqlSugarConventions.InitTables(client, typeof(SoftDeletedIndexedRecord));

        ConstrainsUndeletedRowsOnly(client);
    }

    [Fact]
    public void PartialUniqueIndex_ConstrainsUndeletedRowsOnly_OnPostgreSql()
    {
        using var database = PostgresTestServer.CreateDatabase();
        using var client = Client(DbType.PostgreSQL, database.ConnectionString, PostgresConfigId);
        SqlSugarConventions.InitTables(client, typeof(SoftDeletedIndexedRecord));
        // 再建一次：IF NOT EXISTS 按小写的名字查到已有索引
        SqlSugarConventions.InitTables(client, typeof(SoftDeletedIndexedRecord));

        // 名字与 SqlSugar 建出的其他索引一样折成小写；条件是未删除
        var definition = Assert.Single(client.Ado.SqlQuery<string>("SELECT indexdef FROM pg_indexes WHERE tablename = 'soft_deleted_indexed_record' AND indexname NOT LIKE '%pkey'"));
        Assert.Contains("ux_soft_deleted_indexed_record_co", definition, StringComparison.Ordinal);
        Assert.Contains("WHERE", definition, StringComparison.Ordinal);
        ConstrainsUndeletedRowsOnly(client);
    }

    [Fact]
    public void PartialUniqueIndex_ConstrainsUndeletedRowsOnly_OnSqlServer()
    {
        using var database = SqlServerTestServer.CreateDatabase();
        using var client = Client(DbType.SqlServer, database.ConnectionString, SqlServerConfigId);
        SqlSugarConventions.InitTables(client, typeof(SoftDeletedIndexedRecord));
        // 再建一次：按名字与表查 sys.indexes，查到即跳过
        SqlSugarConventions.InitTables(client, typeof(SoftDeletedIndexedRecord));

        var filter = Assert.Single(client.Ado.SqlQuery<string>("SELECT filter_definition FROM sys.indexes WHERE name = 'UX_soft_deleted_indexed_record_Co' AND has_filter = 1"));
        Assert.Contains("[IsDeleted]", filter, StringComparison.Ordinal);
        ConstrainsUndeletedRowsOnly(client);
    }

    [Fact]
    public void InitTables_OnADatabaseWithoutPartialIndexes_FailsBeforeCreatingTables()
    {
        // 只取映射、不连库：核对在建表之前，MySQL 上不会建出缺了唯一约束的表
        using var client = Client(DbType.MySql, "Server=127.0.0.1;Port=1;Database=unused;Uid=unused;Pwd=unused", MySqlConfigId);

        var error = Assert.Throws<FrameworkException>(() => SqlSugarConventions.InitTables(client, typeof(IndexedRecord), typeof(SoftDeletedIndexedRecord)));

        Assert.Equal(PersistenceErrors.NotSupported, error.Code);
        Assert.Contains(typeof(SoftDeletedIndexedRecord).FullName!, error.Message, StringComparison.Ordinal);
        Assert.Contains("MySql", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(typeof(IndexedRecord).FullName + "'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Startup_OnADatabaseWithoutPartialIndexes_Fails_EvenWithoutInitTables()
    {
        // 生产库不开 InitTables、由迁移脚本建表：唯一约束同样建不出来，启动时就失败，不等写入时才发现
        var feature = new SqlSugarPersistenceFeature(static (_, config) =>
        {
            config.DbType = DbType.MySql;
            config.ConnectionString = "Server=127.0.0.1;Port=1;Database=unused;Uid=unused;Pwd=unused";
        });

        var error = await Assert.ThrowsAsync<FrameworkException>(() => TestHost.StartAsync(
            builder => builder.UseFeature(feature),
            [],
            static services =>
            {
                services.AddRepository<IndexedRecord, Guid>();
                services.AddRepository<SoftDeletedIndexedRecord, Guid>();
                services.AddRepository<ExplicitDeletedIndexedRecord, Guid>();
            }));

        // 宿主把特性启动失败包成 FeatureStartFailed，不支持的原因在内部异常里，只列出要部分索引的实体
        var cause = Assert.IsType<FrameworkException>(error.InnerException);
        Assert.Equal(PersistenceErrors.NotSupported, cause.Code);
        Assert.Contains(typeof(SoftDeletedIndexedRecord).FullName!, cause.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(typeof(ExplicitDeletedIndexedRecord).FullName!, cause.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplicitDeletionFlag_NeedsNoPartialIndex()
    {
        // 显式把删除标记写进列是普通唯一索引：不支持部分索引的数据库上可以用，代价是同一编码最多一行已删除
        Assert.False(PartialUniqueIndexes.Declares(typeof(ExplicitDeletedIndexedRecord)));
        Assert.True(PartialUniqueIndexes.Declares(typeof(SoftDeletedIndexedRecord)));
        Assert.False(PartialUniqueIndexes.Declares(typeof(IndexedRecord)));
    }

    [Fact]
    public void InitTables_CreatesDeclaredIndexes_OnceOnPostgreSql()
    {
        using var database = PostgresTestServer.CreateDatabase();
        using var client = Client(DbType.PostgreSQL, database.ConnectionString, PostgresConfigId);

        SqlSugarConventions.InitTables(client, typeof(IndexedRecord), typeof(SoftDeletedIndexedRecord));
        // PostgreSQL 把不加引号的索引名折成小写，SqlSugar 按不区分大小写查已有索引，再建一次不撞名
        SqlSugarConventions.InitTables(client, typeof(IndexedRecord), typeof(SoftDeletedIndexedRecord));

        Assert.Equal(
            ["ix_indexed_record_ca_at", "ux_indexed_record_teid_co"],
            client.Ado.SqlQuery<string>("SELECT indexname FROM pg_indexes WHERE tablename = 'indexed_record' AND indexname NOT LIKE '%pkey' ORDER BY indexname"));
        client.Insertable(Record("acme", "A")).ExecuteCommand();
        Assert.ThrowsAny<Exception>(() => client.Insertable(Record("acme", "A")).ExecuteCommand());
    }

    [Fact]
    public void SplitTableEntity_DeclaringIndexes_IsRejected()
    {
        using var client = Client(DbType.Sqlite, "Data Source=unused.db", SqliteConfigId);

        var error = Assert.Throws<NotSupportedException>(() => client.EntityMaintenance.GetEntityInfo<SplitIndexedRecord>());
        Assert.Contains("[SugarIndex]", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 同一编码：已删除的行任意多行，未删除的行之间唯一
    /// </summary>
    private static void ConstrainsUndeletedRowsOnly(SqlSugarClient client)
    {
        // 已删除的行不受约束：同一编码可以有任意多行已删除
        client.Insertable(Soft("A", deleted: true)).ExecuteCommand();
        client.Insertable(Soft("A", deleted: true)).ExecuteCommand();
        client.Insertable(Soft("A", deleted: false)).ExecuteCommand();

        // 未删除的行之间唯一
        Assert.ThrowsAny<Exception>(() => client.Insertable(Soft("A", deleted: false)).ExecuteCommand());
        Assert.Equal(3, client.Queryable<SoftDeletedIndexedRecord>().Count());
    }

    /// <summary>
    /// 按协议约定配置的客户端
    /// </summary>
    private static SqlSugarClient Client(DbType dbType, string connectionString, string configId)
        => new(SqlSugarConventions.Apply(new ConnectionConfig { DbType = dbType, ConnectionString = connectionString, ConfigId = configId, IsAutoCloseConnection = true }));

    /// <summary>
    /// 一行记录
    /// </summary>
    private static IndexedRecord Record(string tenantId, string code)
        => new() { Id = Guid.CreateVersion7(), TenantId = tenantId, Code = code, Category = "c", At = new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.Zero) };

    /// <summary>
    /// 一行软删除实体
    /// </summary>
    private static SoftDeletedIndexedRecord Soft(string code, bool deleted)
        => new() { Id = Guid.CreateVersion7(), Code = code, IsDeleted = deleted, DeletedAt = deleted ? new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.Zero) : null };
}
