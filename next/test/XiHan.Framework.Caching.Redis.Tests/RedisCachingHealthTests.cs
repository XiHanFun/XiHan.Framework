// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Caching.InMemory;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Caching.Redis.Tests;

/// <summary>
/// 健康项 <c>caching.redis.connection</c>。
/// </summary>
/// <remarks>
/// 选项里的探测间隔解析；真实 Redis 可用时宿主启动后条目 Ready（未设置 <c>XIHAN_TEST_REDIS</c> 时跳过）。
/// </remarks>
public sealed class RedisCachingHealthTests
{
    [Fact]
    public void Options_ParseHealthProbeInterval_AndRejectNonPositive()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
        [
            new("XiHan:Caching:Redis:Configuration", "localhost:6379"),
            new("XiHan:Caching:Redis:HealthProbeIntervalSeconds", "2.5"),
        ]).Build();

        Assert.Equal(TimeSpan.FromSeconds(2.5), RedisCachingOptions.FromConfiguration(configuration, default).HealthProbeInterval);
        Assert.Equal(TimeSpan.FromSeconds(15), new RedisCachingOptions { Configuration = "localhost:6379" }.HealthProbeInterval);
        Assert.Throws<ArgumentException>(() => RedisCachingOptions.FromConfiguration(
            new ConfigurationBuilder().AddInMemoryCollection([new("XiHan:Caching:Redis:Configuration", "localhost:6379"), new("XiHan:Caching:Redis:HealthProbeIntervalSeconds", "0")]).Build(),
            default));
    }

    [Fact]
    public async Task Health_ReportsConnectionReady_AgainstARealServer()
    {
        var configuration = RedisTestServer.RequireConfiguration();
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<InMemoryCachingFeature>().UseFeature<RedisCachingFeature>(),
            [new("XiHan:Caching:Redis:Configuration", configuration), new("XiHan:Caching:Redis:KeyPrefix", $"xihan-test:{Guid.NewGuid():N}:")]);

        var health = host.App.Health();
        var status = Assert.Single(health.Contributions, static c => c.Name == RedisCachingFeature.HealthName);
        Assert.Equal(LifecycleState.Ready, status.State);
        Assert.Null(status.Error);
        Assert.True(health.Ready);
    }
}
