// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client.Exceptions;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.CloudEvents;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Conformance;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Messaging.RabbitMQ.Tests;

/// <summary>
/// 真实 RabbitMQ 来自环境变量 <c>XIHAN_TEST_RABBITMQ</c>
/// </summary>
/// <remarks>
/// 如 <c>amqp://guest:guest@localhost:5672</c>。未设置时跳过需要它的用例
/// </remarks>
public sealed class RabbitMQMessageBrokerConformanceTests : MessageBrokerConformance
{
    /// <summary>
    /// 提供 RabbitMQ 连接 URI 的环境变量名
    /// </summary>
    public const string EnvironmentVariable = "XIHAN_TEST_RABBITMQ";

    /// <summary>
    /// 本次运行独有的队列前缀，避免与其他运行的队列互相干扰
    /// </summary>
    private readonly string _prefix = $"xihan-test.{Guid.NewGuid():N}.";

    /// <inheritdoc />
    protected override IEnumerable<KeyValuePair<string, string?>> Configuration =>
    [
        new("XiHan:Messaging:RabbitMQ:Configuration", Environment.GetEnvironmentVariable(EnvironmentVariable)),
        new("XiHan:Messaging:RabbitMQ:QueuePrefix", _prefix),
    ];

    /// <inheritdoc />
    protected override IFeature CreateFeature()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvironmentVariable)), $"Set the environment variable {EnvironmentVariable} to a RabbitMQ URI to run this test.");
        return new RabbitMQMessagingFeature();
    }

    /// <inheritdoc />
    /// <remarks>
    /// 死信队列是 <c>&lt;QueuePrefix&gt;&lt;topic&gt;&lt;DeadLetterSuffix&gt;</c>，第一次死信时才声明。用自己的连接读：先被动声明确认队列存在（不存在即还没有死信），再逐条取出、不自动确认，读完整批退回队列，队列内容不变
    /// </remarks>
    protected override async ValueTask<IReadOnlyList<MessageEnvelope>> ReadDeadLettersAsync(TestHost host, string topic, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        var options = host.Services.GetRequiredService<RabbitMQMessagingOptions>();
        var queue = options.QueuePrefix + topic + host.Services.GetRequiredService<MessagingOptions>().DeadLetterSuffix;
        await using var connection = await RabbitMQMessageBroker.CreateFactory(options).CreateConnectionAsync(cancellationToken);

        // 被动声明失败会关闭通道：用单独的通道探，探到 404 就是还没有死信
        await using (var probe = await connection.CreateChannelAsync(cancellationToken: cancellationToken))
        {
            try
            {
                await probe.QueueDeclarePassiveAsync(queue, cancellationToken);
            }
            catch (OperationInterruptedException exception) when (exception.ShutdownReason?.ReplyCode == 404)
            {
                return [];
            }
        }

        var envelopes = new List<MessageEnvelope>();
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        ulong last = 0;
        while (await channel.BasicGetAsync(queue, autoAck: false, cancellationToken) is { } received)
        {
            last = received.DeliveryTag;
            // 无法解码的死信（原样转入的坏消息）不在套件的断言范围里，跳过
            if (CloudEventsFormat.Decode(received.Body).TryGetValue(out var envelope))
            {
                envelopes.Add(envelope);
            }
        }

        if (last != 0)
        {
            // 读到的全部退回：死信留在队列里，下一次读到的还是它们
            await channel.BasicNackAsync(last, multiple: true, requeue: true, cancellationToken);
        }

        return envelopes;
    }
}
