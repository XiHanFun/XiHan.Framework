// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Messaging.Kafka.Tests;

/// <summary>
/// 健康项 <c>messaging.kafka.connection</c>：选项里的探测间隔解析；真实 Kafka 可用时宿主启动后条目 Ready（未设置 <c>XIHAN_TEST_KAFKA</c> 时跳过）。
/// </summary>
public sealed class KafkaMessagingHealthTests
{
    [Fact]
    public void Options_ParseHealthProbeInterval_AndRejectNonPositive()
    {
        static IConfiguration Build(params (string Key, string Value)[] values)
            => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

        Assert.Equal(TimeSpan.FromSeconds(15), KafkaMessagingOptions.FromConfiguration(Build(("XiHan:Messaging:Kafka:BootstrapServers", "localhost:9092")), default).HealthProbeInterval);
        Assert.Equal(TimeSpan.FromSeconds(3), KafkaMessagingOptions.FromConfiguration(Build(("XiHan:Messaging:Kafka:BootstrapServers", "localhost:9092"), ("XiHan:Messaging:Kafka:HealthProbeIntervalSeconds", "3")), default).HealthProbeInterval);
        Assert.Throws<ArgumentException>(() => KafkaMessagingOptions.FromConfiguration(Build(("XiHan:Messaging:Kafka:BootstrapServers", "localhost:9092"), ("XiHan:Messaging:Kafka:HealthProbeIntervalSeconds", "-1")), default));
    }

    [Fact]
    public async Task Health_ReportsConnectionReady_AgainstARealCluster()
    {
        var servers = Environment.GetEnvironmentVariable(KafkaMessagingConformanceTests.EnvironmentVariable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(servers), $"Set the environment variable {KafkaMessagingConformanceTests.EnvironmentVariable} to Kafka bootstrap servers to run this test.");
        var suffix = Guid.NewGuid().ToString("N")[..12];
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<KafkaMessagingFeature>(),
            [
                new("XiHan:Messaging:Kafka:BootstrapServers", servers),
                new("XiHan:Messaging:Kafka:TopicPrefix", $"xihan-test.{suffix}."),
                new("XiHan:Messaging:Kafka:GroupId", $"xihan-test-{suffix}"),
            ]);

        var health = host.App.Health();
        var status = Assert.Single(health.Contributions, static c => c.Name == KafkaMessagingFeature.HealthName);
        Assert.Equal(LifecycleState.Ready, status.State);
        Assert.Null(status.Error);
        Assert.True(health.Ready);
    }
}
