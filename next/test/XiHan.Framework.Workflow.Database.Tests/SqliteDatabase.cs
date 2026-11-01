// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.SqlSugar;
using XiHan.Framework.Testing;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Database.Entities;

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// 每个测试一组临时 SQLite 文件，宿主装配 SqlSugar 持久化与数据库工作流特性。
/// </summary>
/// <remarks>
/// SQLite 只有一个写者（库级锁，Microsoft.Data.Sqlite 的事务是 IMMEDIATE）：引擎在调用方事务里写过之后再取实例锁，租约行若与业务在同一个库文件，就要等调用方事务结束——等的正是自己。所以按框架的选项把租约表（<see cref="WorkflowLockRecord"/>）放到另一个库文件：SqlSugar 方式一以 <c>XiHan:Persistence:SqlSugar:Connections</c> 声明命名连接 <see cref="LeaseConnection"/>，<see cref="DatabaseWorkflowOptions.LockConnection"/> 指向它（ADR-0084）；PostgreSQL 等行级锁的数据库不需要这样做。带租户分片的调用由特性按租户属性里的连接串另加 <c>shard</c> 连接。释放时停止宿主并删除全部文件。
/// </remarks>
internal sealed class SqliteDatabase : IAsyncDisposable
{
    /// <summary>
    /// 租约表所在的命名连接（SqlSugar 的 <c>ConfigId</c>）。
    /// </summary>
    public const string LeaseConnection = "leases";

    /// <summary>
    /// 已启动的宿主。
    /// </summary>
    /// <remarks>
    /// 释放时一并停止。
    /// </remarks>
    private TestHost? _host;

    /// <summary>
    /// 业务库文件的完整路径。
    /// </summary>
    /// <remarks>
    /// 位于系统临时目录，文件名带随机后缀以免测试之间互相干扰。
    /// </remarks>
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"xihan-workflow-{Guid.NewGuid():N}.db");

    /// <summary>
    /// 租约库文件的完整路径。
    /// </summary>
    public string LeasePath => Path + ".leases.db";

    /// <summary>
    /// 指向业务库的 SQLite 连接串。
    /// </summary>
    /// <remarks>
    /// 忙等上限 5 秒：误把两个写者放进同一个库时尽快失败，而不是挂 30 秒。
    /// </remarks>
    public string ConnectionString => Connection(Path);

    /// <summary>
    /// 本测试另建的库文件（租户独立库），释放时一并删除。
    /// </summary>
    public List<string> ExtraFiles { get; } = [];

    /// <summary>
    /// 宿主配置。
    /// </summary>
    /// <remarks>
    /// 业务库绑定 <c>db</c> 与租约库的命名连接，租约表指向它；开启 <c>InitTables</c> 建表（按落点分组，租约表建在租约库上）、关掉后台轮询（周期 1 小时）、实例锁等待上限 0.2 秒。
    /// </remarks>
    public IEnumerable<KeyValuePair<string, string?>> Configuration =>
    [
        new("XiHan:Bindings:db", ConnectionString),
        new($"{SqlSugarPersistenceOptions.ConfigurationSection}:Connections:{LeaseConnection}", Connection(LeasePath)),
        new($"{DatabaseWorkflowOptions.ConfigurationSection}:LockConnection", LeaseConnection),
        new("XiHan:Persistence:SqlSugar:InitTables", "true"),
        new($"{DatabaseWorkflowOptions.ConfigurationSection}:PollIntervalSeconds", "3600"),
        new($"{WorkflowOptions.ConfigurationSection}:LockTimeoutSeconds", "0.2"),
    ];

    /// <summary>
    /// 指向某个库文件的连接串。
    /// </summary>
    public static string Connection(string path) => $"Data Source={path};Default Timeout=5";

    /// <summary>
    /// 一个连接配置：SQLite，施加持久化约定。
    /// </summary>
    public static ConnectionConfig Config(string configId, string connectionString)
        => SqlSugarConventions.Apply(new ConnectionConfig { ConfigId = configId, DbType = DbType.Sqlite, ConnectionString = connectionString, IsAutoCloseConnection = true });

    /// <summary>
    /// 启动装配了本数据库与 <see cref="DatabaseWorkflowFeature"/> 的宿主。
    /// </summary>
    /// <remarks>
    /// 后给的配置覆盖默认配置；<paramref name="configure"/> 可以再加特性（多租户等）。
    /// </remarks>
    public async Task<TestHost> StartHostAsync(IEnumerable<KeyValuePair<string, string?>>? configuration = null, Action<IServiceCollection>? services = null, Action<AppBuilder>? configure = null)
    {
        // 内存配置不接受重复键：后给的覆盖默认的
        var settings = Configuration.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in configuration ?? [])
        {
            settings[pair.Key] = pair.Value;
        }

        _host = await TestHost.StartAsync(b =>
        {
            // 方式一：特性按回调建客户端——默认连接、租约库的命名连接、带分片的调用里的分片连接都经同一回调
            b.UseFeature(new SqlSugarPersistenceFeature(static (binding, config) =>
            {
                config.DbType = DbType.Sqlite;
                config.ConnectionString = binding.ValueOrThrow().Value;
            })).UseFeature<DatabaseWorkflowFeature>();
            configure?.Invoke(b);
        }, settings, services);
        return _host;
    }

    /// <summary>
    /// 停止宿主，清空连接池并删除全部库文件及其日志、WAL 与共享内存文件。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
            _host = null;
        }

        foreach (var file in new[] { Path, LeasePath }.Concat(ExtraFiles))
        {
            await DeleteAsync(file);
        }
    }

    /// <summary>
    /// 删除一个库文件及其附属文件。
    /// </summary>
    private static async Task DeleteAsync(string path)
    {
        // SQLite 连接会被池化并持有文件句柄，先清空连接池，Windows 上才能删除文件；
        // 宿主停止时还在收尾的连接（释放锁、后台续约的最后一次写）可能仍占着文件，等它们结束再删
        for (var attempt = 0; ; attempt++)
        {
            SqliteConnection.ClearAllPools();
            try
            {
                File.Delete(path);
                break;
            }
            catch (IOException) when (attempt < 50)
            {
                await Task.Delay(100);
            }
        }

        File.Delete(path + "-journal");
        File.Delete(path + "-wal");
        File.Delete(path + "-shm");
    }
}
