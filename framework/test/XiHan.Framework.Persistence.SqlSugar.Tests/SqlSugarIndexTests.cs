// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using SqlSugar;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// <c>[EntityIndex]</c> 到 SqlSugar 索引的映射（ADR-0072、ADR-0081、ADR-0249）
/// </summary>
/// <remarks>
/// 经约定映射为 SqlSugar 索引，<c>InitTables</c> 建出（RFC-0005「索引声明」）
/// 名字按映射出的表名推出；唯一索引真实拒绝重复；软删除实体的唯一索引是只约束未删除行的部分唯一索引，由 <see cref="SqlSugarConventions.InitTables"/> 补建（SQLite、PostgreSQL、SQL Server 带条件，MySQL、Oracle 是函数索引），同一编码可以反复删除；显式的名字原样使用，两个实体同名在建表前失败；再次建表不重复建；不支持部分索引的数据库在建表前与启动时失败；分表实体不支持
/// </remarks>
public sealed class SqlSugarIndexTests
{
    /// <summary>
    /// SQLite 客户端的 ConfigId
    /// </summary>
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
    /// MySQL 客户端的 ConfigId
    /// </summary>
    private const string MySqlConfigId = "index-mysql";

    /// <summary>
    /// Oracle 客户端的 ConfigId
    /// </summary>
    private const string OracleConfigId = "index-oracle";

    /// <summary>
    /// 达梦客户端的 ConfigId（只取映射、不连库）
    /// </summary>
    private const string DmConfigId = "index-dm";

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
    public void PartialUniqueIndex_ConstrainsUndeletedRowsOnly_OnMySql()
    {
        using var database = MySqlTestServer.CreateDatabase();
        using var client = Client(DbType.MySql, database.ConnectionString, MySqlConfigId);
        SqlSugarConventions.InitTables(client, typeof(SoftDeletedIndexedRecord));
        // 再建一次：MySQL 没有 IF NOT EXISTS，先按表与名字查到已有索引就跳过
        SqlSugarConventions.InitTables(client, typeof(SoftDeletedIndexedRecord));

        // 函数索引：唯一一个键是表达式，不是列
        var expression = Assert.Single(client.Ado.SqlQuery<string>("SELECT expression FROM information_schema.statistics WHERE table_schema = DATABASE() AND index_name = 'UX_soft_deleted_indexed_record_Co'"));
        Assert.Contains("case when", expression, StringComparison.OrdinalIgnoreCase);
        ConstrainsUndeletedRowsOnly(client);
    }

    [Fact]
    public void PartialUniqueIndex_ConstrainsUndeletedRowsOnly_OnOracle()
    {
        using var schema = OracleTestServer.CreateSchema();
        using var client = Client(DbType.Oracle, schema.ConnectionString, OracleConfigId);
        SqlSugarConventions.InitTables(client, typeof(SoftDeletedIndexedRecord));
        // 再建一次：先按大写的名字查到已有索引就跳过
        SqlSugarConventions.InitTables(client, typeof(SoftDeletedIndexedRecord));

        // 名字不加引号、存成大写；函数索引
        Assert.Equal("FUNCTION-BASED NORMAL", Assert.Single(client.Ado.SqlQuery<string>("SELECT index_type FROM user_indexes WHERE index_name = 'UX_SOFT_DELETED_INDEXED_RECORD_CO'")));
        ConstrainsUndeletedRowsOnly(client);
    }

    [Fact]
    public void ExplicitlyNamedPartialUniqueIndex_SkipsRowsWithANullKey_OnSqlite()
    {
        using var database = new SqliteDatabase();
        using var client = Client(DbType.Sqlite, database.ConnectionString, SqliteConfigId);
        SqlSugarConventions.InitTables(client, typeof(NamedSoftDeletedRecord));
        SqlSugarConventions.InitTables(client, typeof(NamedSoftDeletedRecord));

        // 显式的名字原样使用
        Assert.Equal(
            [("ux_named_soft_deleted_record_code_region", true, "Code,Region")],
            SqliteSchema.Indexes(client, "named_soft_deleted_record").Select(static i => (i.Name, i.IsUnique, string.Join(',', i.Columns))));
        SkipsRowsWithANullKey(client);
    }

    [Fact]
    public void ExplicitlyNamedPartialUniqueIndex_SkipsRowsWithANullKey_OnPostgreSql()
    {
        using var database = PostgresTestServer.CreateDatabase();
        using var client = Client(DbType.PostgreSQL, database.ConnectionString, PostgresConfigId);
        SqlSugarConventions.InitTables(client, typeof(NamedSoftDeletedRecord));
        SqlSugarConventions.InitTables(client, typeof(NamedSoftDeletedRecord));

        Assert.Equal(["ux_named_soft_deleted_record_code_region"], client.Ado.SqlQuery<string>("SELECT indexname FROM pg_indexes WHERE tablename = 'named_soft_deleted_record' AND indexname NOT LIKE '%pkey'"));
        SkipsRowsWithANullKey(client);
    }

    [Fact]
    public void ExplicitlyNamedPartialUniqueIndex_SkipsRowsWithANullKey_OnSqlServer()
    {
        using var database = SqlServerTestServer.CreateDatabase();
        using var client = Client(DbType.SqlServer, database.ConnectionString, SqlServerConfigId);
        SqlSugarConventions.InitTables(client, typeof(NamedSoftDeletedRecord));
        SqlSugarConventions.InitTables(client, typeof(NamedSoftDeletedRecord));

        Assert.Single(client.Ado.SqlQuery<string>("SELECT name FROM sys.indexes WHERE name = 'ux_named_soft_deleted_record_code_region' AND has_filter = 1"));
        SkipsRowsWithANullKey(client);
    }

    [Fact]
    public void ExplicitlyNamedPartialUniqueIndex_SkipsRowsWithANullKey_OnMySql()
    {
        using var database = MySqlTestServer.CreateDatabase();
        using var client = Client(DbType.MySql, database.ConnectionString, MySqlConfigId);
        SqlSugarConventions.InitTables(client, typeof(NamedSoftDeletedRecord));
        SqlSugarConventions.InitTables(client, typeof(NamedSoftDeletedRecord));

        // 两列各一个表达式键
        Assert.Equal(2, client.Ado.GetInt("SELECT COUNT(1) FROM information_schema.statistics WHERE table_schema = DATABASE() AND index_name = 'ux_named_soft_deleted_record_code_region' AND expression IS NOT NULL"));
        SkipsRowsWithANullKey(client);
    }

    [Fact]
    public void ExplicitlyNamedPartialUniqueIndex_SkipsRowsWithANullKey_OnOracle()
    {
        using var schema = OracleTestServer.CreateSchema();
        using var client = Client(DbType.Oracle, schema.ConnectionString, OracleConfigId);
        SqlSugarConventions.InitTables(client, typeof(NamedSoftDeletedRecord));
        SqlSugarConventions.InitTables(client, typeof(NamedSoftDeletedRecord));

        Assert.Single(client.Ado.SqlQuery<string>("SELECT index_name FROM user_indexes WHERE index_name = 'UX_NAMED_SOFT_DELETED_RECORD_CODE_REGION'"));
        // Oracle 的组合键有一列不为空就比较：可空的键列写进条件，区域为空的行整键为空、不受约束
        SkipsRowsWithANullKey(client);
    }

    [Fact]
    public void InitTables_WithTwoEntitiesSharingAnIndexName_FailsBeforeCreatingTables()
    {
        using var database = new SqliteDatabase();
        using var client = Client(DbType.Sqlite, database.ConnectionString, SqliteConfigId);

        // SQLite 的索引名按库唯一：后建的那个按 IF NOT EXISTS 静默跳过，那张表就少了索引
        var error = Assert.Throws<InvalidOperationException>(() => SqlSugarConventions.InitTables(client, typeof(SameIndexNameRecordA), typeof(SameIndexNameRecordB)));

        Assert.Contains(typeof(SameIndexNameRecordA).FullName!, error.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(SameIndexNameRecordB).FullName!, error.Message, StringComparison.Ordinal);
        Assert.Contains("'ix_same_index_name_code'", error.Message, StringComparison.Ordinal);
        Assert.False(client.DbMaintenance.IsAnyTable("same_index_name_record_a", false));
        // 同一实体写了两次不算重名
        SqlSugarConventions.InitTables(client, typeof(SameIndexNameRecordA), typeof(SameIndexNameRecordA));
        Assert.True(client.DbMaintenance.IsAnyTable("same_index_name_record_a", false));
    }

    [Fact]
    public void InitTables_OnADatabaseWithoutPartialIndexes_FailsBeforeCreatingTables()
    {
        // 只取映射、不连库：核对在建表之前，达梦上不会建出缺了唯一约束的表
        using var client = Client(DbType.Dm, "Server=127.0.0.1;Port=1;User Id=unused;PWD=unused", DmConfigId);

        var error = Assert.Throws<FrameworkException>(() => SqlSugarConventions.InitTables(client, typeof(IndexedRecord), typeof(SoftDeletedIndexedRecord)));

        Assert.Equal(PersistenceErrors.NotSupported, error.Code);
        Assert.Contains(typeof(SoftDeletedIndexedRecord).FullName!, error.Message, StringComparison.Ordinal);
        Assert.Contains("Dm", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(typeof(IndexedRecord).FullName + "'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Startup_OnADatabaseWithoutPartialIndexes_Fails_EvenWithoutInitTables()
    {
        // 生产库不开 InitTables、由迁移脚本建表：唯一约束同样建不出来，启动时就失败，不等写入时才发现
        var feature = new SqlSugarPersistenceFeature(static (_, config) =>
        {
            config.DbType = DbType.Dm;
            config.ConnectionString = "Server=127.0.0.1;Port=1;User Id=unused;PWD=unused";
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
    /// 键含可空列：区域为空的行不受约束，区域不为空的未删除行之间唯一
    /// </summary>
    private static void SkipsRowsWithANullKey(SqlSugarClient client)
    {
        client.Insertable(Named("A", null, deleted: false)).ExecuteCommand();
        client.Insertable(Named("A", null, deleted: false)).ExecuteCommand();
        client.Insertable(Named("A", "east", deleted: true)).ExecuteCommand();
        client.Insertable(Named("A", "east", deleted: false)).ExecuteCommand();

        Assert.ThrowsAny<Exception>(() => client.Insertable(Named("A", "east", deleted: false)).ExecuteCommand());
        Assert.Equal(4, client.Queryable<NamedSoftDeletedRecord>().Count());
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
    /// 一行显式命名索引的软删除实体
    /// </summary>
    private static NamedSoftDeletedRecord Named(string code, string? region, bool deleted)
        => new() { Id = Guid.CreateVersion7(), Code = code, Region = region, IsDeleted = deleted, DeletedAt = deleted ? new DateTimeOffset(2026, 11, 20, 8, 0, 0, TimeSpan.Zero) : null };

    /// <summary>
    /// 一行软删除实体
    /// </summary>
    private static SoftDeletedIndexedRecord Soft(string code, bool deleted)
        => new() { Id = Guid.CreateVersion7(), Code = code, IsDeleted = deleted, DeletedAt = deleted ? new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.Zero) : null };
}
