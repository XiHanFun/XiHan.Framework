// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Messaging.Conformance;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Messaging.RabbitMQ.Tests;

/// <summary>
/// 真实 RabbitMQ 上的拓扑：队列只由订阅方声明（ADR-0079）。
/// </summary>
/// <remarks>
/// 未设置 <c>XIHAN_TEST_RABBITMQ</c> 时跳过。每个用例用独立的队列前缀与交换机，结束时删除它们。
/// </remarks>
public sealed class RabbitMQTopologyTests : IAsyncLifetime
{
    /// <summary>
    /// 订阅的主题。
    /// </summary>
    private const string SubscribedTopic = "orders";

    /// <summary>
    /// 没人订阅的主题。
    /// </summary>
    private const string UnsubscribedTopic = "scheduling.job.started";

    /// <summary>
    /// 本用例独有的队列前缀。
    /// </summary>
    private readonly string _prefix = $"xihan-test.{Guid.NewGuid():N}.";

    /// <summary>
    /// 本用例独有的交换机。
    /// </summary>
    private readonly string _exchange = $"xihan-test-{Guid.NewGuid():N}";

    /// <summary>
    /// 用来查看拓扑的原始连接。
    /// </summary>
    private IConnection? _connection;

    /// <summary>
    /// 测试代理的 URI，没有设置环境变量时为空。
    /// </summary>
    private static string? Uri => Environment.GetEnvironmentVariable(RabbitMQMessageBrokerConformanceTests.EnvironmentVariable);

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        // 没有测试代理时什么都不做，用例自己跳过
        if (!string.IsNullOrWhiteSpace(Uri))
        {
            _connection = await new ConnectionFactory { Uri = new System.Uri(Uri) }.CreateConnectionAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_connection is null)
        {
            return;
        }

        // 用例建出的持久队列与交换机不留在测试代理上
        await using (var channel = await _connection.CreateChannelAsync().ConfigureAwait(false))
        {
            foreach (var queue in new[] { _prefix + SubscribedTopic, _prefix + UnsubscribedTopic })
            {
                await channel.QueueDeleteAsync(queue).ConfigureAwait(false);
            }

            await channel.ExchangeDeleteAsync(_exchange).ConfigureAwait(false);
        }

        await _connection.DisposeAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task PublishingToATopicNobodySubscribes_DeclaresNoQueue_WhileTheSubscriberBindsItsQueue()
    {
        SkipWithoutBroker();
        var log = new DeliveryLog();
        await using var host = await StartAsync(log);

        var dropped = await PublishAsync(host, UnsubscribedTopic, "nobody");
        var delivered = await PublishAsync(host, SubscribedTopic, "somebody");

        Assert.True(dropped.IsSuccess, dropped.ToString());
        Assert.True(delivered.IsSuccess, delivered.ToString());
        Assert.Single(await log.WaitAsync(static r => r.Delivery.Envelope.Id == "somebody", 1, TimeSpan.FromSeconds(10)));
        // 发布方不声明队列：没人订阅的主题没有队列，消息没有地方堆积
        Assert.False(await QueueExistsAsync(_prefix + UnsubscribedTopic));
        Assert.True(await QueueExistsAsync(_prefix + SubscribedTopic));
    }

    [Fact]
    public async Task TheSubscribersDurableQueue_KeepsMessagesPublishedWhileItIsOffline()
    {
        SkipWithoutBroker();
        await using (var offline = await StartAsync(new DeliveryLog()))
        {
            // 订阅方第一次启动建好持久队列与绑定，随后下线
            Assert.NotNull(offline);
        }

        await using (var publisher = await StartAsync(log: null))
        {
            (await PublishAsync(publisher, SubscribedTopic, "while-offline")).ThrowIfFailure();
        }

        var second = new DeliveryLog();
        await using var subscriber = await StartAsync(second);
        Assert.Single(await second.WaitAsync(static r => r.Delivery.Envelope.Id == "while-offline", 1, TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task TheBroadcastQueue_IsOwnedByTheInstance_AndGoesAwayWhenItStops()
    {
        SkipWithoutBroker();
        var host = await StartAsync(new DeliveryLog(), SubscriptionMode.Broadcast);
        var queue = _prefix + SubscribedTopic + "@" + host.Services.GetRequiredService<MessagingInstance>().Id;
        try
        {
            // 独占队列：别的连接被动声明它得到 405（被本实例的连接占用），说明它在、且只属于这个实例；没有建竞争队列
            Assert.Equal(405, await QueueStateAsync(queue));
            Assert.Equal(404, await QueueStateAsync(_prefix + SubscribedTopic));
        }
        finally
        {
            await host.DisposeAsync();
        }

        // 实例下线：队列随连接删除，不留下没人消费的队列
        Assert.Equal(404, await QueueStateAsync(queue));
    }

    /// <summary>
    /// 没有测试代理时跳过。
    /// </summary>
    private static void SkipWithoutBroker()
        => Assert.SkipWhen(string.IsNullOrWhiteSpace(Uri), $"Set the environment variable {RabbitMQMessageBrokerConformanceTests.EnvironmentVariable} to a RabbitMQ URI to run this test.");

    /// <summary>
    /// 启动一个宿主，<paramref name="log"/> 不为空时以 <paramref name="mode"/> 订阅 <see cref="SubscribedTopic"/>。
    /// </summary>
    private Task<TestHost> StartAsync(DeliveryLog? log, SubscriptionMode mode = SubscriptionMode.Competing)
        => TestHost.StartAsync(
            static builder => builder.UseFeature(new RabbitMQMessagingFeature()),
            [
                new("XiHan:Messaging:RabbitMQ:Configuration", Uri),
                new("XiHan:Messaging:RabbitMQ:QueuePrefix", _prefix),
                new("XiHan:Messaging:RabbitMQ:Exchange", _exchange),
            ],
            services =>
            {
                if (log is not null)
                {
                    services.AddSingleton(log);
                    services.AddMessageHandler<RecordingHandler>(SubscribedTopic, mode);
                }
            });

    /// <summary>
    /// 在新作用域内发布一条。
    /// </summary>
    private static async Task<Primitives.Result> PublishAsync(TestHost host, string topic, string id)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMessagePublisher>().PublishAsync(topic, new MessageEnvelope { Id = id, Source = "/tests", Type = "tests.topology" }, CancellationToken.None);
    }

    /// <summary>
    /// 被动声明：队列存在为真，不存在（404）为假。
    /// </summary>
    /// <remarks>
    /// 被动声明失败会关闭通道，每次用新通道。
    /// </remarks>
    private async Task<bool> QueueExistsAsync(string queue) => await QueueStateAsync(queue) == 200;

    /// <summary>
    /// 被动声明的结果：200 为存在，404 为不存在，405 为被别的连接独占。
    /// </summary>
    /// <remarks>
    /// 被动声明失败会关闭通道，每次用新通道。
    /// </remarks>
    private async Task<int> QueueStateAsync(string queue)
    {
        await using var channel = await _connection!.CreateChannelAsync();
        try
        {
            await channel.QueueDeclarePassiveAsync(queue);
            return 200;
        }
        catch (OperationInterruptedException exception) when (exception.ShutdownReason?.ReplyCode is 404 or 405)
        {
            return exception.ShutdownReason.ReplyCode;
        }
    }
}
