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
/// 套件实体的 DbContext：显式注册三种实体并应用协议约定。
/// </summary>
public sealed class ConformanceDbContext(DbContextOptions<ConformanceDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConformanceOrder>();
        modelBuilder.Entity<ConformanceNote>();
        modelBuilder.Entity<ConformanceItem>(static item => item.Property(static i => i.Id).ValueGeneratedNever());
        modelBuilder.ApplyPersistenceConventions();
    }
}

/// <summary>
/// 每个测试一个临时 SQLite 文件。
/// </summary>
internal sealed class SqliteDatabase : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"xihan-efcore-{Guid.NewGuid():N}.db");

    public string ConnectionString => $"Data Source={Path}";

    public IEnumerable<KeyValuePair<string, string?>> Configuration =>
    [
        new("XiHan:Bindings:db", ConnectionString),
        new("XiHan:Persistence:EntityFrameworkCore:EnsureCreated", "true"),
    ];

    public static IFeature CreateFeature()
        => new EntityFrameworkCorePersistenceFeature<ConformanceDbContext>(static (binding, options) => options.UseSqlite(binding.ValueOrThrow().Value));

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

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(Path);
        File.Delete(Path + "-journal");
        File.Delete(Path + "-wal");
        File.Delete(Path + "-shm");
    }
}
