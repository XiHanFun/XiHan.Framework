// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using SqlSugar;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 从库的权重归一化、周期探测、摘除与恢复（RFC-0005「读写分离」、ADR-0118）
/// </summary>
/// <remarks>
/// 从库宕机用 SQLite 打不开文件模拟：连接串指向不存在的目录。目录建出来就恢复了，探测都是真的打开连接、执行语句
/// </remarks>
public sealed class SqlSugarReplicaHealthTests : IDisposable
{
    /// <summary>
    /// 探测间隔：长得循环不会自己跑，探测由用例显式触发
    /// </summary>
    private const string Interval = "3600";

    /// <summary>
    /// 一主两从
    /// </summary>
    private readonly ReplicatedSqlite _site = new();

    /// <summary>
    /// 还不存在的目录，代表宕机的从库
    /// </summary>
    private readonly string _missing = Path.Combine(Path.GetTempPath(), $"xihan-replica-{Guid.NewGuid():N}");

    /// <summary>
    /// 宕机从库的连接串
    /// </summary>
    private string Down => $"Data Source={Path.Combine(_missing, "replica.db")}";

    /// <inheritdoc />
    public void Dispose()
    {
        SqliteDatabase.ClearPool(Down);
        if (Directory.Exists(_missing))
        {
            Directory.Delete(_missing, recursive: true);
        }

        _site.Dispose();
    }

    [Fact]
    public async Task NoReplicas_HasNoMonitor_NoHealthItem_AndScopesShareOneConfig()
    {
        using var database = new SqliteDatabase();
        await using var host = await TestHost.StartAsync(static b => b.UseFeature(SqliteDatabase.CreateFeature()), [new("XiHan:Bindings:db", database.ConnectionString)]);

        Assert.Null(host.Services.GetService<SqlSugarReplicaMonitor>());
        Assert.Null(host.Services.GetKeyedService<PeriodicHealthContributor>(SqlSugarPersistenceFeature.ReplicaHealthName));
        Assert.DoesNotContain(host.App.Health().Contributions, static c => c.Name == SqlSugarPersistenceFeature.ReplicaHealthName);
        await using var first = host.Services.CreateAsyncScope();
        await using var second = host.Services.CreateAsyncScope();
        Assert.Same(ClientOf(first).CurrentConnectionConfig, ClientOf(second).CurrentConnectionConfig);
    }

    [Fact]
    public async Task EmptyReplicaList_CountsAsNoReplicas()
    {
        await using var host = await TestHost.StartAsync(static b => b.UseFeature(ReplicatedSqlite.CreateFeature([])), _site.Configuration);

        Assert.Null(host.Services.GetService<SqlSugarReplicaMonitor>());
    }

    [Fact]
    public async Task Weights_NotAboveZero_AreNormalized_AndExplicitOnesKept()
    {
        await using var host = await TestHost.StartAsync(
            builder => builder.UseFeature(ReplicatedSqlite.CreateFeature([(_site.ReplicaA.ConnectionString, 0), (_site.ReplicaB.ConnectionString, 7)])),
            [.. _site.Configuration, new("XiHan:Persistence:SqlSugar:Replicas:DefaultHitRate", "25")]);
        await using var scope = host.Services.CreateAsyncScope();

        Assert.Equal([25, 7], ClientOf(scope).CurrentConnectionConfig.SlaveConnectionConfigs.Select(static replica => replica.HitRate));
    }

    [Fact]
    public void Conventions_NormalizeWeights_ForAClientTheApplicationBuilds()
    {
        var config = SqlSugarConventions.Apply(new ConnectionConfig { DbType = DbType.Sqlite, ConnectionString = "Data Source=a.db", SlaveConnectionConfigs = [new SlaveConnectionConfig { ConnectionString = "Data Source=b.db", HitRate = 0 }] });

        Assert.Equal(SqlSugarReplicaOptions.StandardHitRate, Assert.Single(config.SlaveConnectionConfigs).HitRate);
    }

    [Fact]
    public async Task BlankReplicaConnectionString_FailsTheStartup_WithoutNamingASecret()
    {
        var thrown = await Assert.ThrowsAnyAsync<ArgumentException>(() => TestHost.StartAsync(static b => b.UseFeature(ReplicatedSqlite.CreateFeature([(" ", 10)])), _site.Configuration));

        Assert.Contains("'default'", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HealthyReplicas_AreReady_AndServeReads()
    {
        await using var host = await StartAsync(new FakeTimeProvider(), _site.ReplicaA.ConnectionString, _site.ReplicaB.ConnectionString);

        var status = ReplicaStatus(host);
        Assert.Equal(LifecycleState.Ready, status.State);
        Assert.Null(status.Error);
        Assert.Equal(2, status.Data["default.replicas"]);
        Assert.Equal(2, status.Data["default.available"]);
        Assert.Equal("available", status.Data["default.0"]);
        await using var scope = host.Services.CreateAsyncScope();
        Assert.StartsWith("replica-", ClientOf(scope).Ado.GetString(ReplicatedSqlite.ReadMarker), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReplicaDownAtStart_IsTakenOutOfReads_AndHealthOnlyShowsTheExceptionType()
    {
        await using var host = await StartAsync(new FakeTimeProvider(), Down, _site.ReplicaB.ConnectionString);

        await using var scope = host.Services.CreateAsyncScope();
        var replica = Assert.Single(ClientOf(scope).CurrentConnectionConfig.SlaveConnectionConfigs);
        Assert.Equal(_site.ReplicaB.ConnectionString, replica.ConnectionString);
        Assert.Equal("replica-b", ClientOf(scope).Ado.GetString(ReplicatedSqlite.ReadMarker));

        var status = ReplicaStatus(host);
        Assert.Equal(LifecycleState.Degraded, status.State);
        Assert.Equal(PersistenceErrors.ReplicaUnavailable, status.Error?.Code);
        Assert.Equal("default", status.Error?.Details?["connection"]);
        Assert.Equal(0, status.Error?.Details?["index"]);
        Assert.Equal(typeof(SqliteException).FullName, status.Error?.Details?["reason"]);
        Assert.Equal("unavailable", status.Data["default.0"]);
        Assert.Equal(typeof(SqliteException).FullName, status.Data["default.0.reason"]);
        Assert.Equal(1, status.Data["default.available"]);
        // 连接串是机密，不进错误、不进数据
        Assert.DoesNotContain(_missing, status.Error!.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(status.Data.Values, value => value is string text && text.Contains(_missing, StringComparison.Ordinal));
        // 从库不可读只是降级：应用仍然就绪
        Assert.True(host.App.Health().Ready);
        Assert.Equal(LifecycleState.Degraded, host.App.Health().State);
    }

    [Fact]
    public async Task EveryReplicaDown_ReadsGoToThePrimary_AndWritesStillWork()
    {
        await using var host = await StartAsync(new FakeTimeProvider(), Down);

        await using var scope = host.Services.CreateAsyncScope();
        var client = ClientOf(scope);
        // 列表必须是空的，不能是一串权重 0 的从库：SqlSugar 对空的候选集取随机数直接抛异常
        Assert.Null(client.CurrentConnectionConfig.SlaveConnectionConfigs);
        Assert.Equal("primary", client.Ado.GetString(ReplicatedSqlite.ReadMarker));
        client.Ado.ExecuteCommand("insert into marker(name) values ('written')");
        Assert.Equal(["primary", "written"], ReplicatedSqlite.Markers(_site.Primary));
    }

    [Fact]
    public async Task RecoveredReplica_WaitsOutTheCooldown_BeforeServingReadsAgain()
    {
        var clock = new FakeTimeProvider();
        await using var host = await StartAsync(clock, Down);
        Directory.CreateDirectory(_missing);
        ReplicatedSqlite.Seed(Down, "recovered");
        var probe = Probe(host);

        // 探通了，但离上次失败还不到冷却窗口：不放读流量
        clock.Advance(TimeSpan.FromSeconds(60));
        await probe.ProbeAsync(CancellationToken.None);
        Assert.Equal("cooling-down", ReplicaStatus(host).Data["default.0"]);
        Assert.Equal(LifecycleState.Degraded, ReplicaStatus(host).State);
        await using (var cooling = host.Services.CreateAsyncScope())
        {
            Assert.Equal("primary", ClientOf(cooling).Ado.GetString(ReplicatedSqlite.ReadMarker));
        }

        clock.Advance(TimeSpan.FromSeconds(61));
        await probe.ProbeAsync(CancellationToken.None);
        var status = ReplicaStatus(host);
        Assert.Equal(LifecycleState.Ready, status.State);
        Assert.Equal("available", status.Data["default.0"]);
        Assert.False(status.Data.ContainsKey("default.0.reason"));
        await using var recovered = host.Services.CreateAsyncScope();
        Assert.Equal("recovered", ClientOf(recovered).Ado.GetString(ReplicatedSqlite.ReadMarker));
    }

    [Fact]
    public async Task FailureDuringTheCooldown_RestartsIt()
    {
        var clock = new FakeTimeProvider();
        await using var host = await StartAsync(clock, Down);
        var probe = Probe(host);

        clock.Advance(TimeSpan.FromSeconds(100));
        await probe.ProbeAsync(CancellationToken.None);
        Directory.CreateDirectory(_missing);
        ReplicatedSqlite.Seed(Down, "recovered");
        clock.Advance(TimeSpan.FromSeconds(100));
        await probe.ProbeAsync(CancellationToken.None);

        // 第二次失败在 100 秒时，这时才过去 100 秒，冷却窗口按最后一次失败算
        Assert.Equal("cooling-down", ReplicaStatus(host).Data["default.0"]);
    }

    [Fact]
    public async Task ReplicaThatFailsLater_LeavesNewScopes_WhileAnOpenScopeKeepsItsSnapshot()
    {
        Directory.CreateDirectory(_missing);
        ReplicatedSqlite.Seed(Down, "replica-c");
        var clock = new FakeTimeProvider();
        await using var host = await StartAsync(clock, Down, _site.ReplicaB.ConnectionString);
        Assert.Equal(LifecycleState.Ready, ReplicaStatus(host).State);
        await using var open = host.Services.CreateAsyncScope();
        Assert.Equal(2, ClientOf(open).CurrentConnectionConfig.SlaveConnectionConfigs.Count);

        SqliteDatabase.ClearPool(Down);
        Directory.Delete(_missing, recursive: true);
        await Probe(host).ProbeAsync(CancellationToken.None);

        await using var fresh = host.Services.CreateAsyncScope();
        Assert.Equal(_site.ReplicaB.ConnectionString, Assert.Single(ClientOf(fresh).CurrentConnectionConfig.SlaveConnectionConfigs).ConnectionString);
        // 已经开着的作用域拿的是自己的快照，不会读到一半换了列表；作用域是一次调用，很快就结束
        Assert.Equal(2, ClientOf(open).CurrentConnectionConfig.SlaveConnectionConfigs.Count);
    }

    [Fact]
    public async Task Logs_NameTheReplica_FirstFailureAsWarning_RepeatsAsDebug_AndNeverTheConnectionString()
    {
        var clock = new FakeTimeProvider();
        var logs = new LogCapture();
        await using var host = await StartAsync(clock, logs, Down);
        await Probe(host).ProbeAsync(CancellationToken.None);
        Directory.CreateDirectory(_missing);
        ReplicatedSqlite.Seed(Down, "recovered");
        clock.Advance(TimeSpan.FromSeconds(121));
        await Probe(host).ProbeAsync(CancellationToken.None);

        var entries = logs.Entries.Where(static entry => entry.Category == typeof(SqlSugarReplicaMonitor).FullName).ToList();
        Assert.Equal([LogLevel.Warning, LogLevel.Error, LogLevel.Debug, LogLevel.Information, LogLevel.Information], entries.Select(static entry => entry.Level));
        Assert.Contains("'default'", entries[0].Message, StringComparison.Ordinal);
        Assert.IsType<SqliteException>(entries[0].Exception);
        Assert.All(entries, entry => Assert.DoesNotContain(_missing, entry.Message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task HealthCheckOff_HasNoHealthItem_AndTheReplicaStaysInReads()
    {
        await using var host = await TestHost.StartAsync(
            builder => builder.UseFeature(ReplicatedSqlite.CreateFeature([(Down, 10)])),
            [.. _site.Configuration, new("XiHan:Persistence:SqlSugar:Replicas:HealthCheck", "false")]);

        Assert.Null(host.Services.GetKeyedService<PeriodicHealthContributor>(SqlSugarPersistenceFeature.ReplicaHealthName));
        Assert.DoesNotContain(host.App.Health().Contributions, static c => c.Name == SqlSugarPersistenceFeature.ReplicaHealthName);
        await using var scope = host.Services.CreateAsyncScope();
        Assert.Single(ClientOf(scope).CurrentConnectionConfig.SlaveConnectionConfigs);
    }

    [Fact]
    public void Options_AreReadFromConfiguration()
    {
        var options = SqlSugarPersistenceOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["XiHan:Persistence:SqlSugar:Replicas:DefaultHitRate"] = "25",
            ["XiHan:Persistence:SqlSugar:Replicas:HealthCheck"] = "false",
            ["XiHan:Persistence:SqlSugar:Replicas:HealthCheckIntervalSeconds"] = "5",
            ["XiHan:Persistence:SqlSugar:Replicas:FailureCooldownSeconds"] = "0",
        }).Build());

        Assert.Equal(25, options.Replicas.DefaultHitRate);
        Assert.False(options.Replicas.HealthCheck);
        Assert.Equal(TimeSpan.FromSeconds(5), options.Replicas.HealthCheckInterval);
        Assert.Equal(TimeSpan.Zero, options.Replicas.FailureCooldown);
    }

    [Fact]
    public void Options_HaveTheDocumentedDefaults()
    {
        var options = new SqlSugarPersistenceOptions().Replicas;

        Assert.Equal(10, options.DefaultHitRate);
        Assert.True(options.HealthCheck);
        Assert.Equal(TimeSpan.FromSeconds(30), options.HealthCheckInterval);
        Assert.Equal(TimeSpan.FromSeconds(120), options.FailureCooldown);
    }

    [Theory]
    [InlineData("DefaultHitRate", "0")]
    [InlineData("HealthCheck", "yes")]
    [InlineData("HealthCheckIntervalSeconds", "0")]
    [InlineData("FailureCooldownSeconds", "-1")]
    public void Options_InvalidValues_NameTheKey(string key, string value)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [$"XiHan:Persistence:SqlSugar:Replicas:{key}"] = value }).Build();

        var thrown = Assert.Throws<ArgumentException>(() => SqlSugarPersistenceOptions.FromConfiguration(configuration));

        Assert.Contains(key, thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 启动配了这些从库的宿主
    /// </summary>
    /// <remarks>
    /// 探测循环不会自己跑（间隔一小时），冷却窗口 120 秒
    /// </remarks>
    private Task<TestHost> StartAsync(TimeProvider clock, params string[] replicas) => StartAsync(clock, null, replicas);

    /// <summary>
    /// 启动配了这些从库的宿主，日志写到 <paramref name="logs"/>
    /// </summary>
    private Task<TestHost> StartAsync(TimeProvider clock, LogCapture? logs, params string[] replicas) => TestHost.StartAsync(
        builder => builder.UseFeature(ReplicatedSqlite.CreateFeature(replicas.Select(static replica => (replica, 10)))),
        [.. _site.Configuration, new("XiHan:Persistence:SqlSugar:Replicas:HealthCheckIntervalSeconds", Interval)],
        services =>
        {
            services.AddSingleton(clock);
            if (logs is not null)
            {
                services.AddSingleton<ILoggerFactory>(logs);
            }
        });

    /// <summary>
    /// 作用域的客户端
    /// </summary>
    private static ISqlSugarClient ClientOf(AsyncServiceScope scope) => scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();

    /// <summary>
    /// 从库的健康条目
    /// </summary>
    private static ContributorStatus ReplicaStatus(TestHost host) => Assert.Single(host.App.Health().Contributions, static c => c.Name == SqlSugarPersistenceFeature.ReplicaHealthName);

    /// <summary>
    /// 从库的探针
    /// </summary>
    private static PeriodicHealthContributor Probe(TestHost host) => host.Services.GetRequiredKeyedService<PeriodicHealthContributor>(SqlSugarPersistenceFeature.ReplicaHealthName);
}
