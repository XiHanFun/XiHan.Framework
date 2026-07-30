// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Core.Tracing;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.EventBus.Abstractions;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.Abstractions.Local;
using XiHan.Framework.EventBus.Distributed;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Timing;
using XiHan.Framework.Uow;

namespace XiHan.Framework.EventBus.RabbitMQ;

/// <summary>
/// 基于 RabbitMQ 的分布式事件总线
/// </summary>
/// <remarks>
/// 发布：把事件以 <c>事件名</c> 作为路由键投递到 direct 交换机（持久化消息）。
/// 消费：同一应用的多个实例共享同一队列，形成竞争消费，保证分布式事件在集群中只被处理一次。
/// </remarks>
[ExposeServices(typeof(IDistributedEventBus), typeof(RabbitMQDistributedEventBus))]
public class RabbitMQDistributedEventBus : BrokerDistributedEventBusBase, ISingletonDependency, IAsyncDisposable
{
    private readonly XiHanRabbitMQEventBusOptions _options;
    private readonly ILogger<RabbitMQDistributedEventBus> _logger;
    private readonly SemaphoreSlim _publishLock = new(1, 1);
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private IConnection? _connection;
    private IChannel? _publishChannel;
    private IChannel? _consumerChannel;
    private volatile bool _initialized;

    /// <summary>
    /// 构造函数
    /// </summary>
    public RabbitMQDistributedEventBus(
        IServiceScopeFactory serviceScopeFactory,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager unitOfWorkManager,
        IOptions<XiHanDistributedEventBusOptions> distributedEventBusOptions,
        IOptions<XiHanRabbitMQEventBusOptions> rabbitMqOptions,
        IDistributedIdGenerator<Guid> guidGenerator,
        IClock clock,
        IEventHandlerInvoker eventHandlerInvoker,
        ILocalEventBus localEventBus,
        ICorrelationIdProvider correlationIdProvider,
        ILogger<RabbitMQDistributedEventBus> logger)
        : base(serviceScopeFactory,
            currentTenant,
            unitOfWorkManager,
            distributedEventBusOptions,
            guidGenerator,
            clock,
            eventHandlerInvoker,
            localEventBus,
            correlationIdProvider)
    {
        _options = rabbitMqOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// 初始化：建立连接、声明交换机/队列、绑定事件路由键并启动消费者
    /// </summary>
    public override async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        await _initLock.WaitAsync();
        try
        {
            if (_initialized)
            {
                return;
            }

            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                UserName = _options.UserName,
                Password = _options.Password,
                VirtualHost = _options.VirtualHost,
                AutomaticRecoveryEnabled = true
            };

            if (!string.IsNullOrWhiteSpace(_options.Uri))
            {
                factory.Uri = new Uri(_options.Uri);
            }

            _connection = await factory.CreateConnectionAsync(_options.ClientProvidedName);
            _publishChannel = await _connection.CreateChannelAsync();
            _consumerChannel = await _connection.CreateChannelAsync();

            await _consumerChannel.ExchangeDeclareAsync(
                _options.ExchangeName,
                _options.GetExchangeTypeOrDefault(),
                durable: true);

            await _consumerChannel.QueueDeclareAsync(
                _options.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false);

            foreach (var eventName in EventTypes.Keys)
            {
                await _consumerChannel.QueueBindAsync(_options.QueueName, _options.ExchangeName, eventName);
            }

            await _consumerChannel.BasicQosAsync(0, _options.PrefetchCount, global: false);

            var consumer = new AsyncEventingBasicConsumer(_consumerChannel);
            consumer.ReceivedAsync += HandleReceivedAsync;
            await _consumerChannel.BasicConsumeAsync(_options.QueueName, autoAck: false, consumer);

            _initialized = true;
            _logger.LogInformation("RabbitMQ 分布式事件总线已初始化：exchange={Exchange}, queue={Queue}, 绑定 {Count} 个事件",
                _options.ExchangeName, _options.QueueName, EventTypes.Count);
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// 把事件推入 RabbitMQ 交换机
    /// </summary>
    protected override async Task PublishToBrokerAsync(string eventName, byte[] body, string? messageId, string? correlationId)
    {
        if (!_initialized || _publishChannel is null)
        {
            await InitializeAsync();
        }

        var properties = new BasicProperties
        {
            DeliveryMode = DeliveryModes.Persistent
        };

        if (!string.IsNullOrEmpty(messageId))
        {
            properties.MessageId = messageId;
        }

        if (!string.IsNullOrEmpty(correlationId))
        {
            properties.CorrelationId = correlationId;
        }

        await _publishLock.WaitAsync();
        try
        {
            await _publishChannel!.BasicPublishAsync(
                exchange: _options.ExchangeName,
                routingKey: eventName,
                mandatory: false,
                basicProperties: properties,
                body: body);
        }
        finally
        {
            _publishLock.Release();
        }
    }

    /// <summary>
    /// 释放连接与通道
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        try
        {
            if (_consumerChannel is not null)
            {
                await _consumerChannel.CloseAsync();
                await _consumerChannel.DisposeAsync();
            }

            if (_publishChannel is not null)
            {
                await _publishChannel.CloseAsync();
                await _publishChannel.DisposeAsync();
            }

            if (_connection is not null)
            {
                await _connection.CloseAsync();
                await _connection.DisposeAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "释放 RabbitMQ 连接时发生异常");
        }

        _publishLock.Dispose();
        _initLock.Dispose();
    }

    /// <summary>
    /// 消费者回调：处理收到的消息并 Ack / Nack
    /// </summary>
    private async Task HandleReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        try
        {
            var body = ea.Body.ToArray();
            await ProcessIncomingMessageAsync(
                ea.BasicProperties.MessageId,
                ea.RoutingKey,
                ea.BasicProperties.CorrelationId,
                body);

            if (_consumerChannel is not null)
            {
                await _consumerChannel.BasicAckAsync(ea.DeliveryTag, multiple: false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理 RabbitMQ 事件失败，事件名={EventName}", ea.RoutingKey);

            // requeue:false —— 避免毒消息无限重投；可靠重试由收件箱负责（若已配置）
            if (_consumerChannel is not null)
            {
                await _consumerChannel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
            }
        }
    }
}
