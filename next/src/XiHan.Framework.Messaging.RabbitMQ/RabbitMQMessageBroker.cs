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
    private const string AttemptHeader = "x-xihan-attempt";

    private readonly RabbitMQMessagingOptions _options;
    private readonly MessagingOptions _messaging;
    private readonly App _app;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _publishGate = new(1, 1);
    private readonly HashSet<string> _declared = new(StringComparer.Ordinal);
    private readonly List<(IChannel Channel, string ConsumerTag)> _consumers = [];
    private IConnection? _connection;
    private IChannel? _publishChannel;
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
            throw Unavailable("连接 RabbitMQ 失败", exception);
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
                throw Unavailable($"订阅主题 '{topic}' 失败", exception);
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

    private async Task OnReceivedAsync(IChannel channel, string topic, BasicDeliverEventArgs received, MessageDispatch dispatch, CancellationToken stopping)
    {
        var attempt = received.BasicProperties.Headers?.TryGetValue(AttemptHeader, out var raw) == true && raw is int parsed && parsed >= 1 ? parsed : 1;
        var decoded = CloudEventsFormat.Decode(received.Body);
        if (!decoded.TryGetValue(out var envelope))
        {
            LogUndecodable(_logger, topic, decoded.Error?.Message ?? "?");
            await PublishAsync(QueueOf(topic) + _messaging.DeadLetterSuffix, received.Body.ToArray(), attempt, stopping).ConfigureAwait(false);
            await channel.BasicAckAsync(received.DeliveryTag, multiple: false, stopping).ConfigureAwait(false);
            return;
        }

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
            result = Error.Unexpected("MESSAGING_UNEXPECTED_001", exception.Message);
        }

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

    private Task PublishAsync(string queue, MessageEnvelope envelope, int attempt, CancellationToken cancellationToken)
        => PublishAsync(queue, CloudEventsFormat.Encode(envelope), attempt, cancellationToken, envelope.Id);

    private async Task PublishAsync(string queue, byte[] body, int attempt, CancellationToken cancellationToken, string? messageId = null)
    {
        if (_publishChannel is null)
        {
            await ConnectAsync(cancellationToken).ConfigureAwait(false);
        }

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
            throw Unavailable($"发布到队列 '{queue}' 失败", exception);
        }
        finally
        {
            _publishGate.Release();
        }
    }

    private async Task DeclareAsync(IChannel channel, string queue, CancellationToken cancellationToken)
    {
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

    private string QueueOf(string topic) => _options.QueuePrefix + topic;

    private static FrameworkException Unavailable(string what, Exception exception)
        => new(MessagingErrors.BrokerUnavailable, $"{what}：{exception.Message}", exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "消息 {MessageId}（主题 {Topic}）第 {Attempt} 次投递失败后进入死信：{ErrorCode}")]
    private static partial void LogDeadLettered(ILogger logger, string messageId, string topic, int attempt, string errorCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "主题 {Topic} 收到无法解码的消息，已转入死信：{Reason}")]
    private static partial void LogUndecodable(ILogger logger, string topic, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "停止 RabbitMQ 消费者时出错。")]
    private static partial void LogStopFailed(ILogger logger, Exception exception);
}
