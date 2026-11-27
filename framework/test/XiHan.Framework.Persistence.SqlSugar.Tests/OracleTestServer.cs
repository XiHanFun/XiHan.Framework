// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Data.Common;
using System.Diagnostics;
using Oracle.ManagedDataAccess.Client;
using SqlSugar;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 真实 Oracle 来自环境变量 <c>XIHAN_TEST_ORACLE</c>
/// </summary>
/// <remarks>
/// 有建用户权限的连接串，如 <c>Data Source=127.0.0.1:1521/FREEPDB1;User Id=system;Password=…</c>。Oracle 没有「建库」，每个用例建一个随机名的用户（即 schema），读结构读的是连接用户自己的表。未设置时跳过需要它的用例；CI 不提供 Oracle。
/// 用户的连接串用连接池：不用池时每次操作都新开一个专用服务器进程，监听程序把交出去的连接都算作负载，没到 <c>PROCESSES</c> 上限就拒绝新连接（<c>ORA-12516</c>，ADR-0350）
/// </remarks>
internal static class OracleTestServer
{
    /// <summary>
    /// 指向测试服务器的环境变量名
    /// </summary>
    public const string EnvironmentVariable = "XIHAN_TEST_ORACLE";

    /// <summary>
    /// 管理连接失败之后等多久再试
    /// </summary>
    /// <remarks>
    /// 监听程序拒绝新连接（<c>ORA-12516</c>）要等实例下一次报告负载才解除，实测约半秒（ADR-0350）
    /// </remarks>
    private static readonly TimeSpan _retryDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// 删用户前最多等它的会话结束多久
    /// </summary>
    private static readonly TimeSpan _sessionWait = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 等会话结束时查询的间隔
    /// </summary>
    private static readonly TimeSpan _sessionPoll = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// 建一个随机名的用户，返回以它登录的连接串
    /// </summary>
    /// <remarks>
    /// 未设置连接串时跳过当前用例。释放时删用户（连同它的表）
    /// </remarks>
    public static OracleSchema CreateSchema()
    {
        var admin = Environment.GetEnvironmentVariable(EnvironmentVariable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(admin), $"Set the environment variable '{EnvironmentVariable}' to an Oracle connection string of a user that can create users to run this test.");
        return CreateSchema(admin!);
    }

    /// <summary>
    /// 以 <paramref name="admin"/> 建一个随机名的用户，管理连接失败时等两秒、清池再试一次
    /// </summary>
    /// <remarks>
    /// 与 <c>Workflow.Database.Tests</c> 的 Oracle 夹具同一处理（ADR-0338、ADR-0350）：CI 上管理连接失败（<c>ORA-50201</c>，内层是监听程序暂时拒绝新连接的 <c>ORA-12516</c>）之后，紧接着的再试与后面的用例都在几毫秒内失败，失败时等 <see cref="_retryDelay"/>、清掉这个连接串的池、换一个用户名再试一次；只试一次，再失败以 <see cref="AggregateException"/> 报告两次的异常（第一次在前）。只是测试夹具的处理，不改 Provider
    /// </remarks>
    /// <exception cref="AggregateException">两次都失败</exception>
    public static OracleSchema CreateSchema(string admin)
    {
        try
        {
            return CreateUser(admin);
        }
        catch (Exception first) when (first is SqlSugarException or OracleException)
        {
            // SqlSugar 把打开连接的失败包成不带内部异常的 SqlSugarException，执行语句的失败是 OracleException：两种都再试
            // 先等监听程序解除拒绝：立即再试仍落在同一段拒绝里；清池免得再拿到池里断了的连接
            Thread.Sleep(_retryDelay);
            using (var pooled = new OracleConnection(admin))
            {
                OracleConnection.ClearPool(pooled);
            }

            try
            {
                return CreateUser(admin);
            }
            catch (Exception second)
            {
                throw new AggregateException("Creating the Oracle test user failed, and failed again after waiting and clearing the connection pool.", first, second);
            }
        }
    }

    /// <summary>
    /// 建一个随机名的用户并授权，返回以它登录的夹具
    /// </summary>
    private static OracleSchema CreateUser(string admin)
    {
        var user = $"XIHAN_{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        var password = $"P{Guid.NewGuid():N}"[..20];
        using (var maintenance = Maintenance(admin))
        {
            maintenance.Ado.ExecuteCommand($"CREATE USER {user} IDENTIFIED BY \"{password}\" QUOTA UNLIMITED ON USERS");
            maintenance.Ado.ExecuteCommand($"GRANT CREATE SESSION, CREATE TABLE, CREATE SEQUENCE TO {user}");
        }

        var builder = new DbConnectionStringBuilder { ConnectionString = admin };
        builder["User Id"] = user;
        builder["Password"] = password;
        return new OracleSchema(admin, user, builder.ConnectionString);
    }

    /// <summary>
    /// 以有建用户权限的账号连接的客户端，建用户与删用户用
    /// </summary>
    public static SqlSugarClient Maintenance(string admin)
        => new(new ConnectionConfig { DbType = DbType.Oracle, ConnectionString = admin, IsAutoCloseConnection = true });

    /// <summary>
    /// 释放本进程的 Oracle 连接池：删用户前要先断开
    /// </summary>
    public static void ClearPools() => OracleConnection.ClearAllPools();

    /// <summary>
    /// 结束 <paramref name="user"/> 的全部会话，等它们从 <c>v$session</c> 里消失
    /// </summary>
    /// <remarks>
    /// 清池断开的连接与 <c>KILL SESSION … IMMEDIATE</c> 都不等会话真正结束；在那之前 DROP USER 报 ORA-01940（ADR-0350）。最多等 <see cref="_sessionWait"/>，到时仍有会话就交给 DROP USER 报错
    /// </remarks>
    public static void EndSessions(SqlSugarClient maintenance, string user)
    {
        ArgumentNullException.ThrowIfNull(maintenance);
        var started = Stopwatch.GetTimestamp();
        while (true)
        {
            // 已标成 KILLED 的不再杀一次，只等它消失；期间新出现的会话（后台探针刚连上）照样结束
            foreach (var session in maintenance.Ado.SqlQuery<string>("SELECT sid || ',' || serial# FROM v$session WHERE username = :u AND status <> 'KILLED'", new SugarParameter(":u", user)))
            {
                maintenance.Ado.ExecuteCommand($"ALTER SYSTEM KILL SESSION '{session}' IMMEDIATE");
            }

            if (maintenance.Ado.GetInt("SELECT COUNT(*) FROM v$session WHERE username = :u", new SugarParameter(":u", user)) == 0
                || Stopwatch.GetElapsedTime(started) >= _sessionWait)
            {
                return;
            }

            Thread.Sleep(_sessionPoll);
        }
    }
}
