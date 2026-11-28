// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Data.Common;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Oracle.ManagedDataAccess.Client;
using SqlSugar;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.SqlSugar;
using XiHan.Framework.Testing;
using XiHan.Framework.Workflow.Abstractions;

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// 每个测试一个临时 Oracle 用户，宿主装配 SqlSugar 持久化与数据库工作流特性
/// </summary>
/// <remarks>
/// 服务器来自环境变量 <see cref="EnvironmentVariable"/>（有建用户权限的连接串，如 <c>User Id=system;Password=…;Data Source=127.0.0.1:1521/FREEPDB1</c>），未设置即跳过，由 CI 的 oracle 分片运行。Oracle 没有「建库」，每个用例建一个随机名的用户，表建在它自己的 schema 里；缺省的 <c>NLS_COMP</c> 与 <c>NLS_SORT</c> 是 <c>BINARY</c>，<c>LIKE</c> 按码元比较，不必另定排序规则。空串存成 <c>NULL</c>，回填按 RFC-0021 把两者当作相同。行级锁，租约表不必放到另一个库。临时用户的连接串用连接池：不用池时每次操作都新开一个专用服务器进程，监听程序把交出去的连接都算作负载，没到 <c>PROCESSES</c> 上限就拒绝新连接（<c>ORA-12516</c>，ADR-0350）。释放时停止宿主、锁账户、清池、结束它的会话并删用户
/// </remarks>
internal sealed class OracleSchema : IGatedDatabase
{
    /// <summary>
    /// 指向测试服务器的环境变量名
    /// </summary>
    public const string EnvironmentVariable = "XIHAN_TEST_ORACLE";

    /// <summary>
    /// 要结束的会话已经不存在（ORA-00030）
    /// </summary>
    private const int SessionMissing = 30;

    /// <summary>
    /// 会话一时结束不了，已标成 KILLED（ORA-00031）
    /// </summary>
    /// <remarks>
    /// 带 <c>IMMEDIATE</c> 也会这样回；会话随后自己消失（ADR-0350 修订）
    /// </remarks>
    private const int SessionMarkedForKill = 31;

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
    /// 有建用户权限的连接串
    /// </summary>
    private readonly string _admin;

    /// <summary>
    /// 以临时用户登录的连接串
    /// </summary>
    private readonly string _connectionString;

    /// <summary>
    /// 已启动的宿主
    /// </summary>
    /// <remarks>
    /// 释放时一并停止
    /// </remarks>
    private TestHost? _host;

    /// <summary>
    /// 建好临时用户
    /// </summary>
    private OracleSchema(string admin, string user, string connectionString)
    {
        _admin = admin;
        _connectionString = connectionString;
        User = user;
    }

    /// <summary>
    /// 临时用户名（即 schema）
    /// </summary>
    public string User { get; }

    /// <summary>
    /// 在环境变量指向的服务器上建一个随机名的用户
    /// </summary>
    /// <remarks>
    /// 未设置环境变量时跳过当前用例
    /// </remarks>
    public static OracleSchema Create()
    {
        var admin = Environment.GetEnvironmentVariable(EnvironmentVariable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(admin), $"Set the environment variable '{EnvironmentVariable}' to an Oracle connection string of a user that can create users to run this test.");
        return Create(admin!);
    }

    /// <summary>
    /// 以 <paramref name="admin"/> 在服务器上建一个随机名的用户，管理连接失败时等两秒、清池再试一次
    /// </summary>
    /// <remarks>
    /// CI 上管理连接失败（<c>ORA-50201</c>，内层是监听程序暂时拒绝新连接的 <c>ORA-12516</c>）之后，紧接着的再试与后面的用例都在几毫秒内失败，落在同一段拒绝里（ADR-0338、ADR-0350）：失败时等 <see cref="_retryDelay"/>、清掉这个连接串的池、换一个用户名再试一次。只试一次；再失败以 <see cref="AggregateException"/> 报告两次的异常（第一次在前）。只是测试夹具的处理，不改 Provider
    /// </remarks>
    /// <exception cref="AggregateException">两次都失败</exception>
    internal static OracleSchema Create(string admin)
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

    /// <inheritdoc />
    public async Task<TestHost> StartHostAsync(IEnumerable<KeyValuePair<string, string?>>? configuration = null, Action<IServiceCollection>? services = null, Action<AppBuilder>? configure = null)
    {
        // 内存配置不接受重复键：后给的覆盖默认的
        var settings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["XiHan:Bindings:db"] = _connectionString,
            ["XiHan:Persistence:SqlSugar:InitTables"] = "true",
            [$"{DatabaseWorkflowOptions.ConfigurationSection}:PollIntervalSeconds"] = "3600",
            [$"{WorkflowOptions.ConfigurationSection}:LockTimeoutSeconds"] = "2",
            // 每个用例新建用户、在空库上建全部工作流表：CI 上 Oracle Free 的 DDL 偶尔超过缺省的 30 秒启动期限，宿主启动被取消
            ["XiHan:Kernel:StartTimeoutSeconds"] = "180",
        };
        foreach (var pair in configuration ?? [])
        {
            settings[pair.Key] = pair.Value;
        }

        _host = await TestHost.StartAsync(b =>
        {
            b.UseFeature(new SqlSugarPersistenceFeature(static (binding, config) =>
            {
                config.DbType = DbType.Oracle;
                config.ConnectionString = binding.ValueOrThrow().Value;
            })).UseFeature<DatabaseWorkflowFeature>();
            configure?.Invoke(b);
        }, settings, services);
        return _host;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 停止宿主之后先锁账户再清池：清池之后 ODP.NET 会给这个用户补建连接，锁住之后就建不进来（ADR-0352）。清池关掉闲着的连接，剩下的再以管理账号结束并等它们消失，DROP USER 才不会因 ORA-01940 失败
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
            _host = null;
        }

        using var maintenance = Maintenance(_admin);
        // 不先锁：清池后补建的连接正在登录时被杀，回 ORA-00031、要过几秒才从 v$session 消失，CI 上超过等待上限（ADR-0352）
        maintenance.Ado.ExecuteCommand($"ALTER USER {User} ACCOUNT LOCK");
        OracleConnection.ClearAllPools();
        await EndSessionsAsync(maintenance);
        maintenance.Ado.ExecuteCommand($"DROP USER {User} CASCADE");
    }

    /// <summary>
    /// 结束临时用户的全部会话，等它们从 <c>v$session</c> 里消失
    /// </summary>
    /// <remarks>
    /// 清池断开的连接与 <c>KILL SESSION … IMMEDIATE</c> 都不等会话真正结束；在那之前 DROP USER 报 ORA-01940（ADR-0350）。最多等 <see cref="_sessionWait"/>，到时仍有会话就交给 DROP USER 报错；结束时回 ORA-00030（已经没了）或 ORA-00031（只标成了 KILLED）不算失败
    /// </remarks>
    private async Task EndSessionsAsync(SqlSugarClient maintenance)
    {
        var started = Stopwatch.GetTimestamp();
        while (true)
        {
            // 已标成 KILLED 的不再杀一次，只等它消失；期间新出现的会话（锁账户之前已在登录的）照样结束
            foreach (var session in maintenance.Ado.SqlQuery<string>("SELECT sid || ',' || serial# FROM v$session WHERE username = :u AND status <> 'KILLED'", new SugarParameter(":u", User)))
            {
                try
                {
                    maintenance.Ado.ExecuteCommand($"ALTER SYSTEM KILL SESSION '{session}' IMMEDIATE");
                }
                catch (OracleException e) when (e.Number is SessionMissing or SessionMarkedForKill)
                {
                    // 查完到结束之间会话自己没了，或一时结束不了、已标成 KILLED：都不是失败，下面照常等它从 v$session 里消失
                }
            }

            if (maintenance.Ado.GetInt("SELECT COUNT(*) FROM v$session WHERE username = :u", new SugarParameter(":u", User)) == 0
                || Stopwatch.GetElapsedTime(started) >= _sessionWait)
            {
                return;
            }

            await Task.Delay(_sessionPoll);
        }
    }

    /// <summary>
    /// 以有建用户权限的账号连接的客户端，建用户与删用户用
    /// </summary>
    private static SqlSugarClient Maintenance(string admin)
        => new(new ConnectionConfig { DbType = DbType.Oracle, ConnectionString = admin, IsAutoCloseConnection = true });
}
