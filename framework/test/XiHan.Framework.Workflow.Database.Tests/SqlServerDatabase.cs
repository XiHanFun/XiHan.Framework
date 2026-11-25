// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.SqlSugar;
using XiHan.Framework.Testing;
using XiHan.Framework.Workflow.Abstractions;

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// 每个测试一个临时 SQL Server 库，宿主装配 SqlSugar 持久化与数据库工作流特性
/// </summary>
/// <remarks>
/// 服务器来自环境变量 <see cref="EnvironmentVariable"/>（不含 <c>Database</c> 的连接串，如 <c>Server=127.0.0.1,1433;User Id=sa;Password=…;TrustServerCertificate=True</c>），未设置即跳过，由 CI 的 database 分片运行。建库时指定排序规则 <see cref="Collation"/>，并开读已提交快照（与 Persistence.SqlSugar 的夹具相同：未提交的写入不挡读，与 PostgreSQL、MySQL 一致）。SQL Server 是行级锁，租约表不必放到另一个库。释放时停止宿主并删库
/// </remarks>
internal sealed class SqlServerDatabase : IGatedDatabase
{
    /// <summary>
    /// 指向测试服务器的环境变量名
    /// </summary>
    public const string EnvironmentVariable = "XIHAN_TEST_SQLSERVER";

    /// <summary>
    /// 临时库的排序规则
    /// </summary>
    /// <remarks>
    /// RFC-0021 的关键字按字面匹配，排序规则另有放宽时以排序规则为准；套件核对的是不放宽时的结果。服务器缺省的 <c>SQL_Latin1_General_CP1_CI_AS</c> 不区分全角与半角（<c>50％</c> 匹配 <c>50%</c>），平面以外的字符在 <c>LIKE</c> 里被忽略（<c>%𐐨%</c> 匹配任何文本），存储套件与回填的用例都会因此失败。这里取区分重音、假名与全宽、认得平面以外字符的版本 100 排序规则；仍不区分大小写——查询列与关键字都已折成小写，而 SqlSugar 读库结构的语句里列名的大小写与系统视图不一致（如 <c>syscolumns.Id</c>），区分大小写的库上读不了结构
    /// </remarks>
    private const string Collation = "Latin1_General_100_CI_AS_KS_WS_SC";

    /// <summary>
    /// 服务器连接串（不含库名）
    /// </summary>
    private readonly string _server;

    /// <summary>
    /// 已启动的宿主
    /// </summary>
    /// <remarks>
    /// 释放时一并停止
    /// </remarks>
    private TestHost? _host;

    /// <summary>
    /// 建好临时库
    /// </summary>
    private SqlServerDatabase(string server, string name)
    {
        _server = server;
        Name = name;
    }

    /// <summary>
    /// 临时库名
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 在环境变量指向的服务器上建一个随机名的库
    /// </summary>
    /// <remarks>
    /// 未设置环境变量时跳过当前用例
    /// </remarks>
    public static SqlServerDatabase Create()
    {
        var configured = Environment.GetEnvironmentVariable(EnvironmentVariable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(configured), $"Set the environment variable '{EnvironmentVariable}' to a SQL Server connection string (without Database) to run this test.");
        var server = configured!.TrimEnd(';');
        var name = $"xihan_workflow_{Guid.NewGuid():N}";
        using (var maintenance = Maintenance(server))
        {
            maintenance.Ado.ExecuteCommand($"CREATE DATABASE [{name}] COLLATE {Collation}");
            maintenance.Ado.ExecuteCommand($"ALTER DATABASE [{name}] SET READ_COMMITTED_SNAPSHOT ON");
        }

        return new SqlServerDatabase(server, name);
    }

    /// <inheritdoc />
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
                config.DbType = DbType.SqlServer;
                config.ConnectionString = binding.ValueOrThrow().Value;
            })).UseFeature<DatabaseWorkflowFeature>();
            configure?.Invoke(b);
        }, settings, services);
        return _host;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
            _host = null;
        }

        // 删库前先断开本进程的连接池，再切到单用户模式结束别的会话
        SqlConnection.ClearAllPools();
        using var maintenance = Maintenance(_server);
        maintenance.Ado.ExecuteCommand($"IF DB_ID(N'{Name}') IS NOT NULL BEGIN ALTER DATABASE [{Name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{Name}]; END");
    }

    /// <summary>
    /// 连到 <c>master</c> 的客户端，建库与删库用
    /// </summary>
    private static SqlSugarClient Maintenance(string server)
        => new(new ConnectionConfig { DbType = DbType.SqlServer, ConnectionString = $"{server};Database=master", IsAutoCloseConnection = true });
}
