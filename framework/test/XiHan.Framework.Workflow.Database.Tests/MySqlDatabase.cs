// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using SqlSugar;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.SqlSugar;
using XiHan.Framework.Testing;
using XiHan.Framework.Workflow.Abstractions;

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// 每个测试一个临时 MySQL 库，宿主装配 SqlSugar 持久化与数据库工作流特性
/// </summary>
/// <remarks>
/// 服务器来自环境变量 <see cref="EnvironmentVariable"/>（不含 <c>Database</c> 的连接串，如 <c>Server=127.0.0.1;Port=3306;User Id=root;Password=…</c>；账号要能建库与删库），未设置即跳过，由 CI 的 database 分片运行。建库时指定字符集 <c>utf8mb4</c> 与排序规则 <see cref="Collation"/>。InnoDB 是行级锁，租约表不必放到另一个库。释放时停止宿主并删库
/// </remarks>
internal sealed class MySqlDatabase : IGatedDatabase
{
    /// <summary>
    /// 指向测试服务器的环境变量名
    /// </summary>
    public const string EnvironmentVariable = "XIHAN_TEST_MYSQL";

    /// <summary>
    /// 临时库的排序规则
    /// </summary>
    /// <remarks>
    /// RFC-0021 的关键字按字面匹配，排序规则另有放宽时以排序规则为准；套件核对的是不放宽时的结果。缺省的 <c>utf8mb4_0900_ai_ci</c> 不区分重音与全角半角：<c>İ</c> 匹配 <c>i</c>、平面以外的大小写字母互相匹配、<c>50％</c> 匹配 <c>50%</c>，存储套件与回填的用例都会因此失败。这里取按码位比较的二进制排序规则（不补空格），与内存实现的序数比较一致；MySQL 的表名与列名的大小写不随库的排序规则
    /// </remarks>
    private const string Collation = "utf8mb4_0900_bin";

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
    private MySqlDatabase(string server, string name)
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
    public static MySqlDatabase Create()
    {
        var configured = Environment.GetEnvironmentVariable(EnvironmentVariable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(configured), $"Set the environment variable '{EnvironmentVariable}' to a MySQL server connection string (without Database) to run this test.");
        var server = configured!.TrimEnd(';');
        var name = $"xihan_workflow_{Guid.NewGuid():N}";
        using (var maintenance = Maintenance(server))
        {
            maintenance.Ado.ExecuteCommand($"CREATE DATABASE `{name}` CHARACTER SET utf8mb4 COLLATE {Collation}");
        }

        return new MySqlDatabase(server, name);
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
                config.DbType = DbType.MySql;
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

        // 删库前先断开本进程的连接池
        MySqlConnection.ClearAllPools();
        using var maintenance = Maintenance(_server);
        maintenance.Ado.ExecuteCommand($"DROP DATABASE IF EXISTS `{Name}`");
    }

    /// <summary>
    /// 连到系统库 <c>mysql</c> 的客户端，建库与删库用
    /// </summary>
    private static SqlSugarClient Maintenance(string server)
        => new(new ConnectionConfig { DbType = DbType.MySql, ConnectionString = $"{server};Database=mysql", IsAutoCloseConnection = true });
}
