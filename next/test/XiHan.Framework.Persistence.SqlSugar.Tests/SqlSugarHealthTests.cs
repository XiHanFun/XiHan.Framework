// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 健康项 <c>persistence.sqlsugar.database</c>：选项里的探测间隔解析；数据库可达时 Ready，连接串指向不存在的目录时 Degraded 并带 <see cref="PersistenceErrors.StoreUnavailable"/>，应用仍就绪。
/// </summary>
public sealed class SqlSugarHealthTests : IDisposable
{
    /// <summary>
    /// 本测试实例独占的临时 SQLite 数据库。
    /// </summary>
    private readonly SqliteDatabase _database = new();

    /// <inheritdoc />
    public void Dispose() => _database.Dispose();

    [Fact]
    public void Options_ParseHealthProbeInterval_AndRejectNonPositive()
    {
        static IConfiguration Build(params (string Key, string Value)[] values)
            => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

        Assert.Equal(TimeSpan.FromSeconds(15), SqlSugarPersistenceOptions.FromConfiguration(Build()).HealthProbeInterval);
        Assert.Equal(TimeSpan.FromSeconds(2), SqlSugarPersistenceOptions.FromConfiguration(Build(("XiHan:Persistence:SqlSugar:HealthProbeIntervalSeconds", "2"))).HealthProbeInterval);
        Assert.Throws<ArgumentException>(() => SqlSugarPersistenceOptions.FromConfiguration(Build(("XiHan:Persistence:SqlSugar:HealthProbeIntervalSeconds", "-3"))));
    }

    [Fact]
    public async Task Health_IsReady_WhenTheDatabaseIsReachable()
    {
        await using var host = await _database.StartHostAsync();

        var health = host.App.Health();
        var status = Assert.Single(health.Contributions, static c => c.Name == SqlSugarPersistenceFeature.HealthName);
        Assert.Null(status.Error);
        Assert.Equal(LifecycleState.Ready, status.State);
        // 应用整体可能因可选的 multitenancy.sharding 缺失而是 Degraded，这里只看就绪
        Assert.True(health.Ready);
    }

    [Fact]
    public async Task Health_IsDegraded_WhenTheDatabaseCannotBeOpened()
    {
        // 连接串指向不存在的目录：SQLite 打不开文件，IsValidConnection 为 false；不建表，启动本身不碰数据库
        var missing = Path.Combine(Path.GetTempPath(), "xihan-missing-" + Guid.NewGuid().ToString("N"), "health.db");
        await using var host = await TestHost.StartAsync(static b => b.UseFeature(SqliteDatabase.CreateFeature()), [new("XiHan:Bindings:db", $"Data Source={missing}")]);

        var health = host.App.Health();
        var status = Assert.Single(health.Contributions, static c => c.Name == SqlSugarPersistenceFeature.HealthName);
        Assert.Equal(LifecycleState.Degraded, status.State);
        Assert.Equal(PersistenceErrors.StoreUnavailable, status.Error?.Code);
        Assert.True(health.Ready);
        Assert.Equal(LifecycleState.Degraded, health.State);
    }
}
