// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net.Sockets;

namespace XiHan.Framework.EventBus.Kafka.Tests;

/// <summary>
/// Kafka 真实 Broker 往返验证
/// </summary>
/// <remarks>
/// 建连接、建主题、生产、消费、提交偏移这几件事无法脱离 Broker 验证，也不该用假替身糊过去。
/// 地址取环境变量 <c>XIHAN_TEST_KAFKA</c>，缺省 <c>localhost:9092</c>；先做一次 TCP 探测，
/// 不可达时整类显式跳过，CI 不会因为没有 Kafka 而变红。
/// 每次运行都用随机主题与随机消费者组，避免污染既有数据或与并发运行互相干扰。
/// 所有等待都带超时，绝不允许把流水线挂死。
/// </remarks>
[Collection("Kafka")]
public class KafkaBrokerRoundTripTests
{
    /// <summary>
    /// Broker 地址
    /// </summary>
    private static readonly string BootstrapServers =
        Environment.GetEnvironmentVariable("XIHAN_TEST_KAFKA") ?? "localhost:9092";

    /// <summary>
    /// Broker 可达性探测结果，整个测试进程只探测一次
    /// </summary>
    private static readonly Lazy<bool> BrokerReachable = new(Probe);

    /// <summary>
    /// 事件经由真实 Broker 往返后被本实例的处理器收到
    /// </summary>
    /// <remarks>
    /// 这条路径串起了发布端（事件名作 Key、载荷作 Value、messageId 与关联标识入消息头）
    /// 与消费端（同组竞争消费、手动提交偏移）的全部约定。
    /// </remarks>
    [Fact]
    public async Task PublishAsync_ThroughRealBroker_ReachesSubscribedHandler()
    {
        Assert.SkipUnless(BrokerReachable.Value, $"Kafka 不可达（{BootstrapServers}），跳过该组验证。");

        await using var host = new KafkaEventBusTestHost(new XiHanKafkaEventBusOptions
        {
            BootstrapServers = BootstrapServers,
            TopicName = $"xihan.test.{Guid.NewGuid():N}",
            GroupId = $"xihan-test-{Guid.NewGuid():N}",
            AutoOffsetReset = "earliest",
            EnsureTopicExists = true
        });

        host.Bus.Subscribe(typeof(KafkaTestEvent), new SingleInstanceHandlerFactory(new KafkaTestEventHandler()));

        await host.Bus.InitializeAsync()
            .WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);

        // 已初始化后再次调用应直接返回，不会重复建连接与消费循环
        await host.Bus.InitializeAsync()
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await host.Bus
            .PublishAsync(typeof(KafkaTestEvent), new KafkaTestEvent { Payload = "round-trip" }, onUnitOfWorkComplete: false, useOutbox: false)
            .WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);

        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (host.Invoker.Invocations.Count == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(200, TestContext.Current.CancellationToken);
        }

        var invocation = Assert.Single(host.Invoker.Invocations.ToArray());

        Assert.Equal(typeof(KafkaTestEvent), invocation.EventType);
        Assert.Equal("round-trip", Assert.IsType<KafkaTestEvent>(invocation.EventData).Payload);
    }

    /// <summary>
    /// 探测 Broker 的第一个地址是否可建立 TCP 连接
    /// </summary>
    /// <returns>是否可达</returns>
    private static bool Probe()
    {
        var address = BootstrapServers.Split(',')[0].Trim();
        var separator = address.LastIndexOf(':');
        var hostName = separator > 0 ? address[..separator] : address;
        var port = separator > 0 && int.TryParse(address[(separator + 1)..], out var parsed) ? parsed : 9092;

        try
        {
            using var client = new TcpClient();

            return client.ConnectAsync(hostName, port).Wait(TimeSpan.FromMilliseconds(800));
        }
        catch (Exception)
        {
            return false;
        }
    }
}

/// <summary>
/// Kafka 测试集合，需要真实 Broker 的用例串行执行
/// </summary>
[CollectionDefinition("Kafka", DisableParallelization = true)]
public class KafkaTestCollection;
