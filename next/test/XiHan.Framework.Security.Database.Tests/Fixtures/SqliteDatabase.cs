// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.SqlSugar;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Security.Database.Tests.Fixtures;

/// <summary>
/// 一个临时 SQLite 文件。
/// </summary>
/// <remarks>
/// 宿主装配 SqlSugar 持久化（应用自注册客户端、<c>InitTables</c> 建表）与数据库授权特性。释放时停止宿主并删除文件。文件用 WAL 日志且每个连接关掉同步写盘：测试库不需要崩溃安全，省掉每次提交的 fsync。
/// </remarks>
internal sealed class SqliteDatabase : IAsyncDisposable
{
    /// <summary>
    /// 已启动的宿主。
    /// </summary>
    /// <remarks>
    /// 释放时停止。
    /// </remarks>
    private TestHost? _host;

    /// <summary>
    /// 数据库文件的完整路径。
    /// </summary>
    /// <remarks>
    /// 位于系统临时目录，文件名带随机后缀以免测试之间互相干扰。
    /// </remarks>
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"xihan-security-{Guid.NewGuid():N}.db");

    /// <summary>
    /// 指向该文件的 SQLite 连接串。
    /// </summary>
    public string ConnectionString => $"Data Source={Path}";

    /// <summary>
    /// 为真时此后新建的客户端连向一个不存在的目录，打开连接即失败，模拟数据库不可达。
    /// </summary>
    /// <remarks>
    /// 宿主启动（建表）时必须为假；启动后切换，下一次作用域读取即生效。
    /// </remarks>
    public bool Unreachable
    {
        get => Volatile.Read(ref _unreachable);
        set => Volatile.Write(ref _unreachable, value);
    }

    /// <summary>
    /// <see cref="Unreachable"/> 的存储。
    /// </summary>
    private bool _unreachable;

    /// <summary>
    /// 启动装配了本数据库与 <see cref="DatabaseSecurityFeature"/> 的宿主。
    /// </summary>
    /// <param name="configuration">追加的配置。</param>
    /// <param name="features">追加装配的特性（如认证与业务特性）。</param>
    public async Task<TestHost> StartHostAsync(IEnumerable<KeyValuePair<string, string?>>? configuration = null, Action<AppBuilder>? features = null)
    {
        EnsureWriteAheadLog();
        var settings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase) { ["XiHan:Persistence:SqlSugar:InitTables"] = "true" };
        foreach (var pair in configuration ?? [])
        {
            settings[pair.Key] = pair.Value;
        }

        _host = await TestHost.StartAsync(
            b =>
            {
                b.UseFeature(new SqlSugarPersistenceFeature()).UseFeature<DatabaseSecurityFeature>();
                features?.Invoke(b);
            },
            settings,
            collection => collection.AddScoped<ISqlSugarClient>(_ => CreateClient()));
        return _host;
    }

    /// <summary>
    /// 停止宿主，清空本库的连接池并删除数据库文件及其日志、WAL 与共享内存文件。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
            _host = null;
        }

        // SQLite 连接会被池化并持有文件句柄，先清空本库的连接池，Windows 上才能删除文件。
        // 只清本库连接串的连接池（连接池按连接串分组）。不用 SqliteConnection.ClearAllPools()：它清整个进程的连接池，会把并行用例此刻正在打开的连接当作泄漏回收并关掉句柄，那个用例随即以 ObjectDisposedException（SQLitePCL.sqlite3）失败
        using (var connection = new SqliteConnection(ConnectionString))
        {
            SqliteConnection.ClearPool(connection);
        }

        File.Delete(Path);
        File.Delete(Path + "-journal");
        File.Delete(Path + "-wal");
        File.Delete(Path + "-shm");
    }

    /// <summary>
    /// 创建一个作用域的客户端：按协议约定配置映射，连接建好后关掉同步写盘。
    /// </summary>
    private SqlSugarClient CreateClient()
    {
        if (Unreachable)
        {
            // 目录不存在：任何模式都打不开文件，开启工作单元时抛出，与连不上数据库同样表现为基础设施异常
            var missing = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"xihan-missing-{Guid.NewGuid():N}", "grants.db");
            return new SqlSugarClient(SqlSugarConventions.Apply(new ConnectionConfig { DbType = DbType.Sqlite, ConnectionString = $"Data Source={missing};Mode=ReadWrite" }));
        }

        var client = new SqlSugarClient(SqlSugarConventions.Apply(new ConnectionConfig { DbType = DbType.Sqlite, ConnectionString = ConnectionString }));
        client.Ado.ExecuteCommand("PRAGMA synchronous=OFF");
        return client;
    }

    /// <summary>
    /// 启动宿主前把文件切到 WAL 日志：提交只追加日志，读不阻塞写。
    /// </summary>
    private void EnsureWriteAheadLog()
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL";
        command.ExecuteNonQuery();
    }
}
