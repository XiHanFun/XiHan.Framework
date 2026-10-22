// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.RabbitMQ;

/// <summary>
/// 基于 RabbitMQ.Client 的代理：每主题一个持久队列，手动确认，失败重新发布到队尾或死信队列。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class RabbitMQMessageBroker : IMessageBroker, IAsyncDisposable
{
    /// <summary>
    /// 携带尝试次数的消息头；重新发布时加一。
    /// </summary>
    private const string AttemptHeader = "x-xihan-attempt";

    /// <summary>
    /// RabbitMQ 选项：连接、队列前缀与预取数。
    /// </summary>
    private readonly RabbitMQMessagingOptions _options;

    /// <summary>
    /// 消息选项：最多投递次数与死信后缀。
    /// </summary>
    private readonly MessagingOptions _messaging;

    /// <summary>
    /// 应用；只在其进入服务状态后才投递。
    /// </summary>
    private readonly App _app;

    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 串行化发布通道访问的信号量；通道不是线程安全的。
    /// </summary>
    private readonly SemaphoreSlim _publishGate = new(1, 1);

    /// <summary>
    /// 已声明过的队列名；避免每次发布都声明。
    /// </summary>
    private readonly HashSet<string> _declared = new(StringComparer.Ordinal);

    /// <summary>
    /// 每个被消费主题的通道与消费者标签；停止时逐个取消并关闭。
    /// </summary>
    private readonly List<(IChannel Channel, string ConsumerTag)> _consumers = [];

    /// <summary>
    /// 连接；<see cref="ConnectAsync"/> 建立，释放时关闭。
    /// </summary>
    private IConnection? _connection;

    /// <summary>
    /// 发布专用通道。
    /// </summary>
    private IChannel? _publishChannel;

    /// <summary>
    /// 停止信号；取消后进行中的投递不再确认。
    /// </summary>
    private CancellationTokenSource? _stopping;

    /// <summary>
    /// 创建代理；连接在 <see cref="ConnectAsync"/> 时建立。
    /// </summary>
    public RabbitMQMessageBroker(RabbitMQMessagingOptions options, MessagingOptions messaging, App app, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _messaging = messaging ?? throw new ArgumentNullException(nameof(messaging));
        _app = app ?? throw new ArgumentNullException(nameof(app));
        _logger = loggerFactory.CreateLogger<RabbitMQMessageBroker>();
    }

    /// <summary>
    /// 是否已连接。
    /// </summary>
    public bool IsConnected => _connection?.IsOpen == true;

    /// <summary>
    /// 建立连接与发布通道。
    /// </summary>
    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
        {
            return;
        }

        try
        {
            var factory = new ConnectionFactory { Uri = new Uri(_options.Configuration), ConsumerDispatchConcurrency = 1 };
            _connection = await factory.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
            _publishChannel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is RabbitMQClientException or IOException)
        {
            throw Unavailable("Failed to connect to RabbitMQ", exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result> PublishAsync(string topic, MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        TopicName.Validate(topic);
        await PublishAsync(QueueOf(topic), envelope, attempt: 1, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task StartAsync(IReadOnlyCollection<string> topics, MessageDispatch dispatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(topics);
        ArgumentNullException.ThrowIfNull(dispatch);
        await ConnectAsync(cancellationToken).ConfigureAwait(false);
        _stopping = new CancellationTokenSource();

        foreach (var topic in topics)
        {
            var queue = QueueOf(topic);
            try
            {
                var channel = await _connection!.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                await DeclareAsync(channel, queue, cancellationToken).ConfigureAwait(false);
                await channel.BasicQosAsync(0, _options.PrefetchCount, global: false, cancellationToken).ConfigureAwait(false);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += (_, received) => OnReceivedAsync(channel, topic, received, dispatch, _stopping.Token);
                var tag = await channel.BasicConsumeAsync(queue, autoAck: false, consumer, cancellationToken).ConfigureAwait(false);
                _consumers.Add((channel, tag));
            }
            catch (Exception exception) when (exception is RabbitMQClientException or IOException)
            {
                throw Unavailable($"Failed to subscribe to topic '{topic}'", exception);
            }
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_stopping is not null)
        {
            await _stopping.CancelAsync().ConfigureAwait(false);
        }

        foreach (var (channel, tag) in _consumers)
        {
            try
            {
                await channel.BasicCancelAsync(tag, noWait: false, cancellationToken).ConfigureAwait(false);
                await channel.CloseAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is RabbitMQClientException or IOException)
            {
                LogStopFailed(_logger, exception);
            }

            await channel.DisposeAsync().ConfigureAwait(false);
        }

        _consumers.Clear();
        _stopping?.Dispose();
        _stopping = null;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        if (_publishChannel is not null)
        {
            await _publishChannel.DisposeAsync().ConfigureAwait(false);
            _publishChannel = null;
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
        }

        _publishGate.Dispose();
    }

    /// <summary>
    /// 处理一条到达的消息：解码、等待宿主就绪、派发，失败重新发布到队尾或死信队列，最后确认原消息。
    /// </summary>
    private async Task OnReceivedAsync(IChannel channel, string topic, BasicDeliverEventArgs received, MessageDispatch dispatch, CancellationToken stopping)
    {
        // 尝试次数随消息头携带，重新发布时加一；缺失或非法按首次处理
        var attempt = received.BasicProperties.Headers?.TryGetValue(AttemptHeader, out var raw) == true && raw is int parsed && parsed >= 1 ? parsed : 1;
        var decoded = CloudEventsFormat.Decode(received.Body);
        // 无法解码的消息不重试：原样转入死信队列后确认，不让坏消息阻塞队列
        if (!decoded.TryGetValue(out var envelope))
        {
            LogUndecodable(_logger, topic, decoded.Error?.Message ?? "?");
            await PublishAsync(QueueOf(topic) + _messaging.DeadLetterSuffix, received.Body.ToArray(), attempt, stopping).ConfigureAwait(false);
            await channel.BasicAckAsync(received.DeliveryTag, multiple: false, stopping).ConfigureAwait(false);
            return;
        }

        // 宿主未进入服务状态前不投递，避免处理器在特性尚未就绪时执行
        while (!_app.State.IsServing())
        {
            await Task.Delay(10, stopping).ConfigureAwait(false);
        }

        var delivery = new MessageDelivery(topic, envelope, attempt);
        Result result;
        try
        {
            result = await dispatch(delivery, stopping).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            // 不确认：连接关闭后由 RabbitMQ 重新投递
            return;
        }
        catch (Exception exception)
        {
            result = Error.Unexpected(MessagingErrors.HandlerFailed, MessagingMessages.HandlerFailed, details: MessageArguments.Of(("reason", exception.Message)));
        }

        // 失败时先重新发布（队尾或死信），再确认原消息：顺序保证不丢消息，代价是极端情况下可能重复投递
        if (result.IsFailure)
        {
            if (attempt < _messaging.MaxAttempts)
            {
                await PublishAsync(QueueOf(topic), envelope, attempt + 1, stopping).ConfigureAwait(false);
            }
            else
            {
                LogDeadLettered(_logger, envelope.Id, topic, attempt, result.Error?.Code ?? "?");
                await PublishAsync(QueueOf(topic) + _messaging.DeadLetterSuffix, envelope, attempt, stopping).ConfigureAwait(false);
            }
        }

        await channel.BasicAckAsync(received.DeliveryTag, multiple: false, stopping).ConfigureAwait(false);
    }

    /// <summary>
    /// 把信封编码为 CloudEvents JSON 后发布到队列，事件 ID 作为消息 ID。
    /// </summary>
    private Task PublishAsync(string queue, MessageEnvelope envelope, int attempt, CancellationToken cancellationToken)
        => PublishAsync(queue, CloudEventsFormat.Encode(envelope), attempt, cancellationToken, envelope.Id);

    /// <summary>
    /// 发布原始字节到队列：持久化投递、CloudEvents 内容类型、尝试次数写入消息头；必要时先连接并声明队列。
    /// </summary>
    private async Task PublishAsync(string queue, byte[] body, int attempt, CancellationToken cancellationToken, string? messageId = null)
    {
        if (_publishChannel is null)
        {
            await ConnectAsync(cancellationToken).ConfigureAwait(false);
        }

        // 发布通道不是线程安全的，用信号量串行化；消费者的重新发布与应用发布共用这条通道
        await _publishGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await DeclareAsync(_publishChannel!, queue, cancellationToken).ConfigureAwait(false);
            var properties = new BasicProperties
            {
                ContentType = CloudEventsFormat.ContentType,
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = messageId,
                Headers = new Dictionary<string, object?>(StringComparer.Ordinal) { [AttemptHeader] = attempt },
            };
            await _publishChannel!.BasicPublishAsync(string.Empty, queue, mandatory: false, properties, body, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is RabbitMQClientException or IOException)
        {
            throw Unavailable($"Failed to publish to queue '{queue}'", exception);
        }
        finally
        {
            _publishGate.Release();
        }
    }

    /// <summary>
    /// 声明持久、非独占、不自动删除的队列；同一队列只声明一次。
    /// </summary>
    private async Task DeclareAsync(IChannel channel, string queue, CancellationToken cancellationToken)
    {
        // 队列声明是幂等的，但每次都声明会多一次往返；按名缓存已声明的队列
        lock (_declared)
        {
            if (_declared.Contains(queue))
            {
                return;
            }
        }

        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: cancellationToken).ConfigureAwait(false);
        lock (_declared)
        {
            _declared.Add(queue);
        }
    }

    /// <summary>
    /// 主题对应的队列名：前缀加主题。
    /// </summary>
    private string QueueOf(string topic) => _options.QueuePrefix + topic;

    /// <summary>
    /// 把客户端或网络异常包装成携带 <see cref="MessagingErrors.BrokerUnavailable"/> 的框架异常。
    /// </summary>
    private static FrameworkException Unavailable(string what, Exception exception)
        => new(MessagingErrors.BrokerUnavailable, $"{what}: {exception.Message}", exception);

    /// <summary>
    /// 记录一条消息重试到上限后进入死信队列。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Message {MessageId} on topic {Topic} was dead-lettered after attempt {Attempt} failed: {ErrorCode}")]
    private static partial void LogDeadLettered(ILogger logger, string messageId, string topic, int attempt, string errorCode);

    /// <summary>
    /// 记录一条无法解码的消息被转入死信队列。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Topic {Topic} received an undecodable message; it was moved to the dead-letter queue: {Reason}")]
    private static partial void LogUndecodable(ILogger logger, string topic, string reason);

    /// <summary>
    /// 记录停止消费者时的异常；停止流程继续。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "An error occurred while stopping the RabbitMQ consumer.")]
    private static partial void LogStopFailed(ILogger logger, Exception exception);
}
