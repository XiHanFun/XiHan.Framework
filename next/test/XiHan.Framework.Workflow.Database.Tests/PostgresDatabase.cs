// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SqlSugar;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.SqlSugar;
using XiHan.Framework.Testing;
using XiHan.Framework.Workflow.Abstractions;

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// 每个测试一个临时 PostgreSQL 库，宿主装配 SqlSugar 持久化与数据库工作流特性。
/// </summary>
/// <remarks>
/// 服务器来自环境变量 <see cref="EnvironmentVariable"/>（不含 <c>Database</c> 的连接串，如 <c>Host=127.0.0.1;Port=5432;Username=postgres;Password=postgres</c>），未设置即跳过。PostgreSQL 是行级锁，租约表不必放到另一个库（与 <see cref="SqliteDatabase"/> 不同）；定义列表、实例与待办的关键字在这里翻译成区分大小写、带转义的 <c>LIKE … ESCAPE '!'</c>（ADR-0098），小写折叠的查询列与关键字按字面匹配（ADR-0099 第四节）。释放时停止宿主并删库。
/// </remarks>
internal sealed class PostgresDatabase : IAsyncDisposable
{
    /// <summary>
    /// 指向测试服务器的环境变量名。
    /// </summary>
    public const string EnvironmentVariable = "XIHAN_TEST_POSTGRES";

    /// <summary>
    /// 服务器连接串（不含库名）。
    /// </summary>
    private readonly string _server;

    /// <summary>
    /// 已启动的宿主。
    /// </summary>
    /// <remarks>
    /// 释放时一并停止。
    /// </remarks>
    private TestHost? _host;

    /// <summary>
    /// 建好临时库。
    /// </summary>
    private PostgresDatabase(string server, string name)
    {
        _server = server;
        Name = name;
    }

    /// <summary>
    /// 临时库名。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 在环境变量指向的服务器上建一个随机名的库。
    /// </summary>
    /// <remarks>
    /// 未设置环境变量时跳过当前用例。
    /// </remarks>
    public static PostgresDatabase Create()
    {
        var configured = Environment.GetEnvironmentVariable(EnvironmentVariable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(configured), $"Set the environment variable '{EnvironmentVariable}' to a PostgreSQL server connection string (without Database) to run this test.");
        var server = configured!.TrimEnd(';');
        var name = $"xihan_workflow_{Guid.NewGuid():N}";
        using (var maintenance = Maintenance(server))
        {
            maintenance.Ado.ExecuteCommand($"CREATE DATABASE \"{name}\"");
        }

        return new PostgresDatabase(server, name);
    }

    /// <summary>
    /// 启动宿主：业务库绑定 <c>db</c>，开启 <c>InitTables</c>，关掉后台轮询（周期 1 小时）。
    /// </summary>
    public async Task<TestHost> StartHostAsync(IEnumerable<KeyValuePair<string, string?>>? configuration = null, Action<IServiceCollection>? services = null, Action<AppBuilder>? configure = null)
    {
        // 内存配置不接受重复键：后给的覆盖默认的
        var settings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["XiHan:Bindings:db"] = $"{_server};Database={Name}",
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
                config.DbType = DbType.PostgreSQL;
                config.ConnectionString = binding.ValueOrThrow().Value;
            })).UseFeature<DatabaseWorkflowFeature>();
            configure?.Invoke(b);
        }, settings, services);
        return _host;
    }

    /// <summary>
    /// 立即删库（宿主可能还在服务）。
    /// </summary>
    /// <remarks>
    /// 先断开本进程的连接池，再 <c>DROP DATABASE … WITH (FORCE)</c>；之后的 <see cref="DisposeAsync"/> 照常停宿主，再删一次是 <c>IF EXISTS</c>。
    /// </remarks>
    public void Drop()
    {
        NpgsqlConnection.ClearAllPools();
        using var maintenance = Maintenance(_server);
        maintenance.Ado.ExecuteCommand($"DROP DATABASE IF EXISTS \"{Name}\" WITH (FORCE)");
    }

    /// <summary>
    /// 停止宿主并删库。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
            _host = null;
        }

        // 删库前先断开本进程的连接池
        NpgsqlConnection.ClearAllPools();
        using var maintenance = Maintenance(_server);
        maintenance.Ado.ExecuteCommand($"DROP DATABASE IF EXISTS \"{Name}\" WITH (FORCE)");
    }

    /// <summary>
    /// 连到维护库 <c>postgres</c> 的客户端，建库与删库用。
    /// </summary>
    private static SqlSugarClient Maintenance(string server)
        => new(new ConnectionConfig { DbType = DbType.PostgreSQL, ConnectionString = $"{server};Database=postgres", IsAutoCloseConnection = true });
}
