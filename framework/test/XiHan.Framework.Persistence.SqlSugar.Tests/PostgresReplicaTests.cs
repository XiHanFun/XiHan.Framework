// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SqlSugar;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Persistence.Conformance.Fixtures;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 真实的 PostgreSQL 流复制主从上的读写分离（ADR-0118）
/// </summary>
/// <remarks>
/// 环境变量 <c>XIHAN_TEST_POSTGRES</c> 与 <c>XIHAN_TEST_POSTGRES_REPLICA</c>，未设置即跳过。读到哪个库看 <c>pg_is_in_recovery()</c>：从库上为真。SQLite 替身验证不了的都在这里：从库真的只读、复制真的发生、连接池里的空闲连接在从库宕了之后还打得开
/// </remarks>
public sealed class PostgresReplicaTests : IDisposable
{
    /// <summary>
    /// 判断读到哪个库的语句
    /// </summary>
    private const string InRecovery = "select pg_is_in_recovery()";

    /// <summary>
    /// 本测试实例独占的临时库
    /// </summary>
    private PostgresDatabase? _database;

    /// <inheritdoc />
    public void Dispose()
    {
        NpgsqlConnection.ClearAllPools();
        _database?.Dispose();
    }

    [Fact]
    public async Task ReadOnlyUnitOfWork_ReadsTheStandby_AndAWritingOneThePrimary()
    {
        await using var host = await StartAsync(await ReplicaAsync());

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<SqlSugarUnitOfWork>();
            await unitOfWork.BeginAsync(new UnitOfWorkOptions { IsReadOnly = true }, CancellationToken.None);
            Assert.True(IsOnStandby(unitOfWork.ConnectionFor(typeof(ConformanceOrder))));
        }

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<SqlSugarUnitOfWork>();
            await unitOfWork.BeginAsync(new UnitOfWorkOptions(), CancellationToken.None);
            Assert.False(IsOnStandby(unitOfWork.ConnectionFor(typeof(ConformanceOrder))));
        }
    }

    [Fact]
    public async Task Write_LandsOnThePrimary_AndReachesTheStandby()
    {
        await using var host = await StartAsync(await ReplicaAsync());
        var id = Guid.CreateVersion7();
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<SqlSugarUnitOfWork>();
            await unitOfWork.BeginAsync(new UnitOfWorkOptions(), CancellationToken.None);
            var orders = scope.ServiceProvider.GetRequiredService<IRepository<ConformanceOrder, Guid>>();
            (await orders.AddAsync(new ConformanceOrder { Id = id, Name = "mine" }, CancellationToken.None)).ThrowIfFailure();
            Assert.Equal("mine", (await orders.FindAsync(id, CancellationToken.None)).ValueOrThrow().Name);
            (await unitOfWork.CommitAsync(CancellationToken.None)).ThrowIfFailure();
        }

        await using var read = host.Services.CreateAsyncScope();
        var readOnly = read.ServiceProvider.GetRequiredService<SqlSugarUnitOfWork>();
        await readOnly.BeginAsync(new UnitOfWorkOptions { IsReadOnly = true }, CancellationToken.None);
        Assert.True(IsOnStandby(readOnly.ConnectionFor(typeof(ConformanceOrder))));
        Assert.Equal("mine", (await read.ServiceProvider.GetRequiredService<IRepository<ConformanceOrder, Guid>>().FindAsync(id, CancellationToken.None)).ValueOrThrow().Name);
    }

    [Fact]
    public async Task ReadThenWrite_OutsideAUnitOfWork_StillWritesToThePrimary()
    {
        // SqlSugar 读从库前记下主库的连接串、读完换回来：这里核对换回来的串能写（Npgsql 打开过的连接取连接串时可能丢掉密码）
        await using var host = await StartAsync(await ReplicaAsync());
        await using var scope = host.Services.CreateAsyncScope();
        var client = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();

        Assert.True(IsOnStandby(client));
        client.Ado.ExecuteCommand("create table written_after_read(id int)");
        Assert.True(IsOnStandby(client));
        client.Ado.ExecuteCommand("insert into written_after_read(id) values (1)");

        Assert.Equal(1, client.Ado.GetInt("select count(*) from written_after_read"));
    }

    [Fact]
    public async Task ConcurrentReadsAndWrites_StayOnTheirSide()
    {
        // 配置共用时读从库会把别的作用域的写送到从库上，真实的从库报只读错误：这里并发压一遍
        await using var host = await StartAsync(await ReplicaAsync());
        await using (var scope = host.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<ISqlSugarClient>().Ado.ExecuteCommand("create table concurrent_writes(id int)");
        }

        await Parallel.ForAsync(0, 200, new ParallelOptions { MaxDegreeOfParallelism = 16 }, async (index, _) =>
        {
            await using var scope = host.Services.CreateAsyncScope();
            var client = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
            if (index % 2 == 0)
            {
                client.Ado.ExecuteCommand($"insert into concurrent_writes(id) values ({index})");
            }
            else
            {
                Assert.True(IsOnStandby(client));
            }
        });

        await using var check = host.Services.CreateAsyncScope();
        Assert.Equal(100, check.ServiceProvider.GetRequiredService<ISqlSugarClient>().Ado.GetInt("select count(*) from concurrent_writes"));
    }

    [Fact]
    public async Task StoppedStandby_IsTakenOutOfReads_AndComesBack_EvenWithPooledConnections()
    {
        var replica = new NpgsqlConnectionStringBuilder(await ReplicaAsync());
        await using var forwarder = new TcpForwarder(replica.Host!, replica.Port);
        forwarder.Start();
        replica.Host = "127.0.0.1";
        replica.Port = forwarder.Port;
        await using var host = await StartAsync(replica.ConnectionString, new KeyValuePair<string, string?>("XiHan:Persistence:SqlSugar:Replicas:FailureCooldownSeconds", "0"));
        var probe = host.Services.GetRequiredKeyedService<PeriodicHealthContributor>(SqlSugarPersistenceFeature.ReplicaHealthName);
        Assert.Equal(LifecycleState.Ready, ReplicaStatus(host).State);
        await using (var scope = host.Services.CreateAsyncScope())
        {
            // 读一次，连接池里留下一条空闲连接：只打开连接的探针在从库宕了之后还会成功
            Assert.True(IsOnStandby(scope.ServiceProvider.GetRequiredService<ISqlSugarClient>()));
        }

        await forwarder.StopAsync();
        await probe.ProbeAsync(CancellationToken.None);

        var down = ReplicaStatus(host);
        Assert.Equal(LifecycleState.Degraded, down.State);
        Assert.Equal(PersistenceErrors.ReplicaUnavailable, down.Error?.Code);
        Assert.Equal(0, down.Data["default.available"]);
        Assert.DoesNotContain("Password", down.Error!.Message, StringComparison.OrdinalIgnoreCase);
        await using (var scope = host.Services.CreateAsyncScope())
        {
            Assert.False(IsOnStandby(scope.ServiceProvider.GetRequiredService<ISqlSugarClient>()));
        }

        forwarder.Start();
        await probe.ProbeAsync(CancellationToken.None);

        Assert.Equal(LifecycleState.Ready, ReplicaStatus(host).State);
        await using var back = host.Services.CreateAsyncScope();
        Assert.True(IsOnStandby(back.ServiceProvider.GetRequiredService<ISqlSugarClient>()));
    }

    /// <summary>
    /// 这个连接的读落在从库上吗
    /// </summary>
    private static bool IsOnStandby(ISqlSugarClient connection) => connection.Ado.GetScalar(InRecovery) is true;

    /// <summary>
    /// 从库的健康条目
    /// </summary>
    private static ContributorStatus ReplicaStatus(TestHost host) => Assert.Single(host.App.Health().Contributions, static c => c.Name == SqlSugarPersistenceFeature.ReplicaHealthName);

    /// <summary>
    /// 建临时库，返回从库上指向它的连接串
    /// </summary>
    private async Task<string> ReplicaAsync()
    {
        PostgresReplicaServer.RequireConfiguration();
        _database = PostgresTestServer.CreateDatabase();
        return await PostgresReplicaServer.ConnectionStringAsync(_database);
    }

    /// <summary>
    /// 启动配了这个从库的宿主
    /// </summary>
    /// <remarks>
    /// 探测循环不会自己跑（间隔一小时）
    /// </remarks>
    private Task<TestHost> StartAsync(string replica, params KeyValuePair<string, string?>[] configuration) => TestHost.StartAsync(
        builder => builder.UseFeature(new SqlSugarPersistenceFeature((binding, config) =>
        {
            config.DbType = DbType.PostgreSQL;
            config.ConnectionString = binding.ValueOrThrow().Value;
            config.SlaveConnectionConfigs = [new SlaveConnectionConfig { ConnectionString = replica, HitRate = 10 }];
        })),
        [.. _database!.Configuration, new("XiHan:Persistence:SqlSugar:Replicas:HealthCheckIntervalSeconds", "3600"), .. configuration],
        static services =>
        {
            services.AddSingleton(TimeProvider.System);
            services.AddRepository<ConformanceOrder, Guid>();
        });
}
