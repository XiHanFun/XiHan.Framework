// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using MySqlConnector;
using SqlSugar;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 真实 MySQL 来自环境变量 <c>XIHAN_TEST_MYSQL</c>
/// </summary>
/// <remarks>
/// 服务器连接串，不含 <c>Database</c>，如 <c>Server=127.0.0.1;Port=3306;User Id=root;Password=…</c>；账号要能建库与删库。未设置时跳过需要它的用例。用于库结构读取与一致性套件在真实 MySQL 上的验证；CI 不提供 MySQL
/// </remarks>
internal static class MySqlTestServer
{
    /// <summary>
    /// 指向测试服务器的环境变量名
    /// </summary>
    public const string EnvironmentVariable = "XIHAN_TEST_MYSQL";

    /// <summary>
    /// 在服务器上建一个随机名的库，返回指向它的连接串
    /// </summary>
    /// <remarks>
    /// 未设置连接串时跳过当前用例。释放时删库
    /// </remarks>
    public static MySqlDatabase CreateDatabase()
    {
        var server = Environment.GetEnvironmentVariable(EnvironmentVariable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(server), $"Set the environment variable '{EnvironmentVariable}' to a MySQL server connection string (without Database) to run this test.");
        server = server!.TrimEnd(';');
        var name = $"xihan_sqlsugar_{Guid.NewGuid():N}";
        using (var maintenance = Maintenance(server))
        {
            maintenance.Ado.ExecuteCommand($"CREATE DATABASE `{name}` CHARACTER SET utf8mb4");
        }

        return new MySqlDatabase(server, name);
    }

    /// <summary>
    /// 连到系统库 <c>mysql</c> 的客户端，建库与删库用
    /// </summary>
    public static SqlSugarClient Maintenance(string server)
        => new(new ConnectionConfig { DbType = DbType.MySql, ConnectionString = $"{server};Database=mysql", IsAutoCloseConnection = true });

    /// <summary>
    /// 释放本进程对该服务器的连接池：删库前要先断开
    /// </summary>
    public static void ClearPools() => MySqlConnection.ClearAllPools();
}
