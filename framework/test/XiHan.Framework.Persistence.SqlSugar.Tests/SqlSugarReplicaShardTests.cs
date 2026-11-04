// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.MultiTenancy.Abstractions.Sharding;
using XiHan.Framework.MultiTenancy.InMemory;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Persistence.Conformance.Fixtures;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 从库不串到别的库（RFC-0005「读写分离」、ADR-0118）
/// </summary>
/// <remarks>
/// 回调对每个连接都调用：不看 <c>ConfigId</c> 就把默认连接的从库挂上去，租户的读就读到了平台库。这里核对分片连接与命名连接：要么用自己的从库，要么没有从库，否则失败
/// </remarks>
public sealed class SqlSugarReplicaShardTests : IDisposable
{
    /// <summary>
    /// 平台库：一主两从
    /// </summary>
    private readonly ReplicatedSqlite _site = new();

    /// <summary>
    /// 租户 <c>acme</c> 的分片库
    /// </summary>
    private readonly SqliteDatabase _shard = new();

    /// <summary>
    /// 分片库自己的从库
    /// </summary>
    private readonly SqliteDatabase _shardReplica = new();

    /// <summary>
    /// 给分片库写上名字
    /// </summary>
    public SqlSugarReplicaShardTests()
    {
        ReplicatedSqlite.Seed(_shard, "shard");
        ReplicatedSqlite.Seed(_shardReplica, "shard-replica");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _site.Dispose();
        _shard.Dispose();
        _shardReplica.Dispose();
    }

    [Fact]
    public async Task ShardWithoutReplicas_ReadsItsOwnPrimary_WhileTheDefaultConnectionKeepsItsReplica()
    {
        // 回调只给默认连接配从库：这是 BasicApp 的接法
        await using var host = await StartAsync((_, config) =>
        {
            if (Equals(config.ConfigId, SqlSugarConnections.Default))
            {
                config.SlaveConnectionConfigs = [new SlaveConnectionConfig { ConnectionString = _site.ReplicaA.ConnectionString, HitRate = 10 }];
            }
        });
        await using var scope = host.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantShardScope>().Bind(new TenantShard("acme", _shard.ConnectionString, "sqlite"));
        var unitOfWork = scope.ServiceProvider.GetRequiredService<SqlSugarUnitOfWork>();
        await unitOfWork.BeginAsync(new UnitOfWorkOptions("acme", IsReadOnly: true), CancellationToken.None);

        Assert.Equal("shard", unitOfWork.ConnectionFor(typeof(ConformanceNote)).Ado.GetString(ReplicatedSqlite.ReadMarker));
        Assert.Equal("replica-a", unitOfWork.ConnectionFor(typeof(ConformanceOrder)).Ado.GetString(ReplicatedSqlite.ReadMarker));
    }

    [Fact]
    public async Task ShardGivenTheDefaultConnectionsReplica_FailsTheCall_InsteadOfReadingAnotherDatabase()
    {
        // 回调不看 ConfigId：分片连接也拿到了平台库的从库
        await using var host = await StartAsync((_, config) => config.SlaveConnectionConfigs = [new SlaveConnectionConfig { ConnectionString = _site.ReplicaA.ConnectionString, HitRate = 10 }]);
        await using var scope = host.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantShardScope>().Bind(new TenantShard("acme", _shard.ConnectionString, "sqlite"));

        var thrown = Assert.Throws<FrameworkException>(() => scope.ServiceProvider.GetRequiredService<ISqlSugarClient>());

        Assert.Equal(PersistenceErrors.ReplicaMisplaced, thrown.Code);
        Assert.Contains("'shard'", thrown.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(_site.ReplicaA.Path, thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShardOnTheSameDatabase_MayShareItsReplica()
    {
        await using var host = await StartAsync((_, config) => config.SlaveConnectionConfigs = [new SlaveConnectionConfig { ConnectionString = _site.ReplicaA.ConnectionString, HitRate = 10 }]);
        await using var scope = host.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantShardScope>().Bind(new TenantShard("acme", _site.Primary.ConnectionString, "sqlite"));

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISqlSugarClient>());
    }

    [Fact]
    public async Task ShardWithItsOwnReplica_ReadsIt_AndAWritingUnitOfWorkStaysOnTheShardPrimary()
    {
        // 从库的连接串来自分片绑定，这个连接自己的；分片连接的配置每个作用域现建，不进探测
        await using var host = await StartAsync((binding, config) =>
        {
            if (binding.TryGetValue(out var value) && value.Properties.TryGetValue("replica", out var replica))
            {
                config.SlaveConnectionConfigs = [new SlaveConnectionConfig { ConnectionString = replica, HitRate = 0 }];
            }
        });
        var shard = new TenantShard("acme", _shard.ConnectionString, "sqlite", new Dictionary<string, string>(StringComparer.Ordinal) { ["replica"] = _shardReplica.ConnectionString });

        await using (var scope = host.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<TenantShardScope>().Bind(shard);
            var unitOfWork = scope.ServiceProvider.GetRequiredService<SqlSugarUnitOfWork>();
            await unitOfWork.BeginAsync(new UnitOfWorkOptions("acme", IsReadOnly: true), CancellationToken.None);
            var connection = unitOfWork.ConnectionFor(typeof(ConformanceNote));

            Assert.Equal("shard-replica", connection.Ado.GetString(ReplicatedSqlite.ReadMarker));
            // 权重同样归一化：SqlSugar 对全是 0 的权重直接抛异常
            Assert.Equal(SqlSugarReplicaOptions.StandardHitRate, Assert.Single(connection.CurrentConnectionConfig.SlaveConnectionConfigs).HitRate);
        }

        await using (var scope = host.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<TenantShardScope>().Bind(shard);
            var unitOfWork = scope.ServiceProvider.GetRequiredService<SqlSugarUnitOfWork>();
            await unitOfWork.BeginAsync(new UnitOfWorkOptions("acme"), CancellationToken.None);

            Assert.Equal("shard", unitOfWork.ConnectionFor(typeof(ConformanceNote)).Ado.GetString(ReplicatedSqlite.ReadMarker));
        }

        Assert.Null(host.Services.GetService<SqlSugarReplicaMonitor>());
    }

    [Fact]
    public async Task NamedConnectionGivenTheDefaultConnectionsReplica_FailsTheStartup()
    {
        var thrown = await Assert.ThrowsAnyAsync<FrameworkException>(() => TestHost.StartAsync(
            builder => builder.UseFeature(ReplicatedSqlite.CreateFeature([(_site.ReplicaA.ConnectionString, 10)])),
            [.. _site.Configuration, new("XiHan:Persistence:SqlSugar:Connections:audit", _shard.ConnectionString)]));

        Assert.Equal(PersistenceErrors.ReplicaMisplaced, thrown.Code);
    }

    [Fact]
    public async Task NamedConnectionOnTheSameDatabase_MayShareTheReplica()
    {
        await using var host = await TestHost.StartAsync(
            builder => builder.UseFeature(ReplicatedSqlite.CreateFeature([(_site.ReplicaA.ConnectionString, 10)])),
            [.. _site.Configuration, new("XiHan:Persistence:SqlSugar:Connections:audit", _site.Primary.ConnectionString)]);

        Assert.NotNull(host.Services.GetRequiredService<SqlSugarReplicaMonitor>());
    }

    [Fact]
    public async Task NamedConnectionWithItsOwnReplica_IsProbedAndRouted()
    {
        var named = new SqlSugarPersistenceFeature((binding, config) =>
        {
            config.DbType = DbType.Sqlite;
            config.ConnectionString = binding.ValueOrThrow().Value;
            config.SlaveConnectionConfigs = Equals(config.ConfigId, "audit")
                ? [new SlaveConnectionConfig { ConnectionString = _shardReplica.ConnectionString, HitRate = 10 }]
                : [new SlaveConnectionConfig { ConnectionString = _site.ReplicaA.ConnectionString, HitRate = 10 }];
        });
        await using var host = await TestHost.StartAsync(
            builder => builder.UseFeature(named),
            [.. _site.Configuration, new("XiHan:Persistence:SqlSugar:Connections:audit", _shard.ConnectionString)]);
        await using var scope = host.Services.CreateAsyncScope();
        var client = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();

        Assert.Equal("shard-replica", client.AsTenant().GetConnection("audit").Ado.GetString(ReplicatedSqlite.ReadMarker));
        Assert.Equal("replica-a", client.Ado.GetString(ReplicatedSqlite.ReadMarker));
        var status = Assert.Single(host.App.Health().Contributions, static c => c.Name == SqlSugarPersistenceFeature.ReplicaHealthName);
        Assert.Equal(1, status.Data["default.available"]);
        Assert.Equal(1, status.Data["audit.available"]);
    }

    /// <summary>
    /// 启动带租户分片能力的宿主
    /// </summary>
    /// <param name="replicas">回调里配从库的部分</param>
    private Task<TestHost> StartAsync(Action<Maybe<BindingValue>, ConnectionConfig> replicas) => TestHost.StartAsync(
        builder => builder.UseFeature<InMemoryMultiTenancyFeature>().UseFeature(new SqlSugarPersistenceFeature((binding, config) =>
        {
            config.DbType = DbType.Sqlite;
            config.ConnectionString = binding.ValueOrThrow().Value;
            replicas(binding, config);
        })),
        _site.Configuration,
        static services =>
        {
            services.AddRepository<ConformanceOrder, Guid>();
            services.AddRepository<ConformanceNote, Guid>();
        });
}
