// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Data.Common;
using Oracle.ManagedDataAccess.Client;
using SqlSugar;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 真实 Oracle 来自环境变量 <c>XIHAN_TEST_ORACLE</c>。
/// </summary>
/// <remarks>
/// 有建用户权限的连接串，如 <c>Data Source=127.0.0.1:1521/FREEPDB1;User Id=system;Password=…</c>。Oracle 没有「建库」，每个用例建一个随机名的用户（即 schema），读结构读的是连接用户自己的表。未设置时跳过需要它的用例；CI 不提供 Oracle。
/// </remarks>
internal static class OracleTestServer
{
    /// <summary>
    /// 指向测试服务器的环境变量名。
    /// </summary>
    public const string EnvironmentVariable = "XIHAN_TEST_ORACLE";

    /// <summary>
    /// 建一个随机名的用户，返回以它登录的连接串。
    /// </summary>
    /// <remarks>
    /// 未设置连接串时跳过当前用例。释放时删用户（连同它的表）。
    /// </remarks>
    public static OracleSchema CreateSchema()
    {
        var admin = Environment.GetEnvironmentVariable(EnvironmentVariable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(admin), $"Set the environment variable '{EnvironmentVariable}' to an Oracle connection string of a user that can create users to run this test.");
        var user = $"XIHAN_{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        var password = $"P{Guid.NewGuid():N}"[..20];
        using (var maintenance = Maintenance(admin!))
        {
            maintenance.Ado.ExecuteCommand($"CREATE USER {user} IDENTIFIED BY \"{password}\" QUOTA UNLIMITED ON USERS");
            maintenance.Ado.ExecuteCommand($"GRANT CREATE SESSION, CREATE TABLE, CREATE SEQUENCE TO {user}");
        }

        var builder = new DbConnectionStringBuilder { ConnectionString = admin };
        builder["User Id"] = user;
        builder["Password"] = password;
        // 不用连接池：删用户时不能有它的会话，池里闲着的连接会挡住 DROP USER（ORA-01940）
        builder["Pooling"] = "false";
        return new OracleSchema(admin!, user, builder.ConnectionString);
    }

    /// <summary>
    /// 以有建用户权限的账号连接的客户端，建用户与删用户用。
    /// </summary>
    public static SqlSugarClient Maintenance(string admin)
        => new(new ConnectionConfig { DbType = DbType.Oracle, ConnectionString = admin, IsAutoCloseConnection = true });

    /// <summary>
    /// 释放本进程的 Oracle 连接池：删用户前要先断开。
    /// </summary>
    public static void ClearPools() => OracleConnection.ClearAllPools();
}
