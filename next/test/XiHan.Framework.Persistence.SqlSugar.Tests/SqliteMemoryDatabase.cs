// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Data.Sqlite;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Conformance.Fixtures;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 每个测试一个独立命名的 SQLite 共享内存库，不落文件。
/// </summary>
/// <remarks>
/// 用例结束时没有要删除的文件：超载时杀毒或索引程序短暂占着临时文件，按文件删除会以 IOException 让释放失败。库名带随机后缀，用例之间互不可见。
/// 共享缓存的内存库在最后一个连接关闭时消失，而别的测试类释放时会清空整个进程的连接池（<see cref="SqliteConnection.ClearAllPools"/>），
/// 所以本类自己持有一个打开的连接直到释放，库的生命期只由它决定。共享缓存是表级锁、遇锁立即失败而不排队等待，
/// 只适合不依赖多连接并发语义的用例；要多个连接并发读写的用例仍用 <see cref="SqliteDatabase"/>。
/// </remarks>
internal sealed class SqliteMemoryDatabase : IDisposable
{
    /// <summary>
    /// 保活连接：打开到释放为止，内存库随它关闭而消失。
    /// </summary>
    private readonly SqliteConnection _keepAlive;

    /// <summary>
    /// 打开保活连接，建出内存库。
    /// </summary>
    public SqliteMemoryDatabase()
    {
        _keepAlive = new SqliteConnection(ConnectionString);
        _keepAlive.Open();
    }

    /// <summary>
    /// 库名。
    /// </summary>
    /// <remarks>
    /// 随机后缀，同一进程里的用例互不共享。
    /// </remarks>
    public string Name { get; } = $"xihan-sqlsugar-{Guid.NewGuid():N}";

    /// <summary>
    /// 指向该内存库的连接串。
    /// </summary>
    public string ConnectionString => $"Data Source={Name};Mode=Memory;Cache=Shared";

    /// <summary>
    /// 宿主配置：连接绑定 <c>db</c> 与开启 <c>InitTables</c> 建表。
    /// </summary>
    public IEnumerable<KeyValuePair<string, string?>> Configuration =>
    [
        new("XiHan:Bindings:db", ConnectionString),
        new("XiHan:Persistence:SqlSugar:InitTables", "true"),
    ];

    /// <summary>
    /// 启动带本数据库与三种套件实体的宿主。
    /// </summary>
    public Task<TestHost> StartHostAsync() => TestHost.StartAsync(
        static builder => builder.UseFeature(SqliteDatabase.CreateFeature()),
        Configuration,
        static services =>
        {
            services.AddRepository<ConformanceOrder, Guid>();
            services.AddRepository<ConformanceNote, Guid>();
            services.AddRepository<ConformanceItem, int>();
        });

    /// <summary>
    /// 关闭保活连接，内存库随之消失。
    /// </summary>
    public void Dispose() => _keepAlive.Dispose();
}
