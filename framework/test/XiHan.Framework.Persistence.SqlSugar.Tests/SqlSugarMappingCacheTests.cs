// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using SqlSugar;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 实体映射缓存按数据库类型分开
/// </summary>
/// <remarks>
/// SqlSugar 按实体类型与 <c>ConfigId</c> 缓存实体映射、进程内共用，约定的列定义却随数据库类型而变（ADR-0248）：<c>ConfigId</c> 相同的不同库各用各的映射，不论哪个先建；同类型的库共用一份；数据库类型在 <see cref="SqlSugarConventions.Apply"/> 之后才设也按它。只取映射、不连库
/// </remarks>
public sealed class SqlSugarMappingCacheTests
{
    /// <summary>
    /// 不连库的 SQLite 连接串
    /// </summary>
    private const string SqliteConnectionString = "Data Source=:memory:";

    /// <summary>
    /// 不连库的 SQL Server 连接串
    /// </summary>
    private const string SqlServerConnectionString = "Server=127.0.0.1,1;Database=unused";

    [Fact]
    public void SqlServer_AfterSqlite_OnTheSameConfigId_GetsItsOwnMapping()
    {
        const string configId = "mapping-sqlite-first";
        var sqlite = Mapping(DbType.Sqlite, SqliteConnectionString, configId);
        var sqlServer = Mapping(DbType.SqlServer, SqlServerConnectionString, configId);

        AssertSqlite(sqlite);
        AssertSqlServer(sqlServer);
    }

    [Fact]
    public void Sqlite_AfterSqlServer_OnTheSameConfigId_GetsItsOwnMapping()
    {
        const string configId = "mapping-sqlserver-first";
        var sqlServer = Mapping(DbType.SqlServer, SqlServerConnectionString, configId);
        var sqlite = Mapping(DbType.Sqlite, SqliteConnectionString, configId);

        AssertSqlServer(sqlServer);
        AssertSqlite(sqlite);
    }

    [Fact]
    public void Connections_OfTheSameDatabaseType_ShareOneMapping()
    {
        const string configId = "mapping-shared";
        var first = Mapping(DbType.SqlServer, SqlServerConnectionString, configId);
        var second = Mapping(DbType.SqlServer, SqlServerConnectionString, configId);

        Assert.Same(first, second);
    }

    [Fact]
    public void DatabaseType_SetAfterApply_DecidesTheMapping()
    {
        var config = SqlSugarConventions.Apply(new ConnectionConfig { ConfigId = "mapping-late-type", ConnectionString = SqlServerConnectionString, IsAutoCloseConnection = true });
        config.DbType = DbType.SqlServer;
        using var client = new SqlSugarClient(config);

        AssertSqlServer(client.EntityMaintenance.GetEntityInfo<MappingCacheRecord>());
    }

    /// <summary>
    /// 按协议约定配置的客户端取到的实体映射
    /// </summary>
    private static EntityInfo Mapping(DbType dbType, string connectionString, string configId)
    {
        using var client = new SqlSugarClient(SqlSugarConventions.Apply(new ConnectionConfig { DbType = dbType, ConnectionString = connectionString, ConfigId = configId, IsAutoCloseConnection = true }));
        return client.EntityMaintenance.GetEntityInfo<MappingCacheRecord>();
    }

    /// <summary>
    /// SQLite 的列定义：时刻按缺省的转换器写入，字符串不指定列类型
    /// </summary>
    private static void AssertSqlite(EntityInfo entity)
    {
        Assert.Equal(typeof(DateTimeOffsetConverter), Column(entity, nameof(MappingCacheRecord.At)).SqlParameterDbType);
        Assert.True(string.IsNullOrEmpty(Column(entity, nameof(MappingCacheRecord.Name)).DataType));
    }

    /// <summary>
    /// SQL Server 的列定义：时刻按 datetime2 写入，字符串是 nvarchar
    /// </summary>
    private static void AssertSqlServer(EntityInfo entity)
    {
        Assert.Equal(typeof(SqlServerDateTimeOffsetConverter), Column(entity, nameof(MappingCacheRecord.At)).SqlParameterDbType);
        Assert.Equal("nvarchar", Column(entity, nameof(MappingCacheRecord.Name)).DataType);
    }

    /// <summary>
    /// 按属性名取列
    /// </summary>
    private static EntityColumnInfo Column(EntityInfo entity, string property)
        => Assert.Single(entity.Columns, column => string.Equals(column.PropertyName, property, StringComparison.Ordinal));
}
