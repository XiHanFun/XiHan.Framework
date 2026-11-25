// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Data.Common;
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
/// 服务器来自环境变量 <see cref="EnvironmentVariable"/>（有建用户权限的连接串，如 <c>User Id=system;Password=…;Data Source=127.0.0.1:1521/FREEPDB1</c>），未设置即跳过，由 CI 的 oracle 分片运行。Oracle 没有「建库」，每个用例建一个随机名的用户，表建在它自己的 schema 里；缺省的 <c>NLS_COMP</c> 与 <c>NLS_SORT</c> 是 <c>BINARY</c>，<c>LIKE</c> 按码元比较，不必另定排序规则。空串存成 <c>NULL</c>，回填按 RFC-0021 把两者当作相同。行级锁，租约表不必放到另一个库。释放时停止宿主、结束它的会话并删用户
/// </remarks>
internal sealed class OracleSchema : IGatedDatabase
{
    /// <summary>
    /// 指向测试服务器的环境变量名
    /// </summary>
    public const string EnvironmentVariable = "XIHAN_TEST_ORACLE";

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
    /// 临时用户的连接串不用连接池；后台探针与刚结束的调用仍可能留着会话，先以管理账号结束它们，DROP USER 才不会因 ORA-01940 失败
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
            _host = null;
        }

        OracleConnection.ClearAllPools();
        using var maintenance = Maintenance(_admin);
        foreach (var session in maintenance.Ado.SqlQuery<string>("SELECT sid || ',' || serial# FROM v$session WHERE username = :u", new SugarParameter(":u", User)))
        {
            maintenance.Ado.ExecuteCommand($"ALTER SYSTEM KILL SESSION '{session}' IMMEDIATE");
        }

        maintenance.Ado.ExecuteCommand($"DROP USER {User} CASCADE");
    }

    /// <summary>
    /// 以有建用户权限的账号连接的客户端，建用户与删用户用
    /// </summary>
    private static SqlSugarClient Maintenance(string admin)
        => new(new ConnectionConfig { DbType = DbType.Oracle, ConnectionString = admin, IsAutoCloseConnection = true });
}
