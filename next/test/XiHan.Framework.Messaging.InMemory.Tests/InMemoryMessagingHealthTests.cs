// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Conformance;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Messaging.InMemory.Tests;

/// <summary>
/// 健康项 <c>messaging.inmemory.capacity</c>。
/// </summary>
/// <remarks>
/// 宿主启动后 Ready，某个主题的有界队列排满后 Degraded 并带 <see cref="MessagingErrors.QueueFull"/>。
/// </remarks>
public sealed class InMemoryMessagingHealthTests
{
    [Fact]
    public async Task Health_IsReadyUntilATopicQueueIsFull_ThenDegraded()
    {
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<InMemoryMessagingFeature>(),
            [new("XiHan:Messaging:InMemory:MaxQueuedMessages", "1")],
            static services =>
            {
                services.AddSingleton(new DeliveryLog());
                services.AddMessageHandler<RecordingHandler>("orders");
            });

        var ready = Assert.Single(host.App.Health().Contributions, static c => c.Name == InMemoryMessagingFeature.HealthName);
        Assert.Equal(LifecycleState.Ready, ready.State);

        // 停止消费后发布到订阅了的主题：消息留在通道里，一条就把容量为 1 的队列排满（没人订阅的主题直接丢弃，排不满）
        await host.Services.GetRequiredService<MessagingRuntime>().StopAsync(CancellationToken.None);
        var broker = host.Services.GetRequiredService<InMemoryMessageBroker>();
        var published = await broker.PublishAsync("orders", new MessageEnvelope { Id = "1", Source = "/tests", Type = "orders.placed" }, CancellationToken.None);
        Assert.True(published.IsSuccess);

        var health = host.App.Health();
        var degraded = Assert.Single(health.Contributions, static c => c.Name == InMemoryMessagingFeature.HealthName);
        Assert.Equal(LifecycleState.Degraded, degraded.State);
        Assert.Equal(MessagingErrors.QueueFull, degraded.Error?.Code);
        Assert.Equal("orders", degraded.Error?.Details?["topic"]);
        Assert.Equal(1, degraded.Error?.Details?["capacity"]);
        Assert.True(health.Ready);
    }
}
