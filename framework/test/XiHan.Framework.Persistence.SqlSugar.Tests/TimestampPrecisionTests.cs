// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using SqlSugar;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 日期时间列在各数据库上往返到微秒
/// </summary>
/// <remarks>
/// 按约定建表、写入带微秒的时刻再读回；同一秒里先后写入的两行按时间排序不乱。外部数据库各自的环境变量未设置即跳过
/// </remarks>
public sealed class TimestampPrecisionTests
{
    private static readonly DateTimeOffset _moment = new DateTimeOffset(2026, 11, 16, 1, 2, 3, TimeSpan.Zero).AddTicks(1_234_560);

    [Fact]
    public void Sqlite_RoundTripsMicroseconds()
    {
        var path = Path.Combine(Path.GetTempPath(), $"xihan-timestamp-{Guid.NewGuid():N}.db");
        try
        {
            RoundTrip(DbType.Sqlite, $"Data Source={path}");
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    [Fact]
    public void Postgres_RoundTripsMicroseconds()
    {
        using var database = PostgresTestServer.CreateDatabase();
        RoundTrip(DbType.PostgreSQL, database.ConnectionString);
    }

    [Fact]
    public void MySql_RoundTripsMicroseconds()
    {
        using var database = MySqlTestServer.CreateDatabase();
        var client = RoundTrip(DbType.MySql, database.ConnectionString);
        Assert.All(client.DbMaintenance.GetColumnInfosByTableName(nameof(TimestampRecord), false).Where(static c => c.DbColumnName != nameof(TimestampRecord.Id)), static c => Assert.Equal(SqlSugarConventions.TimestampPrecision, c.Length));
    }

    [Fact]
    public void SqlServer_RoundTripsMicroseconds()
    {
        using var database = SqlServerTestServer.CreateDatabase();
        RoundTrip(DbType.SqlServer, database.ConnectionString);
    }

    [Fact]
    public void Oracle_RoundTripsMicroseconds()
    {
        using var schema = OracleTestServer.CreateSchema();
        RoundTrip(DbType.Oracle, schema.ConnectionString);
    }

    private static SqlSugarClient RoundTrip(DbType dbType, string connectionString)
    {
        var client = new SqlSugarClient(SqlSugarConventions.Apply(new ConnectionConfig { DbType = dbType, ConnectionString = connectionString, IsAutoCloseConnection = true }));
        SqlSugarConventions.InitTables(client, typeof(TimestampRecord));

        var first = new TimestampRecord { Id = Guid.NewGuid(), At = _moment, Due = _moment.AddTicks(10), Local = _moment.UtcDateTime };
        var second = new TimestampRecord { Id = Guid.NewGuid(), At = _moment.AddTicks(10), Local = _moment.UtcDateTime.AddTicks(10) };
        client.Insertable(second).ExecuteCommand();
        client.Insertable(first).ExecuteCommand();

        var rows = client.Queryable<TimestampRecord>().OrderBy(static r => r.At).ToList();
        Assert.Equal([first.Id, second.Id], rows.Select(static r => r.Id));
        Assert.Equal(first.At, rows[0].At);
        Assert.Equal(first.Due, rows[0].Due);
        Assert.Equal(first.Local, rows[0].Local);
        Assert.Null(rows[1].Due);
        Assert.Equal(second.Local, rows[1].Local);
        return client;
    }
}
