// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Messaging.RabbitMQ.Tests;

/// <summary>
/// 健康项 <c>messaging.rabbitmq.connection</c>：选项里的探测间隔解析；真实 RabbitMQ 可用时宿主启动后条目 Ready（未设置 <c>XIHAN_TEST_RABBITMQ</c> 时跳过）。
/// </summary>
public sealed class RabbitMQMessagingHealthTests
{
    [Fact]
    public void Options_ParseHealthProbeInterval_AndRejectNonPositive()
    {
        static IConfiguration Build(params (string Key, string Value)[] values)
            => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

        Assert.Equal(TimeSpan.FromSeconds(15), RabbitMQMessagingOptions.FromConfiguration(Build(("XiHan:Messaging:RabbitMQ:Configuration", "amqp://localhost:5672")), default).HealthProbeInterval);
        Assert.Equal(TimeSpan.FromSeconds(3), RabbitMQMessagingOptions.FromConfiguration(Build(("XiHan:Messaging:RabbitMQ:Configuration", "amqp://localhost:5672"), ("XiHan:Messaging:RabbitMQ:HealthProbeIntervalSeconds", "3")), default).HealthProbeInterval);
        Assert.Throws<ArgumentException>(() => RabbitMQMessagingOptions.FromConfiguration(Build(("XiHan:Messaging:RabbitMQ:Configuration", "amqp://localhost:5672"), ("XiHan:Messaging:RabbitMQ:HealthProbeIntervalSeconds", "abc")), default));
    }

    [Fact]
    public async Task Health_ReportsConnectionReady_AgainstARealBroker()
    {
        var uri = Environment.GetEnvironmentVariable(RabbitMQMessageBrokerConformanceTests.EnvironmentVariable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(uri), $"Set the environment variable {RabbitMQMessageBrokerConformanceTests.EnvironmentVariable} to a RabbitMQ URI to run this test.");
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<RabbitMQMessagingFeature>(),
            [new("XiHan:Messaging:RabbitMQ:Configuration", uri), new("XiHan:Messaging:RabbitMQ:QueuePrefix", $"xihan-test.{Guid.NewGuid():N}.")]);

        var health = host.App.Health();
        var status = Assert.Single(health.Contributions, static c => c.Name == RabbitMQMessagingFeature.HealthName);
        Assert.Equal(LifecycleState.Ready, status.State);
        Assert.Null(status.Error);
        Assert.True(health.Ready);
    }
}
