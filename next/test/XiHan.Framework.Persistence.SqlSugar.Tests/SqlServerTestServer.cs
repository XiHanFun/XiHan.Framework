// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Data.SqlClient;
using SqlSugar;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 真实 SQL Server 来自环境变量 <c>XIHAN_TEST_SQLSERVER</c>。
/// </summary>
/// <remarks>
/// 服务器连接串，不含 <c>Database</c>，如 <c>Server=127.0.0.1,14333;User Id=sa;Password=…;TrustServerCertificate=True</c>。未设置时跳过需要它的用例。
/// 只用于 SQL Server 方言独有的语句（筛选索引）在真实库上的验证；CI 不提供 SQL Server。
/// </remarks>
internal static class SqlServerTestServer
{
    /// <summary>
    /// 指向测试服务器的环境变量名。
    /// </summary>
    public const string EnvironmentVariable = "XIHAN_TEST_SQLSERVER";

    /// <summary>
    /// 在服务器上建一个随机名的库，返回指向它的连接串。
    /// </summary>
    /// <remarks>
    /// 未设置连接串时跳过当前用例。释放时删库。
    /// </remarks>
    public static SqlServerDatabase CreateDatabase()
    {
        var server = Environment.GetEnvironmentVariable(EnvironmentVariable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(server), $"Set the environment variable '{EnvironmentVariable}' to a SQL Server connection string (without Database) to run this test.");
        server = server!.TrimEnd(';');
        var name = $"xihan_sqlsugar_{Guid.NewGuid():N}";
        using (var maintenance = Maintenance(server))
        {
            maintenance.Ado.ExecuteCommand($"CREATE DATABASE [{name}]");
        }

        return new SqlServerDatabase(server, name);
    }

    /// <summary>
    /// 连到 <c>master</c> 的客户端，建库与删库用。
    /// </summary>
    public static SqlSugarClient Maintenance(string server)
        => new(new ConnectionConfig { DbType = DbType.SqlServer, ConnectionString = $"{server};Database=master", IsAutoCloseConnection = true });

    /// <summary>
    /// 释放本进程对该服务器的连接池：删库前要先断开。
    /// </summary>
    public static void ClearPools() => SqlConnection.ClearAllPools();
}
