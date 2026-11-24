// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.CloudEvents;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Conformance;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Messaging.Kafka.Tests;

/// <summary>
/// 让 <see cref="KafkaMessagingFeature"/> 对真实 Kafka 跑完整套队列与流用例
/// </summary>
/// <remarks>
/// 未设置 <c>XIHAN_TEST_KAFKA</c> 时跳过。每个用例用独立的主题前缀与消费组，互不干扰
/// </remarks>
public sealed class KafkaMessagingConformanceTests : MessageStreamConformance
{
    /// <summary>
    /// 指向测试 Kafka 引导地址的环境变量名
    /// </summary>
    public const string EnvironmentVariable = "XIHAN_TEST_KAFKA";

    /// <summary>
    /// 读死信主题时元数据、水位查询与单次取消息的超时
    /// </summary>
    private static readonly TimeSpan _operationTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 本用例的主题前缀与消费组
    /// </summary>
    private readonly string _suffix = Guid.NewGuid().ToString("N")[..12];

    /// <inheritdoc />
    protected override TimeSpan DeliveryTimeout => TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    protected override IEnumerable<KeyValuePair<string, string?>> Configuration =>
    [
        new("XiHan:Messaging:Kafka:BootstrapServers", Environment.GetEnvironmentVariable(EnvironmentVariable)),
        new("XiHan:Messaging:Kafka:TopicPrefix", $"xihan-test.{_suffix}."),
        new("XiHan:Messaging:Kafka:ConsumerGroup", $"xihan-test-{_suffix}"),
    ];

    /// <inheritdoc />
    protected override IFeature CreateFeature()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvironmentVariable)), $"Set the environment variable {EnvironmentVariable} to Kafka bootstrap servers to run this test.");
        return new KafkaMessagingFeature();
    }

    /// <inheritdoc />
    /// <remarks>
    /// 死信主题是 <c>&lt;TopicPrefix&gt;&lt;topic&gt;&lt;DeadLetterSuffix&gt;</c>。用一个不提交位点的独立消费者从头读到调用时的高水位，与代理的重放同样的读法；主题不存在或没有消息时为空。客户端的调用是阻塞的，放到线程池上
    /// </remarks>
    protected override async ValueTask<IReadOnlyList<MessageEnvelope>> ReadDeadLettersAsync(TestHost host, string topic, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        var options = host.Services.GetRequiredService<KafkaMessagingOptions>();
        var deadLetterTopic = options.TopicPrefix + topic + host.Services.GetRequiredService<MessagingOptions>().DeadLetterSuffix;
        return await Task.Run(() => ReadFromBeginning(options, deadLetterTopic, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// 把一个主题从最早位点读到此刻的高水位，返回能解码的信封
    /// </summary>
    private static List<MessageEnvelope> ReadFromBeginning(KafkaMessagingOptions options, string kafkaTopic, CancellationToken cancellationToken)
    {
        var client = new ClientConfig(new Dictionary<string, string>(options.Properties, StringComparer.Ordinal)) { BootstrapServers = options.BootstrapServers };
        List<TopicPartition> partitions;
        using (var admin = new AdminClientBuilder(client).Build())
        {
            var described = admin.GetMetadata(kafkaTopic, _operationTimeout).Topics.FirstOrDefault(t => t.Topic == kafkaTopic);
            if (described is null || described.Error.IsError || described.Partitions.Count == 0)
            {
                // 死信主题在第一次死信时才建出：不存在就是还没有死信
                return [];
            }

            partitions = [.. described.Partitions.Select(p => new TopicPartition(kafkaTopic, p.PartitionId))];
        }

        // 独立消费组、不提交位点：每次都从头读，不影响别的消费者，也不取走死信
        var config = new ConsumerConfig(client)
        {
            GroupId = $"xihan-test-dead-letters-{Guid.NewGuid():N}",
            EnableAutoCommit = false,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnablePartitionEof = false,
        };
        using var consumer = new ConsumerBuilder<string?, byte[]>(config).Build();
        var pending = new Dictionary<TopicPartition, long>();
        foreach (var partition in partitions)
        {
            var watermarks = consumer.QueryWatermarkOffsets(partition, _operationTimeout);
            if (watermarks.High.Value > watermarks.Low.Value)
            {
                pending[partition] = watermarks.High.Value;
            }
        }

        var envelopes = new List<MessageEnvelope>();
        if (pending.Count == 0)
        {
            return envelopes;
        }

        consumer.Assign(pending.Keys.Select(static partition => new TopicPartitionOffset(partition, Offset.Beginning)));
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var consumed = consumer.Consume(_operationTimeout);
            if (consumed?.Message is null)
            {
                // 到高水位之前一个超时内什么都没取到：不再等，把已经读到的交回去，由套件的轮询决定要不要再读
                break;
            }

            // 无法解码的死信（原样转入的坏消息）不在套件的断言范围里，跳过
            if (CloudEventsFormat.Decode(consumed.Message.Value).TryGetValue(out var envelope))
            {
                envelopes.Add(envelope);
            }

            // 读到调用时的高水位就不再读该分区
            if (pending.TryGetValue(consumed.TopicPartition, out var end) && consumed.Offset.Value + 1 >= end)
            {
                pending.Remove(consumed.TopicPartition);
            }
        }

        consumer.Close();
        return envelopes;
    }
}
