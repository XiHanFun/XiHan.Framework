// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Npgsql;
using SqlSugar;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 真实 PostgreSQL 来自环境变量 <c>XIHAN_TEST_POSTGRES</c>。
/// </summary>
/// <remarks>
/// 服务器连接串，不含 <c>Database</c>，如 <c>Host=127.0.0.1;Port=5432;Username=postgres;Password=postgres</c>。未设置时跳过需要它的用例。SQLite 存文本、按 UTC 解析，Npgsql 的 legacy 时间戳模式只在真库上暴露，所以一致性套件要在 PostgreSQL 上再跑一遍。
/// </remarks>
internal static class PostgresTestServer
{
    /// <summary>
    /// 指向测试服务器的环境变量名。
    /// </summary>
    public const string EnvironmentVariable = "XIHAN_TEST_POSTGRES";

    /// <summary>
    /// 环境变量里的服务器连接串。
    /// </summary>
    /// <remarks>
    /// 未设置为 <see langword="null"/>。
    /// </remarks>
    public static string? Configuration => Environment.GetEnvironmentVariable(EnvironmentVariable);

    /// <summary>
    /// 返回服务器连接串。
    /// </summary>
    /// <remarks>
    /// 未设置时跳过当前用例。
    /// </remarks>
    public static string RequireConfiguration()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(Configuration), $"Set the environment variable '{EnvironmentVariable}' to a PostgreSQL server connection string (without Database) to run this test.");
        return Configuration!;
    }

    /// <summary>
    /// 在服务器上建一个随机名的库。
    /// </summary>
    /// <remarks>
    /// 未设置连接串时跳过当前用例。
    /// </remarks>
    public static PostgresDatabase CreateDatabase()
    {
        var server = RequireConfiguration().TrimEnd(';');
        var name = $"xihan_sqlsugar_{Guid.NewGuid():N}";
        using (var maintenance = Maintenance(server))
        {
            maintenance.Ado.ExecuteCommand($"CREATE DATABASE \"{name}\"");
        }

        return new PostgresDatabase(server, name);
    }

    /// <summary>
    /// 连到维护库 <c>postgres</c> 的客户端，建库与删库用。
    /// </summary>
    public static SqlSugarClient Maintenance(string server)
        => new(new ConnectionConfig { DbType = DbType.PostgreSQL, ConnectionString = $"{server};Database=postgres", IsAutoCloseConnection = true });

    /// <summary>
    /// 释放本进程对该服务器的连接池：删库前要先断开。
    /// </summary>
    public static void ClearPools() => NpgsqlConnection.ClearAllPools();
}
