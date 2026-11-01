// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Conformance.Fixtures;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 每个测试一个临时 SQLite 文件。
/// </summary>
internal sealed class SqliteDatabase : IDisposable
{
    /// <summary>
    /// 数据库文件的完整路径。
    /// </summary>
    /// <remarks>
    /// 位于系统临时目录，文件名带随机后缀以免测试之间互相干扰。
    /// </remarks>
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"xihan-sqlsugar-{Guid.NewGuid():N}.db");

    /// <summary>
    /// 指向该文件的 SQLite 连接串。
    /// </summary>
    public string ConnectionString => $"Data Source={Path}";

    /// <summary>
    /// 宿主配置：连接绑定 <c>db</c> 与开启 <c>InitTables</c> 建表。
    /// </summary>
    public IEnumerable<KeyValuePair<string, string?>> Configuration =>
    [
        new("XiHan:Bindings:db", ConnectionString),
        new("XiHan:Persistence:SqlSugar:InitTables", "true"),
    ];

    /// <summary>
    /// 创建以 SQLite 装配的 SqlSugar 特性。
    /// </summary>
    /// <remarks>
    /// 连接串来自宿主解析的绑定。
    /// </remarks>
    public static IFeature CreateFeature()
        => new SqlSugarPersistenceFeature(static (binding, config) =>
        {
            config.DbType = DbType.Sqlite;
            config.ConnectionString = binding.ValueOrThrow().Value;
        });

    /// <summary>
    /// 启动带本数据库、三种套件实体与指定时钟的宿主。
    /// </summary>
    /// <remarks>
    /// 不传时钟则用系统时钟。
    /// </remarks>
    public Task<TestHost> StartHostAsync(TimeProvider? timeProvider = null) => TestHost.StartAsync(
        builder => builder.UseFeature(CreateFeature()),
        Configuration,
        services =>
        {
            services.AddSingleton(timeProvider ?? TimeProvider.System);
            services.AddRepository<ConformanceOrder, Guid>();
            services.AddRepository<ConformanceNote, Guid>();
            services.AddRepository<ConformanceItem, int>();
        });

    /// <summary>
    /// 清空本库的连接池并删除数据库文件及其日志、WAL 与共享内存文件。
    /// </summary>
    /// <remarks>
    /// 用例自己在 <see cref="ConnectionString"/> 后面追加了参数的（连接池按连接串分组），先对那个连接串调用 <see cref="ClearPool"/>。
    /// </remarks>
    public void Dispose()
    {
        // SQLite 连接会被池化并持有文件句柄，先清空本库的连接池，Windows 上才能删除文件
        ClearPool(ConnectionString);
        File.Delete(Path);
        File.Delete(Path + "-journal");
        File.Delete(Path + "-wal");
        File.Delete(Path + "-shm");
    }

    /// <summary>
    /// 清空一个连接串的连接池。
    /// </summary>
    /// <remarks>
    /// 只清本库连接串的连接池（连接池按连接串分组）。不用 <c>SqliteConnection.ClearAllPools()</c>：它清整个进程的连接池，会把并行用例此刻正在打开的连接当作泄漏回收并关掉句柄，那个用例随即以 <c>ObjectDisposedException</c>（<c>SQLitePCL.sqlite3</c>）失败：Microsoft.Data.Sqlite 在池锁之外先置活动标记、再记外层连接，清池时回收泄漏连接恰好落在两步之间；SqlSugar 建表时把这个异常报成「需要系统表权限」。
    /// </remarks>
    public static void ClearPool(string connectionString)
    {
        using var connection = new SqliteConnection(connectionString);
        SqliteConnection.ClearPool(connection);
    }
}
