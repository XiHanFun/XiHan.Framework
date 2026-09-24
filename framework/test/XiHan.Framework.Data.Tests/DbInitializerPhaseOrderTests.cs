// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.Data.SqlSugar.Initializers;
using XiHan.Framework.Data.SqlSugar.Options;
using XiHan.Framework.Data.SqlSugar.Seeders;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.Data.Tests;

/// <summary>
/// 数据库初始化的分段顺序测试。
/// </summary>
/// <remarks>
/// 锁死一条顺序契约：全部连接建库建表 → 表结构升级 → 播种。建表只建缺失的表、不改已存在的表，
/// 存量表的新列由升级器补齐；种子按最新实体读写，排在升级之前就会撞上还没补齐的列、启动失败。
/// </remarks>
public sealed class DbInitializerPhaseOrderTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"xihan-db-phase-{Guid.NewGuid():N}.db");
    private readonly SqlSugarClient _client;
    private readonly List<string> _events = [];

    /// <summary>
    /// 建一个真实的 SQLite 库，供建库阶段探测连通性
    /// </summary>
    public DbInitializerPhaseOrderTests()
    {
        _client = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"DataSource={_databasePath};Pooling=False",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true
        });
    }

    [Fact]
    public async Task 升级排在全部连接建表之后任何播种之前()
    {
        var initializer = CreateInitializer(["Default", "Default_Erp"]);

        await initializer.InitializeAsync();

        Assert.Equal(["tables:Default", "tables:Default_Erp", "upgrade", "seed", "seed"], _events);
    }

    [Fact]
    public async Task 升级失败时不播种()
    {
        var initializer = CreateInitializer(["Default"], upgraderFails: true);

        await Assert.ThrowsAsync<InvalidOperationException>(initializer.InitializeAsync);

        Assert.Equal(["tables:Default", "upgrade"], _events);
    }

    [Fact]
    public async Task 关闭播种时升级照跑()
    {
        var initializer = CreateInitializer(["Default"], enableDataSeeding: false);

        await initializer.InitializeAsync();

        Assert.Equal(["tables:Default", "upgrade"], _events);
    }

    [Fact]
    public async Task 关闭建表时既不升级也不播种()
    {
        // 表结构不由应用维护时，升级交给升级模块在应用初始化之后执行，这里不代为跑
        var initializer = CreateInitializer(["Default"], enableTableInitialization: false);

        await initializer.InitializeAsync();

        Assert.Empty(_events);
    }

    /// <summary>
    /// 释放连接并清理临时库文件
    /// </summary>
    public void Dispose()
    {
        _client.Dispose();
        try
        {
            File.Delete(_databasePath);
        }
        catch (IOException)
        {
            // SQLite 句柄偶尔晚于 Dispose 释放，残留的临时文件交给系统清理
        }
    }

    /// <summary>
    /// 构造初始化器：建表阶段只记录、不建实体；升级器与种子都记录执行顺序
    /// </summary>
    private DbInitializer CreateInitializer(
        string[] configIds,
        bool upgraderFails = false,
        bool enableDataSeeding = true,
        bool enableTableInitialization = true)
    {
        var options = Options.Create(new XiHanSqlSugarCoreOptions
        {
            DefaultConfigId = "Default",
            EnableDbInitialization = true,
            EnableTableInitialization = enableTableInitialization,
            EnableDataSeeding = enableDataSeeding
        });

        var services = new ServiceCollection();
        services.AddSingleton<IDbSchemaUpgrader>(new RecordingUpgrader(_events, upgraderFails));
        services.AddSingleton<IDataSeeder>(new RecordingSeeder(_events));

        return new DbInitializer(
            new FixedClientResolver(_client, configIds),
            services.BuildServiceProvider(),
            NullLogger<DbInitializer>.Instance,
            options,
            new NoTenant(),
            new RecordingEntityTypeProvider(_events),
            new DataSeederSelector(options));
    }

    /// <summary>
    /// 建表阶段的探针：记录被建表的连接，不返回任何实体
    /// </summary>
    private sealed class RecordingEntityTypeProvider(List<string> events) : IDbEntityTypeProvider
    {
        public IReadOnlyList<Type> GetEntityTypes(DbInitializationContext context)
        {
            events.Add($"tables:{context.ConnectionConfigId}");
            return [];
        }
    }

    /// <summary>
    /// 记录执行的升级器，可按需失败
    /// </summary>
    private sealed class RecordingUpgrader(List<string> events, bool fails) : IDbSchemaUpgrader
    {
        public Task UpgradeAsync(CancellationToken cancellationToken = default)
        {
            events.Add("upgrade");
            return fails ? throw new InvalidOperationException("升级失败") : Task.CompletedTask;
        }
    }

    /// <summary>
    /// 记录执行的种子
    /// </summary>
    private sealed class RecordingSeeder(List<string> events) : IDataSeeder
    {
        public int Order => 0;

        public string Name => "recording";

        public Task SeedAsync()
        {
            events.Add("seed");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 所有连接标识都解析到同一个 SQLite 客户端
    /// </summary>
    private sealed class FixedClientResolver(ISqlSugarClient client, string[] configIds) : ISqlSugarClientResolver
    {
        public IReadOnlyCollection<string> GetAllConfigIds() => configIds;

        public IReadOnlyList<string> GetCurrentLayoutConfigIds() => configIds;

        public ISqlSugarClient GetClient(string configId) => client;

        public ISqlSugarClient GetCurrentClient() => client;

        public ISqlSugarClient GetClientForEntity(Type entityType) => client;

        public IEnumerable<ISqlSugarClient> GetAllClients() => [client];

        public ITenant AsTenant() => throw new NotSupportedException("用例不涉及多连接容器。");
    }

    /// <summary>
    /// 无租户上下文的当前租户实现
    /// </summary>
    private sealed class NoTenant : ICurrentTenant
    {
        public bool IsAvailable => false;

        public long? Id => null;

        public string? Name => null;

        public IDisposable Change(long? id, string? name = null) => new NoopScope();

        private sealed class NoopScope : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
