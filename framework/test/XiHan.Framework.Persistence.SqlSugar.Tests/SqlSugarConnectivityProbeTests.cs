// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SqlSugar;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.DataSources;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 数据库连通性探测走一次往返，并且有上限（ADR-0123）
/// </summary>
/// <remarks>
/// 不回话的服务器不需要数据库，总是跑；连接池里的空闲连接要真实的 PostgreSQL 才有，环境变量 <c>XIHAN_TEST_POSTGRES</c> 未设置即跳过。服务器经 <see cref="TcpForwarder"/> 连，停掉转发就是服务宕了，连接池里的连接还在
/// </remarks>
public sealed class SqlSugarConnectivityProbeTests : IDisposable
{
    /// <summary>
    /// 用例等探测结束的上限
    /// </summary>
    /// <remarks>
    /// 远小于 <see cref="StalledTcpServer"/> 配的驱动超时，远大于配置的单次超时：只断言不会卡到驱动超时，不断言精确时长
    /// </remarks>
    private static readonly TimeSpan _bound = TimeSpan.FromSeconds(60);

    /// <summary>
    /// 应用自己的库
    /// </summary>
    private readonly SqliteDatabase _application = new();

    /// <summary>
    /// 本测试实例独占的 PostgreSQL 临时库
    /// </summary>
    private PostgresDatabase? _database;

    /// <inheritdoc />
    public void Dispose()
    {
        NpgsqlConnection.ClearAllPools();
        _database?.Dispose();
        _application.Dispose();
    }

    [Fact]
    public async Task DatabaseItem_IsReady_OnAReachableDatabase()
    {
        await using var host = await _application.StartHostAsync();

        Assert.Equal(LifecycleState.Ready, DatabaseItem(host).State);
    }

    [Fact]
    public async Task DatabaseItem_ReportsTheFailureType_OnAnUnreachableDatabase()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"xihan-missing-{Guid.NewGuid():N}", "app.db");
        await using var host = await StartAsync(DbType.Sqlite, $"Data Source={missing}");

        var status = DatabaseItem(host);

        Assert.Equal(LifecycleState.Degraded, status.State);
        Assert.Equal(KernelErrors.HealthProbeFailed, status.Error?.Code);
        Assert.Equal(SqlSugarPersistenceFeature.HealthName, status.Error?.Details?["name"]);
        Assert.Equal(typeof(Microsoft.Data.Sqlite.SqliteException).FullName, status.Error?.Details?["reason"]);
    }

    [Fact]
    public async Task DatabaseItem_StalledServer_DoesNotHoldTheHostStart()
    {
        using var server = new StalledTcpServer();

        // 首探同步卡住时启动根本不返回，超时等不到：直接量启动花了多久
        var started = Stopwatch.GetTimestamp();
        await using var host = await StartAsync(DbType.PostgreSQL, server.PostgresConnectionString);
        Assert.True(Stopwatch.GetElapsedTime(started) < _bound);

        var status = DatabaseItem(host);
        Assert.Equal(LifecycleState.Degraded, status.State);
        Assert.Equal(KernelErrors.HealthProbeFailed, status.Error?.Code);
    }

    [Fact]
    public async Task TestConnection_StalledServer_EndsAtTheProbeTimeout()
    {
        using var server = new StalledTcpServer();
        await using var host = await StartDataSourcesAsync();
        var registry = host.Services.GetRequiredService<DataSourceRegistry>();

        var tested = await registry.TestAsync(new DataSourceDefinition("stalled", DatabaseTypes.PostgreSql, server.PostgresConnectionString), TestContext.Current.CancellationToken).AsTask().WaitAsync(_bound, TestContext.Current.CancellationToken);

        Assert.Equal(PersistenceErrors.DataSourceUnavailable, tested.Error?.Code);
        Assert.Contains("did not finish", (string)tested.Error!.Details!["reason"]!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TestConnection_CallerCancels_Throws()
    {
        // 配的超时比用例的上限长，结束只能是调用方的令牌：取消不是连不上
        using var server = new StalledTcpServer();
        await using var host = await StartDataSourcesAsync(intervalSeconds: 600);
        var registry = host.Services.GetRequiredService<DataSourceRegistry>();
        using var cancellation = new CancellationTokenSource();

        var testing = registry.TestAsync(new DataSourceDefinition("stalled", DatabaseTypes.PostgreSql, server.PostgresConnectionString), cancellation.Token).AsTask();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => testing.WaitAsync(_bound, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DataSourceItem_StalledServer_IsDegraded_WithinTheProbeTimeout()
    {
        using var server = new StalledTcpServer();
        await using var host = await StartDataSourcesAsync();
        var registry = host.Services.GetRequiredService<DataSourceRegistry>();

        (await registry.GetOrAddAsync(new DataSourceDefinition("stalled", DatabaseTypes.PostgreSql, server.PostgresConnectionString), TestContext.Current.CancellationToken)).ValueOrThrow();
        await registry.ProbeAsync(TestContext.Current.CancellationToken).AsTask().WaitAsync(_bound, TestContext.Current.CancellationToken);

        var status = DataSourceItem(registry, "stalled");
        Assert.Equal(LifecycleState.Degraded, status.State);
        Assert.Equal(PersistenceErrors.DataSourceUnavailable, status.Error?.Code);
    }

    [Fact]
    public async Task DatabaseItem_GoesDegraded_WhenPostgresIsGone_DespiteAnIdlePooledConnection()
    {
        await using var forwarder = Forward(out var connectionString);
        await using var host = await StartAsync(DbType.PostgreSQL, connectionString);
        // 首探之后连接池里留着一条空闲连接，只打开连接的探测拿到的就是它
        Assert.Equal(LifecycleState.Ready, DatabaseItem(host).State);
        var probe = host.Services.GetRequiredKeyedService<PeriodicHealthContributor>(SqlSugarPersistenceFeature.HealthName);

        await forwarder.StopAsync();
        await probe.ProbeAsync(CancellationToken.None);

        var down = DatabaseItem(host);
        Assert.Equal(LifecycleState.Degraded, down.State);
        Assert.Equal(KernelErrors.HealthProbeFailed, down.Error?.Code);

        forwarder.Start();
        await probe.ProbeAsync(CancellationToken.None);
        Assert.Equal(LifecycleState.Ready, DatabaseItem(host).State);
    }

    [Fact]
    public async Task TestConnection_Fails_WhenPostgresIsGone_DespiteAnIdlePooledConnection()
    {
        await using var forwarder = Forward(out var connectionString);
        await using var host = await StartDataSourcesAsync();
        var registry = host.Services.GetRequiredService<DataSourceRegistry>();
        var definition = new DataSourceDefinition("pg", DatabaseTypes.PostgreSql, connectionString);
        Assert.True((await registry.TestAsync(definition, TestContext.Current.CancellationToken)).IsSuccess);

        await forwarder.StopAsync();

        var tested = await registry.TestAsync(definition, TestContext.Current.CancellationToken);
        Assert.Equal(PersistenceErrors.DataSourceUnavailable, tested.Error?.Code);
    }

    [Fact]
    public async Task DataSourceItem_GoesDegraded_WhenPostgresIsGone_DespiteAnIdlePooledConnection()
    {
        await using var forwarder = Forward(out var connectionString);
        await using var host = await StartDataSourcesAsync();
        var registry = host.Services.GetRequiredService<DataSourceRegistry>();
        (await registry.GetOrAddAsync(new DataSourceDefinition("pg", DatabaseTypes.PostgreSql, connectionString), TestContext.Current.CancellationToken)).ValueOrThrow();
        await registry.ProbeAsync(TestContext.Current.CancellationToken);
        Assert.Equal(LifecycleState.Ready, DataSourceItem(registry, "pg").State);

        await forwarder.StopAsync();
        await registry.ProbeAsync(TestContext.Current.CancellationToken);

        var down = DataSourceItem(registry, "pg");
        Assert.Equal(LifecycleState.Degraded, down.State);
        Assert.Equal(PersistenceErrors.DataSourceUnavailable, down.Error?.Code);
    }

    /// <summary>
    /// 健康项 <c>persistence.sqlsugar.database</c>
    /// </summary>
    private static ContributorStatus DatabaseItem(TestHost host)
        => Assert.Single(host.App.Health().Contributions, static c => c.Name == SqlSugarPersistenceFeature.HealthName);

    /// <summary>
    /// 数据源的健康项
    /// </summary>
    private static ContributorStatus DataSourceItem(DataSourceRegistry registry, string name)
        => Assert.Single(registry.Check(), status => status.Name == $"persistence.datasource.{name}");

    /// <summary>
    /// 启动缺省连接指向给定库的宿主，单次探测超时 1 秒
    /// </summary>
    private static Task<TestHost> StartAsync(DbType dbType, string connectionString) => TestHost.StartAsync(
        builder => builder.UseFeature(new SqlSugarPersistenceFeature((binding, config) =>
        {
            config.DbType = dbType;
            config.ConnectionString = binding.ValueOrThrow().Value;
        })),
        [new("XiHan:Bindings:db", connectionString), new("XiHan:Persistence:SqlSugar:HealthProbeIntervalSeconds", "1")]);

    /// <summary>
    /// 启动能登记运行时数据源的宿主
    /// </summary>
    /// <param name="intervalSeconds">运行时数据源的探测间隔，也是单次超时</param>
    private Task<TestHost> StartDataSourcesAsync(int intervalSeconds = 1) => TestHost.StartAsync(
        static builder => builder.UseFeature(SqliteDatabase.CreateFeature()),
        [.. _application.Configuration, new("XiHan:Persistence:DataSources:HealthProbeIntervalSeconds", intervalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture))]);

    /// <summary>
    /// 建一个 PostgreSQL 临时库并经转发器连它
    /// </summary>
    private TcpForwarder Forward(out string connectionString)
    {
        _database = PostgresTestServer.CreateDatabase();
        var target = new NpgsqlConnectionStringBuilder(_database.ConnectionString);
        var forwarder = new TcpForwarder(target.Host!, target.Port);
        forwarder.Start();
        target.Host = "127.0.0.1";
        target.Port = forwarder.Port;
        connectionString = target.ConnectionString;
        return forwarder;
    }
}
