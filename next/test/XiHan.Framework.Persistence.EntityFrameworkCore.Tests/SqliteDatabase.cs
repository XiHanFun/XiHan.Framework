// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Conformance;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// 每个测试一个临时 SQLite 文件。
/// </summary>
internal sealed class SqliteDatabase : IDisposable
{
    /// <summary>
    /// 数据库文件的完整路径；位于系统临时目录，文件名带随机后缀以免测试之间互相干扰。
    /// </summary>
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"xihan-efcore-{Guid.NewGuid():N}.db");

    /// <summary>
    /// 指向该文件的 SQLite 连接串。
    /// </summary>
    public string ConnectionString => $"Data Source={Path}";

    /// <summary>
    /// 宿主配置：连接绑定 <c>db</c> 与开启 <c>EnsureCreated</c> 建表。
    /// </summary>
    public IEnumerable<KeyValuePair<string, string?>> Configuration =>
    [
        new("XiHan:Bindings:db", ConnectionString),
        new("XiHan:Persistence:EntityFrameworkCore:EnsureCreated", "true"),
    ];

    /// <summary>
    /// 创建以 <see cref="ConformanceDbContext"/> 与 SQLite 装配的 EF Core 特性；连接串来自宿主解析的绑定。
    /// </summary>
    public static IFeature CreateFeature()
        => new EntityFrameworkCorePersistenceFeature<ConformanceDbContext>(static (binding, options) => options.UseSqlite(binding.ValueOrThrow().Value));

    /// <summary>
    /// 启动带本数据库、三种套件实体与指定时钟的宿主；不传时钟则用系统时钟。
    /// </summary>
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
    /// 释放连接池并删除数据库文件及其日志、WAL 与共享内存文件。
    /// </summary>
    public void Dispose()
    {
        // SQLite 连接会被池化并持有文件句柄，先清空连接池，Windows 上才能删除文件
        SqliteConnection.ClearAllPools();
        File.Delete(Path);
        File.Delete(Path + "-journal");
        File.Delete(Path + "-wal");
        File.Delete(Path + "-shm");
    }
}
