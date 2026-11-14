// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using XiHan.Framework.Caching.InMemory;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Localization.InMemory;
using XiHan.Framework.Persistence.SqlSugar;
using XiHan.Framework.Testing;

namespace XiHan.Framework.MultiTenancy.Database.Tests;

/// <summary>
/// 每个测试一个临时 SQLite 文件
/// </summary>
/// <remarks>
/// 宿主装配 SqlSugar 持久化（应用自注册客户端）、内存本地化与数据库多租户特性。同一个文件可以启动多个宿主。释放时停止全部宿主并删除文件。文件用 WAL 日志且每个连接关掉同步写盘：测试库不需要崩溃安全，省掉每次提交的 fsync
/// </remarks>
internal sealed class SqliteDatabase : IAsyncDisposable
{
    /// <summary>
    /// 已启动的宿主
    /// </summary>
    /// <remarks>
    /// 释放时一并停止
    /// </remarks>
    private readonly List<TestHost> _hosts = [];

    /// <summary>
    /// 文件是否已切到 WAL 日志（该设置持久在文件里，只需做一次）
    /// </summary>
    private bool _journaled;

    /// <summary>
    /// 数据库文件的完整路径
    /// </summary>
    /// <remarks>
    /// 位于系统临时目录，文件名带随机后缀以免测试之间互相干扰
    /// </remarks>
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"xihan-multitenancy-{Guid.NewGuid():N}.db");

    /// <summary>
    /// 指向该文件的 SQLite 连接串
    /// </summary>
    public string ConnectionString => $"Data Source={Path}";

    /// <summary>
    /// 启动装配了本数据库与 <see cref="DatabaseMultiTenancyFeature"/> 的宿主
    /// </summary>
    /// <remarks>
    /// 开启 <c>InitTables</c> 建表，后给的配置覆盖默认配置。同一个库可以起多个宿主，模拟多实例部署
    /// </remarks>
    /// <param name="configuration">追加的配置</param>
    /// <param name="features">追加装配的特性（如 Redis 与两级缓存）</param>
    /// <param name="services">在特性之前登记的服务（如几个宿主共用的缓存）</param>
    public async Task<TestHost> StartHostAsync(IEnumerable<KeyValuePair<string, string?>>? configuration = null, Action<AppBuilder>? features = null, Action<IServiceCollection>? services = null)
    {
        EnsureWriteAheadLog();
        // 内存配置不接受重复键：后给的覆盖默认的
        var settings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase) { ["XiHan:Persistence:SqlSugar:InitTables"] = "true" };
        foreach (var pair in configuration ?? [])
        {
            settings[pair.Key] = pair.Value;
        }

        var host = await TestHost.StartAsync(
            b =>
            {
                b.UseFeature(new SqlSugarPersistenceFeature()).UseFeature<InMemoryLocalizationFeature>().UseFeature<InMemoryCachingFeature>().UseFeature<DatabaseMultiTenancyFeature>();
                features?.Invoke(b);
            },
            settings,
            collection =>
            {
                collection.AddScoped<ISqlSugarClient>(_ => CreateClient());
                services?.Invoke(collection);
            });
        _hosts.Add(host);
        return host;
    }

    /// <summary>
    /// 启动以本库为主库、配了从库的宿主
    /// </summary>
    /// <remarks>
    /// 客户端由特性按连接配置建，读写路由与真实部署相同；建表只开在主库上
    /// </remarks>
    /// <param name="replica">从库，表要先由它自己的宿主建好</param>
    public async Task<TestHost> StartReplicatedHostAsync(SqliteDatabase replica)
    {
        EnsureWriteAheadLog();
        var feature = new SqlSugarPersistenceFeature((binding, config) =>
        {
            config.DbType = DbType.Sqlite;
            config.ConnectionString = binding.ValueOrThrow().Value;
            config.SlaveConnectionConfigs = [new SlaveConnectionConfig { ConnectionString = replica.ConnectionString, HitRate = 10 }];
        });
        var host = await TestHost.StartAsync(
            b => b.UseFeature(feature).UseFeature<InMemoryLocalizationFeature>().UseFeature<InMemoryCachingFeature>().UseFeature<DatabaseMultiTenancyFeature>(),
            [new("XiHan:Bindings:db", ConnectionString), new("XiHan:Persistence:SqlSugar:InitTables", "true")]);
        _hosts.Add(host);
        return host;
    }

    /// <summary>
    /// 停止全部宿主，清空本库的连接池并删除数据库文件及其日志、WAL 与共享内存文件
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        foreach (var host in _hosts)
        {
            await host.DisposeAsync();
        }

        _hosts.Clear();
        // SQLite 连接会被池化并持有文件句柄，先清空本库的连接池，Windows 上才能删除文件；文件仍被占用时稍等再删
        // 只清本库连接串的连接池（连接池按连接串分组）。不用 SqliteConnection.ClearAllPools()：它清整个进程的连接池，会把并行用例此刻正在打开的连接当作泄漏回收并关掉句柄，那个用例随即以 ObjectDisposedException（SQLitePCL.sqlite3）失败
        // SqlSugar 在 SQLite 上读数据后不释放命令（AdoProvider.GetDataReader 只对别的数据库 Dispose），事务里用过的命令被回收后，
        // 连接关闭时已够不着它，它的预编译语句要等终结器才 finalize；在此之前 sqlite3_close_v2 把库留成僵尸、文件一直占着
        // 先回收并等终结器跑完，库才真正关掉
        GC.Collect();
        GC.WaitForPendingFinalizers();
        using var connection = new SqliteConnection(ConnectionString);
        for (var attempt = 0; ; attempt++)
        {
            SqliteConnection.ClearPool(connection);
            try
            {
                File.Delete(Path);
                break;
            }
            catch (IOException) when (attempt < 50)
            {
                await Task.Delay(100);
            }
        }

        File.Delete(Path + "-journal");
        File.Delete(Path + "-wal");
        File.Delete(Path + "-shm");
    }

    /// <summary>
    /// 创建一个作用域的客户端：按协议约定配置映射，连接建好后关掉同步写盘
    /// </summary>
    private SqlSugarClient CreateClient()
    {
        var client = new SqlSugarClient(SqlSugarConventions.Apply(new ConnectionConfig { DbType = DbType.Sqlite, ConnectionString = ConnectionString }));
        client.Ado.ExecuteCommand("PRAGMA synchronous=OFF");
        return client;
    }

    /// <summary>
    /// 第一次启动宿主前把文件切到 WAL 日志：提交只追加日志，读不阻塞写
    /// </summary>
    private void EnsureWriteAheadLog()
    {
        if (_journaled)
        {
            return;
        }

        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL";
        command.ExecuteNonQuery();
        _journaled = true;
    }
}
