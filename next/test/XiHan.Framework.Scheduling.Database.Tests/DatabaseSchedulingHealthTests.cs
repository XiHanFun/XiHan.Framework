// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Database.Tests;

/// <summary>
/// 健康项 <c>scheduling.database.leader</c>：选项里的探测间隔解析；租约表可达时宿主启动后条目 Ready。
/// </summary>
public sealed class DatabaseSchedulingHealthTests
{
    [Fact]
    public void Options_ParseHealthProbeInterval_AndRejectNonPositive()
    {
        static IConfiguration Build(params (string Key, string Value)[] values)
            => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

        Assert.Equal(TimeSpan.FromSeconds(15), DatabaseSchedulingOptions.FromConfiguration(Build()).HealthProbeInterval);
        Assert.Equal(TimeSpan.FromSeconds(4), DatabaseSchedulingOptions.FromConfiguration(Build(("XiHan:Scheduling:Database:HealthProbeIntervalSeconds", "4"))).HealthProbeInterval);
        Assert.Throws<ArgumentException>(() => DatabaseSchedulingOptions.FromConfiguration(Build(("XiHan:Scheduling:Database:HealthProbeIntervalSeconds", "0"))));
    }

    [Fact]
    public async Task Health_IsReady_WhenTheLeaseTableIsReachable()
    {
        await using var database = new SqliteDatabase();
        var host = await database.StartHostAsync();

        var health = host.App.Health();
        var status = Assert.Single(health.Contributions, static c => c.Name == DatabaseSchedulingFeature.HealthName);
        Assert.Null(status.Error);
        Assert.Equal(LifecycleState.Ready, status.State);
        // 应用整体可能因持久化 Provider 可选的 multitenancy.sharding 缺失而是 Degraded，这里只看就绪
        Assert.True(health.Ready);
    }
}
