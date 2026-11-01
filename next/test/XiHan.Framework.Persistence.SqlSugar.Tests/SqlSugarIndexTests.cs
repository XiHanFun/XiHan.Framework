// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using SqlSugar;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// <c>[EntityIndex]</c> 到 SqlSugar 索引的映射（ADR-0072）。
/// </summary>
/// <remarks>
/// 经约定映射为 SqlSugar 索引，<c>InitTables</c> 建出（RFC-0005「索引声明」）。
/// 名字按映射出的表名推出；唯一索引真实拒绝重复；软删除实体的唯一索引末尾带删除标记；再次建表不重复建；分表实体不支持。
/// </remarks>
public sealed class SqlSugarIndexTests
{
    /// <summary>
    /// SQLite 客户端的 ConfigId。
    /// </summary>
    /// <remarks>
    /// SqlSugar 按类型与 ConfigId 缓存实体映射，两种数据库用不同的 ConfigId 才不会串用对方的列定义。
    /// </remarks>
    private const string SqliteConfigId = "index-sqlite";

    /// <summary>
    /// PostgreSQL 客户端的 ConfigId。
    /// </summary>
    private const string PostgresConfigId = "index-pg";

    [Fact]
    public void Conventions_MapDeclaredIndexes_ToAscendingSugarIndexes()
    {
        // 只取映射、不连库
        using var client = Client(DbType.Sqlite, "Data Source=unused.db", SqliteConfigId);

        var indexes = client.EntityMaintenance.GetEntityInfo<IndexedRecord>().Indexs;
        Assert.Equal(
            [("IX_indexed_record_Ca_At", false, "Category,At"), ("UX_indexed_record_TeId_Co", true, "TenantId,Code")],
            indexes.Select(static i => (i.IndexName, i.IsUnique, string.Join(',', i.IndexFields.Keys))).OrderBy(static i => i.IndexName, StringComparer.Ordinal));
        Assert.All(indexes.SelectMany(static i => i.IndexFields.Values), static order => Assert.Equal(OrderByType.Asc, order));

        // 软删除实体的唯一索引末尾带删除标记，名字只按声明的列
        var soft = Assert.Single(client.EntityMaintenance.GetEntityInfo<SoftDeletedIndexedRecord>().Indexs);
        Assert.Equal(("UX_soft_deleted_indexed_record_Co", true, "Code,IsDeleted"), (soft.IndexName, soft.IsUnique, string.Join(',', soft.IndexFields.Keys)));
    }

    [Fact]
    public void InitTables_CreatesDeclaredIndexes_OnceOnSqlite()
    {
        using var database = new SqliteDatabase();
        using var client = Client(DbType.Sqlite, database.ConnectionString, SqliteConfigId);

        client.CodeFirst.InitTables(typeof(IndexedRecord), typeof(SoftDeletedIndexedRecord));
        // 再建一次：按名字查到已有的索引就跳过，不报"已存在"
        client.CodeFirst.InitTables(typeof(IndexedRecord), typeof(SoftDeletedIndexedRecord));

        Assert.Equal(
            [("IX_indexed_record_Ca_At", false, "Category,At"), ("UX_indexed_record_TeId_Co", true, "TenantId,Code")],
            SqliteSchema.Indexes(client, "indexed_record").Select(static i => (i.Name, i.IsUnique, string.Join(',', i.Columns))));
        Assert.Equal(
            [("UX_soft_deleted_indexed_record_Co", true, "Code,IsDeleted")],
            SqliteSchema.Indexes(client, "soft_deleted_indexed_record").Select(static i => (i.Name, i.IsUnique, string.Join(',', i.Columns))));
    }

    [Fact]
    public void UniqueIndex_RejectsDuplicateKeys_OnSqlite()
    {
        using var database = new SqliteDatabase();
        using var client = Client(DbType.Sqlite, database.ConnectionString, SqliteConfigId);
        client.CodeFirst.InitTables<IndexedRecord>();

        client.Insertable(Record("acme", "A")).ExecuteCommand();
        // 另一个租户的同一编码不冲突
        client.Insertable(Record("other", "A")).ExecuteCommand();

        var error = Assert.ThrowsAny<Exception>(() => client.Insertable(Record("acme", "A")).ExecuteCommand());
        Assert.Contains("UNIQUE", error.Message, StringComparison.Ordinal);
        Assert.Equal(2, client.Queryable<IndexedRecord>().Count());
    }

    [Fact]
    public void UniqueIndex_OfSoftDeletableEntity_SkipsDeletedRows_ButKeepsOneDeletedRowPerKey()
    {
        using var database = new SqliteDatabase();
        using var client = Client(DbType.Sqlite, database.ConnectionString, SqliteConfigId);
        client.CodeFirst.InitTables<SoftDeletedIndexedRecord>();

        // 删掉的行不占编码：同一编码可以再建一行未删除的
        client.Insertable(new SoftDeletedIndexedRecord { Id = Guid.CreateVersion7(), Code = "A", IsDeleted = true, DeletedAt = DateTimeOffset.UtcNow }).ExecuteCommand();
        client.Insertable(new SoftDeletedIndexedRecord { Id = Guid.CreateVersion7(), Code = "A" }).ExecuteCommand();

        // 未删除的行之间唯一
        Assert.ThrowsAny<Exception>(() => client.Insertable(new SoftDeletedIndexedRecord { Id = Guid.CreateVersion7(), Code = "A" }).ExecuteCommand());
        // 代价：同一编码最多一行已删除
        Assert.ThrowsAny<Exception>(() => client.Insertable(new SoftDeletedIndexedRecord { Id = Guid.CreateVersion7(), Code = "A", IsDeleted = true, DeletedAt = DateTimeOffset.UtcNow }).ExecuteCommand());
        Assert.Equal(2, client.Queryable<SoftDeletedIndexedRecord>().Count());
    }

    [Fact]
    public void InitTables_CreatesDeclaredIndexes_OnceOnPostgreSql()
    {
        using var database = PostgresTestServer.CreateDatabase();
        using var client = Client(DbType.PostgreSQL, database.ConnectionString, PostgresConfigId);

        client.CodeFirst.InitTables(typeof(IndexedRecord), typeof(SoftDeletedIndexedRecord));
        // PostgreSQL 把不加引号的索引名折成小写，SqlSugar 按不区分大小写查已有索引，再建一次不撞名
        client.CodeFirst.InitTables(typeof(IndexedRecord), typeof(SoftDeletedIndexedRecord));

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
    /// 按协议约定配置的客户端。
    /// </summary>
    private static SqlSugarClient Client(DbType dbType, string connectionString, string configId)
        => new(SqlSugarConventions.Apply(new ConnectionConfig { DbType = dbType, ConnectionString = connectionString, ConfigId = configId, IsAutoCloseConnection = true }));

    /// <summary>
    /// 一行记录。
    /// </summary>
    private static IndexedRecord Record(string tenantId, string code)
        => new() { Id = Guid.CreateVersion7(), TenantId = tenantId, Code = code, Category = "c", At = new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.Zero) };
}
