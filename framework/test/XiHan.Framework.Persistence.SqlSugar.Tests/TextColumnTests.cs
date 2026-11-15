// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using SqlSugar;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// Oracle 与 SQL Server 上 <c>[StringLength(n)]</c> 的列装得下 n 个汉字
/// </summary>
/// <remarks>
/// 环境变量 <c>XIHAN_TEST_ORACLE</c> / <c>XIHAN_TEST_SQLSERVER</c>，未设置即跳过。建表与之后加的列都是 Unicode、按字符计长的类型（<c>nvarchar2</c> / <c>nvarchar</c>）
/// </remarks>
public sealed class TextColumnTests
{
    private const string TenCharacters = "汉字汉字汉字汉字汉字";

    [Fact]
    public void Oracle_StringColumns_HoldTheirLengthInCharacters_WhenCreatedAndWhenAdded()
    {
        using var schema = OracleTestServer.CreateSchema();
        var client = RoundTrip(DbType.Oracle, schema.ConnectionString);
        var types = client.Ado.SqlQuery<string>("SELECT column_name || ' ' || data_type || ' ' || char_length FROM user_tab_columns WHERE table_name = 'TEXT_RECORD' ORDER BY column_name");
        Assert.Contains("NAME NVARCHAR2 10", types);
        Assert.Contains("NOTE NVARCHAR2 10", types);
    }

    [Fact]
    public void SqlServer_StringColumns_HoldTheirLengthInCharacters_WhenCreatedAndWhenAdded()
    {
        using var database = SqlServerTestServer.CreateDatabase();
        var client = RoundTrip(DbType.SqlServer, database.ConnectionString);
        var types = client.Ado.SqlQuery<string>("SELECT c.name + ' ' + t.name + ' ' + CAST(c.max_length / 2 AS varchar(10)) FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id WHERE c.object_id = OBJECT_ID('text_record')");
        Assert.Contains("Name nvarchar 10", types);
        Assert.Contains("Note nvarchar 10", types);
    }

    private static SqlSugarClient RoundTrip(DbType dbType, string connectionString)
    {
        var client = new SqlSugarClient(SqlSugarConventions.Apply(new ConnectionConfig { DbType = dbType, ConnectionString = connectionString, IsAutoCloseConnection = true }));

        SqlSugarConventions.InitTables(client, typeof(TextRecordV1));
        var first = new TextRecordV1 { Id = Guid.NewGuid(), Name = TenCharacters };
        client.Insertable(first).ExecuteCommand();

        SqlSugarConventions.InitTables(client, typeof(TextRecordV2));
        var second = new TextRecordV2 { Id = Guid.NewGuid(), Name = TenCharacters, Note = TenCharacters };
        client.Insertable(second).ExecuteCommand();

        Assert.Equal(TenCharacters, client.Queryable<TextRecordV2>().Single(r => r.Id == first.Id).Name);
        Assert.Equal(TenCharacters, client.Queryable<TextRecordV2>().Single(r => r.Id == second.Id).Note);
        return client;
    }
}
