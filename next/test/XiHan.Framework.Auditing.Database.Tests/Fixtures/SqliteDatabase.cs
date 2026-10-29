// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using XiHan.Framework.Persistence.SqlSugar;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Auditing.Database.Tests.Fixtures;

/// <summary>
/// 每个测试一个临时 SQLite 文件。
/// </summary>
/// <remarks>
/// 宿主装配 SqlSugar 持久化（应用自注册客户端）与数据库审计特性。同一个文件可以先后启动多个宿主。释放时停止全部宿主并删除文件。文件用 WAL 日志且每个连接关掉同步写盘：测试库不需要崩溃安全，省掉每次提交的 fsync。
/// </remarks>
internal sealed class SqliteDatabase : IAsyncDisposable
{
    /// <summary>
    /// 已启动的宿主。
    /// </summary>
    /// <remarks>
    /// 释放时一并停止。
    /// </remarks>
    private readonly List<TestHost> _hosts = [];

    /// <summary>
    /// 文件是否已切到 WAL 日志（该设置持久在文件里，只需做一次）。
    /// </summary>
    private bool _journaled;

    /// <summary>
    /// 数据库文件的完整路径。
    /// </summary>
    /// <remarks>
    /// 位于系统临时目录，文件名带随机后缀以免测试之间互相干扰。
    /// </remarks>
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"xihan-auditing-{Guid.NewGuid():N}.db");

    /// <summary>
    /// 指向该文件的 SQLite 连接串。
    /// </summary>
    public string ConnectionString => $"Data Source={Path}";

    /// <summary>
    /// 宿主配置：开启 <c>InitTables</c> 建表、不写事件日志。
    /// </summary>
    public static IEnumerable<KeyValuePair<string, string?>> Configuration =>
    [
        new("XiHan:Persistence:SqlSugar:InitTables", "true"),
        new($"{DatabaseAuditingOptions.ConfigurationSection}:LogEvents", "false"),
    ];

    /// <summary>
    /// 启动装配了本数据库与 <see cref="DatabaseAuditingFeature"/> 的宿主。
    /// </summary>
    /// <remarks>
    /// 后给的配置覆盖默认配置。
    /// </remarks>
    public async Task<TestHost> StartHostAsync(IEnumerable<KeyValuePair<string, string?>>? configuration = null)
    {
        EnsureWriteAheadLog();
        // 内存配置不接受重复键：后给的覆盖默认的
        var settings = Configuration.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in configuration ?? [])
        {
            settings[pair.Key] = pair.Value;
        }

        var host = await TestHost.StartAsync(
            static b => b.UseFeature(new SqlSugarPersistenceFeature()).UseFeature<DatabaseAuditingFeature>(),
            settings,
            collection => collection.AddScoped<ISqlSugarClient>(_ => CreateClient()));
        _hosts.Add(host);
        return host;
    }

    /// <summary>
    /// 停止全部宿主，清空连接池并删除数据库文件及其日志、WAL 与共享内存文件。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        foreach (var host in _hosts)
        {
            await host.DisposeAsync();
        }

        _hosts.Clear();
        // SQLite 连接会被池化并持有文件句柄，先清空连接池，Windows 上才能删除文件；文件仍被占用时稍等再删
        for (var attempt = 0; ; attempt++)
        {
            SqliteConnection.ClearAllPools();
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
    /// 创建一个作用域的客户端：按协议约定配置映射，连接建好后关掉同步写盘。
    /// </summary>
    private SqlSugarClient CreateClient()
    {
        var client = new SqlSugarClient(SqlSugarConventions.Apply(new ConnectionConfig { DbType = DbType.Sqlite, ConnectionString = ConnectionString }));
        client.Ado.ExecuteCommand("PRAGMA synchronous=OFF");
        return client;
    }

    /// <summary>
    /// 第一次启动宿主前把文件切到 WAL 日志：提交只追加日志，读不阻塞写。
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
