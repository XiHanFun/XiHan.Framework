// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Npgsql;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 真实的 PostgreSQL 流复制从库来自环境变量 <c>XIHAN_TEST_POSTGRES_REPLICA</c>
/// </summary>
/// <remarks>
/// 与 <c>XIHAN_TEST_POSTGRES</c> 同样的写法（不带 <c>Database</c>），指向它的流复制从库；任何一个没设置就跳过。主库开 <c>synchronous_commit = remote_apply</c>，提交返回时从库已经读得到，用例不用等复制
/// </remarks>
internal static class PostgresReplicaServer
{
    /// <summary>
    /// 环境变量名
    /// </summary>
    public const string EnvironmentVariable = "XIHAN_TEST_POSTGRES_REPLICA";

    /// <summary>
    /// 返回从库的服务器连接串
    /// </summary>
    /// <remarks>
    /// 未设置时跳过当前用例
    /// </remarks>
    public static string RequireConfiguration()
    {
        var configuration = Environment.GetEnvironmentVariable(EnvironmentVariable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(configuration), $"Set the environment variable '{EnvironmentVariable}' to the connection string of a streaming replica of '{PostgresTestServer.EnvironmentVariable}' (without Database) to run this test.");
        return configuration!.TrimEnd(';');
    }

    /// <summary>
    /// 从库上指向这个库的连接串，等到库复制过来才返回
    /// </summary>
    public static async Task<string> ConnectionStringAsync(PostgresDatabase database)
    {
        var connectionString = $"{RequireConfiguration()};Database={database.Name}";
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();
                return connectionString;
            }
            catch (PostgresException) when (attempt < 100)
            {
                // 建库的 WAL 还没回放到从库
                await Task.Delay(100);
            }
        }
    }
}
