// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using RabbitMQ.Client;

namespace XiHan.Framework.EventBus.RabbitMQ.Tests;

/// <summary>
/// RabbitMQ 真实 Broker 往返验证
/// </summary>
/// <remarks>
/// 交换机声明、队列绑定、QoS、消费与 Ack 只有连上真实 Broker 才有意义，用替身验证等于验证替身自己。
/// 地址取环境变量 <c>XIHAN_TEST_RABBITMQ</c>（形如 <c>host:port</c>），缺省 <c>localhost:5672</c>；
/// 账户取 <c>XIHAN_TEST_RABBITMQ_USER</c> / <c>XIHAN_TEST_RABBITMQ_PASSWORD</c>，缺省 <c>guest</c>。
/// 不可达时整组跳过。为免污染真实应用的拓扑，这里用固定的测试专用交换机与队列名。
/// </remarks>
[Collection("RabbitMQ")]
public class RabbitMQBrokerRoundTripTests : IAsyncLifetime
{
    private const string TestExchangeName = "XiHan.Tests.EventBus";
    private const string TestQueueName = "XiHan.Tests.EventBus";

    private static readonly string Endpoint =
        Environment.GetEnvironmentVariable("XIHAN_TEST_RABBITMQ") ?? "localhost:5672";

    private static readonly string BrokerUserName =
        Environment.GetEnvironmentVariable("XIHAN_TEST_RABBITMQ_USER") ?? "guest";

    private static readonly string BrokerPassword =
        Environment.GetEnvironmentVariable("XIHAN_TEST_RABBITMQ_PASSWORD") ?? "guest";

    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(20);

    private bool _reachable;

    /// <summary>
    /// 探测 Broker 可达性，带超时保护，避免网络黑洞把用例挂死
    /// </summary>
    /// <returns>任务</returns>
    public async ValueTask InitializeAsync()
    {
        var (host, port) = ParseEndpoint();
        var factory = new ConnectionFactory
        {
            HostName = host,
            Port = port,
            UserName = BrokerUserName,
            Password = BrokerPassword,
            VirtualHost = "/"
        };

        try
        {
            var connectTask = factory.CreateConnectionAsync("XiHan.Tests.Probe");
            var finished = await Task.WhenAny(connectTask, Task.Delay(ProbeTimeout));
            if (!ReferenceEquals(finished, connectTask))
            {
                // 探测超时：连接任务仍在跑，必须观测其异常，否则会成为未观测异常
                Observe(connectTask);
                return;
            }

            var connection = await connectTask;
            await connection.CloseAsync();
            await connection.DisposeAsync();
            _reachable = true;
        }
        catch (Exception)
        {
            _reachable = false;
        }
    }

    /// <summary>
    /// 释放，类级别无常驻资源
    /// </summary>
    /// <returns>任务</returns>
    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// 事件经交换机与队列往返后交给本地处理器
    /// </summary>
    /// <remarks>
    /// 这条串起了全部真实链路：声明交换机 → 声明队列 → 按事件名绑定路由键 → 发布 → 消费 → 反序列化 → 分发。
    /// 其中任何一环写错（比如绑定用了类型简名而发布用全名），事件都会被交换机静默丢弃。
    /// </remarks>
    [Fact]
    public async Task Publish_RoundTripsThroughBroker_ToLocalHandler()
    {
        SkipIfUnreachable();

        var payload = Guid.NewGuid().ToString("N");
        var context = new RabbitMQEventBusTestContext(CreateOptions());

        try
        {
            context.Bus.Subscribe(typeof(RabbitMQRoundTripEvent), new StubEventHandlerFactory(new RabbitMQRoundTripEventHandler()));
            await context.Bus.InitializeAsync();

            await PublishAsync(context, payload, correlationId: null);

            var received = await WaitForPayloadAsync(context, payload);

            Assert.NotNull(received);
            Assert.Equal(payload, received.Payload);
        }
        finally
        {
            await context.Bus.DisposeAsync();
            context.Dispose();
        }
    }

    /// <summary>
    /// 发布时携带的关联标识随消息属性到达消费端
    /// </summary>
    /// <remarks>
    /// 关联标识走的是 AMQP 的 CorrelationId 属性而不是消息体，属性丢了不会报错，
    /// 只会让消费端日志脱离上游链路，因此必须在真实 Broker 上验证它确实往返。
    /// </remarks>
    [Fact]
    public async Task Publish_CarriesCorrelationIdToConsumer()
    {
        SkipIfUnreachable();

        var payload = Guid.NewGuid().ToString("N");
        var correlationId = Guid.NewGuid().ToString("N");
        var context = new RabbitMQEventBusTestContext(CreateOptions());

        try
        {
            context.Bus.Subscribe(typeof(RabbitMQRoundTripEvent), new StubEventHandlerFactory(new RabbitMQRoundTripEventHandler()));
            await context.Bus.InitializeAsync();

            await PublishAsync(context, payload, correlationId);

            var received = await WaitForPayloadAsync(context, payload);

            Assert.NotNull(received);
            Assert.Contains(context.CorrelationIdProvider.Changes, value => value == correlationId);
        }
        finally
        {
            await context.Bus.DisposeAsync();
            context.Dispose();
        }
    }

    /// <summary>
    /// 重复初始化不会重复建连
    /// </summary>
    /// <remarks>
    /// 模块只在应用初始化阶段调一次，但发布路径在未初始化时也会兜底调用，
    /// 因此并发/重复进入必须被短路，否则每次发布都会新建连接与消费者，造成连接泄漏与重复消费。
    /// </remarks>
    [Fact]
    public async Task InitializeAsync_CalledTwice_IsIdempotent()
    {
        SkipIfUnreachable();

        var context = new RabbitMQEventBusTestContext(CreateOptions());

        try
        {
            await context.Bus.InitializeAsync();

            var exception = await Record.ExceptionAsync(() => context.Bus.InitializeAsync());

            Assert.Null(exception);
        }
        finally
        {
            await context.Bus.DisposeAsync();
            context.Dispose();
        }
    }

    /// <summary>
    /// 初始化后释放能正常关闭连接与通道
    /// </summary>
    [Fact]
    public async Task DisposeAsync_AfterInitialize_DoesNotThrow()
    {
        SkipIfUnreachable();

        var context = new RabbitMQEventBusTestContext(CreateOptions());

        try
        {
            await context.Bus.InitializeAsync();

            var exception = await Record.ExceptionAsync(() => context.Bus.DisposeAsync().AsTask());

            Assert.Null(exception);
        }
        finally
        {
            context.Dispose();
        }
    }

    /// <summary>
    /// Broker 不可达时跳过整条用例
    /// </summary>
    private void SkipIfUnreachable()
    {
        var (host, port) = ParseEndpoint();

        Assert.SkipUnless(_reachable, $"RabbitMQ 不可达（{host}:{port}），跳过该组验证。");
    }

    /// <summary>
    /// 构造指向测试专用拓扑的选项
    /// </summary>
    /// <returns>选项</returns>
    private static XiHanRabbitMQEventBusOptions CreateOptions()
    {
        var (host, port) = ParseEndpoint();

        return new XiHanRabbitMQEventBusOptions
        {
            HostName = host,
            Port = port,
            UserName = BrokerUserName,
            Password = BrokerPassword,
            VirtualHost = "/",
            ExchangeName = TestExchangeName,
            QueueName = TestQueueName,
            ClientProvidedName = "XiHan.Tests"
        };
    }

    /// <summary>
    /// 解析测试用的 Broker 地址
    /// </summary>
    /// <returns>主机名与端口</returns>
    private static (string Host, int Port) ParseEndpoint()
    {
        var parts = Endpoint.Split(':');
        var host = parts[0].Length == 0 ? "localhost" : parts[0];
        var port = parts.Length > 1 && int.TryParse(parts[1], out var parsed) ? parsed : 5672;

        return (host, port);
    }

    /// <summary>
    /// 发布一条往返事件
    /// </summary>
    /// <param name="context">测试夹具</param>
    /// <param name="payload">载荷</param>
    /// <param name="correlationId">关联标识</param>
    /// <returns>任务</returns>
    private static Task PublishAsync(RabbitMQEventBusTestContext context, string payload, string? correlationId)
    {
        return context.Bus.PublishToBrokerForTestAsync(
            typeof(RabbitMQRoundTripEvent).FullName!,
            context.Bus.SerializeForTest(new RabbitMQRoundTripEvent { Payload = payload }),
            Guid.NewGuid().ToString("N"),
            correlationId);
    }

    /// <summary>
    /// 轮询等待指定载荷被消费，超时返回空
    /// </summary>
    /// <param name="context">测试夹具</param>
    /// <param name="payload">期望载荷</param>
    /// <returns>收到的事件，超时为空</returns>
    /// <remarks>
    /// 队列上可能残留上一次运行的消息，因此只认自己这次的随机载荷，其余一概忽略。
    /// </remarks>
    private static async Task<RabbitMQRoundTripEvent?> WaitForPayloadAsync(RabbitMQEventBusTestContext context, string payload)
    {
        var deadline = DateTime.UtcNow.Add(DeliveryTimeout);

        while (DateTime.UtcNow < deadline)
        {
            foreach (var invocation in context.Invoker.Invocations)
            {
                if (invocation.EventData is RabbitMQRoundTripEvent candidate && candidate.Payload == payload)
                {
                    return candidate;
                }
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        return null;
    }

    /// <summary>
    /// 观测任务异常，避免超时后成为未观测异常
    /// </summary>
    /// <param name="task">任务</param>
    private static void Observe(Task task)
    {
        _ = task.ContinueWith(static completed => { _ = completed.Exception; }, TaskScheduler.Default);
    }
}

/// <summary>
/// RabbitMQ 测试集合，共享同一队列的用例串行执行
/// </summary>
[CollectionDefinition("RabbitMQ", DisableParallelization = true)]
public class RabbitMQTestCollection;
