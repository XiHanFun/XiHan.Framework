// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Primitives;
using Xunit;

namespace XiHan.Framework.Messaging.Conformance;

/// <summary>
/// <c>messaging.stream@1</c> 的一致性套件。
/// </summary>
/// <remarks>
/// 在 <see cref="MessageBrokerConformance"/> 全部用例之上，再验证同分区键有序与按时刻重放。提供流能力的 Provider 继承本类即可，不必再继承队列套件。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class MessageStreamConformance : MessageBrokerConformance
{
    /// <summary>
    /// 用例 S1。
    /// </summary>
    /// <remarks>
    /// 同一分区键的消息按发布顺序投递（两个键交错批量发布，各自的顺序都保持）。
    /// </remarks>
    [Fact]
    public async Task PartitionKey_PreservesOrder_PerKey()
    {
        var log = new DeliveryLog();
        await using var host = await StartAsync(log).ConfigureAwait(false);
        var envelopes = new List<MessageEnvelope>();
        for (var index = 0; index < 10; index++)
        {
            envelopes.Add(Envelope($"key-a-{index:D2}", "orders.keyed").WithExtension(EnvelopeExtensions.PartitionKey, "a"));
            envelopes.Add(Envelope($"key-b-{index:D2}", "orders.keyed").WithExtension(EnvelopeExtensions.PartitionKey, "b"));
        }

        var results = await PublishManyAsync(host, OrdersTopic, envelopes).ConfigureAwait(false);
        Assert.All(results, static r => Assert.True(r.IsSuccess, r.ToString()));

        var received = await log.WaitAsync(static r => r.Delivery.Envelope.Id.StartsWith("key-", StringComparison.Ordinal), envelopes.Count, DeliveryTimeout).ConfigureAwait(false);
        Assert.Equal(Enumerable.Range(0, 10).Select(static i => $"key-a-{i:D2}"), received.Select(static r => r.Delivery.Envelope.Id).Where(static id => id.StartsWith("key-a-", StringComparison.Ordinal)));
        Assert.Equal(Enumerable.Range(0, 10).Select(static i => $"key-b-{i:D2}"), received.Select(static r => r.Delivery.Envelope.Id).Where(static id => id.StartsWith("key-b-", StringComparison.Ordinal)));
        Assert.All(received, static r => Assert.True(r.Delivery.Envelope.Extensions.ContainsKey(EnvelopeExtensions.PartitionKey)));
    }

    /// <summary>
    /// 用例 S2。
    /// </summary>
    /// <remarks>
    /// 按时刻重放把该时刻起的消息再投递一遍，正常消费不受影响；没有消息的时段重放成功且不投递。
    /// </remarks>
    [Fact]
    public async Task Replay_RedeliversMessagesSince()
    {
        var log = new DeliveryLog();
        await using var host = await StartAsync(log).ConfigureAwait(false);
        var before = DateTimeOffset.UtcNow.AddMinutes(-1);
        foreach (var id in new[] { "rp-1", "rp-2", "rp-3" })
        {
            (await PublishAsync(host, OrdersTopic, Envelope(id, "orders.replay")).ConfigureAwait(false)).ThrowIfFailure();
        }

        _ = await log.WaitAsync(static r => r.Delivery.Envelope.Id.StartsWith("rp-", StringComparison.Ordinal), 3, DeliveryTimeout).ConfigureAwait(false);

        var stream = host.Services.GetRequiredService<IMessageReplayer>();
        (await stream.ReplayAsync(OrdersTopic, before, CancellationToken.None).ConfigureAwait(false)).ThrowIfFailure();
        var replayed = await log.WaitAsync(static r => r.Delivery.Envelope.Id.StartsWith("rp-", StringComparison.Ordinal), 6, DeliveryTimeout).ConfigureAwait(false);
        Assert.Equal(["rp-1", "rp-2", "rp-3"], replayed.Skip(3).Select(static r => r.Delivery.Envelope.Id));
        Assert.All(replayed, static r => Assert.Equal(1, r.Delivery.Attempt));

        // 未来时刻之后没有消息：成功且不再投递
        (await stream.ReplayAsync(OrdersTopic, DateTimeOffset.UtcNow.AddMinutes(5), CancellationToken.None).ConfigureAwait(false)).ThrowIfFailure();
        await Task.Delay(300).ConfigureAwait(false);
        Assert.Equal(6, log.All.Count(static r => r.Delivery.Envelope.Id.StartsWith("rp-", StringComparison.Ordinal)));
    }
}
