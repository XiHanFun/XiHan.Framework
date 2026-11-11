// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using XiHan.Framework.MultiTenancy.Abstractions.Sharding;
using XiHan.Framework.MultiTenancy.InMemory;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Persistence.Conformance.Fixtures;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 命令超时配置（RFC-0005「命令超时」、ADR-0119）
/// </summary>
/// <remarks>
/// <c>Ado.CommandTimeOut</c> 在特性建的客户端上，应用的配置回调碰不到，所以由配置给出；每个连接有自己的 <c>Ado</c>，默认、命名与分片连接都要设上
/// </remarks>
public sealed class SqlSugarCommandTimeoutTests : IDisposable
{
    /// <summary>
    /// 配置键
    /// </summary>
    private const string Key = "XiHan:Persistence:SqlSugar:CommandTimeoutSeconds";

    /// <summary>
    /// 默认库
    /// </summary>
    private readonly SqliteDatabase _default = new();

    /// <summary>
    /// 第二个库：命名连接或分片库
    /// </summary>
    private readonly SqliteDatabase _second = new();

    /// <inheritdoc />
    public void Dispose()
    {
        _default.Dispose();
        _second.Dispose();
    }

    [Fact]
    public async Task NotConfigured_KeepsTheSameTimeoutAsAPlainClient()
    {
        await using var host = await StartAsync();
        await using var scope = host.Services.CreateAsyncScope();
        using var plain = new SqlSugarClient(new ConnectionConfig { DbType = DbType.Sqlite, ConnectionString = _default.ConnectionString, IsAutoCloseConnection = true });

        // 不配时行为不变：与 SqlSugar 自己的缺省值相同。它变了这里会失败，提醒同步文档
        Assert.Equal(300, plain.Ado.CommandTimeOut);
        Assert.Equal(plain.Ado.CommandTimeOut, scope.ServiceProvider.GetRequiredService<ISqlSugarClient>().Ado.CommandTimeOut);
    }

    [Fact]
    public async Task Configured_ReachesTheDefaultConnection()
    {
        await using var host = await StartAsync(new KeyValuePair<string, string?>(Key, "45"));
        await using var scope = host.Services.CreateAsyncScope();

        Assert.Equal(45, scope.ServiceProvider.GetRequiredService<ISqlSugarClient>().Ado.CommandTimeOut);
    }

    [Fact]
    public async Task Configured_ReachesNamedAndShardConnections()
    {
        await using var host = await StartAsync(new KeyValuePair<string, string?>(Key, "45"), new KeyValuePair<string, string?>("XiHan:Persistence:SqlSugar:Connections:audit", _second.ConnectionString));
        await using var scope = host.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantShardScope>().Bind(new TenantShard("acme", _second.ConnectionString, "sqlite"));
        var client = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<SqlSugarUnitOfWork>();
        await unitOfWork.BeginAsync(new UnitOfWorkOptions { TenantId = "acme" }, CancellationToken.None);

        Assert.Equal(45, client.Ado.CommandTimeOut);
        Assert.Equal(45, client.AsTenant().GetConnection("audit").Ado.CommandTimeOut);
        Assert.Equal(45, client.AsTenant().GetConnection(SqlSugarConnections.Shard).Ado.CommandTimeOut);
        Assert.Equal(45, unitOfWork.ConnectionFor(typeof(ConformanceNote)).Ado.CommandTimeOut);
    }

    [Fact]
    public async Task Configured_ReachesAConnectionWithReplicas()
    {
        using var site = new ReplicatedSqlite();
        await using var host = await TestHost.StartAsync(
            builder => builder.UseFeature(ReplicatedSqlite.CreateFeature([(site.ReplicaA.ConnectionString, 10)])),
            [.. site.Configuration, new(Key, "45")]);
        await using var scope = host.Services.CreateAsyncScope();

        Assert.Equal(45, scope.ServiceProvider.GetRequiredService<ISqlSugarClient>().Ado.CommandTimeOut);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("1.5")]
    public async Task InvalidValue_FailsTheStartup_AndNamesTheKey(string value)
    {
        // 0 不是「不覆盖」：ADO.NET 的 0 是无限等待，也不能静默地按没配处理
        var thrown = await Assert.ThrowsAnyAsync<ArgumentException>(() => StartAsync(new KeyValuePair<string, string?>(Key, value)));

        Assert.Contains("CommandTimeoutSeconds", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClientTheApplicationRegisters_DoesNotTakeTheKey()
    {
        // 方式二的客户端是应用建的，这个键碰不到它；静默忽略会让应用以为超时已经生效
        var thrown = await Assert.ThrowsAnyAsync<ArgumentException>(() => TestHost.StartAsync(
            static builder => builder.UseFeature(new SqlSugarPersistenceFeature()),
            [new(Key, "45")],
            services => services.AddScoped<ISqlSugarClient>(_ => new SqlSugarClient(SqlSugarConventions.Apply(new ConnectionConfig { DbType = DbType.Sqlite, ConnectionString = _default.ConnectionString, IsAutoCloseConnection = true })))));

        Assert.Contains("CommandTimeoutSeconds", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Options_ReadTheValue()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [Key] = "45" }).Build();

        Assert.Equal(TimeSpan.FromSeconds(45), SqlSugarPersistenceOptions.FromConfiguration(configuration).CommandTimeout);
        Assert.Equal(TimeSpan.FromSeconds(300), new SqlSugarPersistenceOptions().CommandTimeout);
        Assert.Throws<ArgumentOutOfRangeException>(static () => new SqlSugarPersistenceOptions { CommandTimeout = TimeSpan.FromMilliseconds(500) });
    }

    /// <summary>
    /// 启动由特性建客户端、带租户分片能力的宿主
    /// </summary>
    private Task<TestHost> StartAsync(params KeyValuePair<string, string?>[] configuration) => TestHost.StartAsync(
        static builder => builder.UseFeature<InMemoryMultiTenancyFeature>().UseFeature(SqliteDatabase.CreateFeature()),
        [new("XiHan:Bindings:db", _default.ConnectionString), .. configuration],
        static services =>
        {
            services.AddRepository<ConformanceOrder, Guid>();
            services.AddRepository<ConformanceNote, Guid>();
        });
}
