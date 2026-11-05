// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Messaging.RabbitMQ.Tests;

/// <summary>
/// 健康项 <c>messaging.rabbitmq.connection</c>
/// </summary>
/// <remarks>
/// 选项里的探测间隔解析；真实 RabbitMQ 可用时宿主启动后条目 Ready（未设置 <c>XIHAN_TEST_RABBITMQ</c> 时跳过）
/// </remarks>
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

    [Fact]
    public async Task Health_IsReady_WhenTheBindingSuppliesTheCredentials_AgainstARealBroker()
    {
        var uri = Environment.GetEnvironmentVariable(RabbitMQMessageBrokerConformanceTests.EnvironmentVariable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(uri), $"Set the environment variable {RabbitMQMessageBrokerConformanceTests.EnvironmentVariable} to a RabbitMQ URI to run this test.");
        var real = new Uri(uri!);
        var userInfo = real.UserInfo.Split(':', 2);
        Assert.SkipWhen(userInfo.Length != 2, "This test needs credentials in the RabbitMQ URI.");

        // URI 里的凭据换成错的，正确的用户名与密码只由绑定 queue 的属性给出：能连上说明绑定凭据覆盖了配置节（RFC-0016）
        var wrong = new UriBuilder(real) { UserName = "nobody", Password = "wrong" }.Uri.AbsoluteUri;
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<RabbitMQMessagingFeature>(),
            [
                new("XiHan:Messaging:RabbitMQ:Configuration", wrong),
                new("XiHan:Bindings:queue:username", Uri.UnescapeDataString(userInfo[0])),
                new("XiHan:Bindings:queue:password", Uri.UnescapeDataString(userInfo[1])),
                new("XiHan:Messaging:RabbitMQ:QueuePrefix", $"xihan-test.{Guid.NewGuid():N}."),
            ]);

        var status = Assert.Single(host.App.Health().Contributions, static c => c.Name == RabbitMQMessagingFeature.HealthName);
        Assert.Equal(LifecycleState.Ready, status.State);
        Assert.Null(status.Error);
    }
}
