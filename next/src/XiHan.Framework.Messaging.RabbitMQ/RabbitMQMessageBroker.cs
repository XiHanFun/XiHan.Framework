// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.CloudEvents;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.RabbitMQ;

/// <summary>
/// 基于 RabbitMQ.Client 的代理。
/// </summary>
/// <remarks>
/// 发布方只声明交换机：消息发到持久的 direct 交换机 <see cref="RabbitMQMessagingOptions.Exchange"/>，路由键是前缀加主题；队列只由订阅方声明并绑定（ADR-0079）。发布到没有队列绑定的路由（mandatory 退回，即没有订阅者）丢弃并记 Debug 日志，发布照常成功。竞争订阅每主题一个持久队列，广播订阅每个实例一个独占、随连接删除的队列（<c>&lt;QueuePrefix&gt;&lt;topic&gt;@&lt;InstanceId&gt;</c>），都以主题的路由键绑定；手动确认，失败经默认交换机重新发布到所消费队列的队尾，竞争订阅到上限进死信队列，广播订阅到上限丢弃。发布通道开启发布确认，每次发布（含重试与死信的重新发布）都等代理确认，代理拒绝（nack）按失败处理。批量发布在同一通道上连续发布、一次持锁，再一次等待全部确认。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class RabbitMQMessageBroker : IMessageBroker, IAsyncDisposable
{
    /// <summary>
    /// 携带尝试次数的消息头。
    /// </summary>
    /// <remarks>
    /// 重新发布时加一。
    /// </remarks>
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
    /// 本实例的标识。
    /// </summary>
    /// <remarks>
    /// 广播订阅的队列名里带它。
    /// </remarks>
    private readonly MessagingInstance _instance;

    /// <summary>
    /// 应用。
    /// </summary>
    /// <remarks>
    /// 只在其进入服务状态后才投递。
    /// </remarks>
    private readonly App _app;

    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 建立连接。
    /// </summary>
    /// <remarks>
    /// 缺省按 <see cref="CreateFactory"/> 建的 <see cref="ConnectionFactory"/> 连接；测试换成打桩的连接。
    /// </remarks>
    private readonly Func<CancellationToken, Task<IConnection>> _connect;

    /// <summary>
    /// 串行化发布通道访问的信号量。
    /// </summary>
    /// <remarks>
    /// 通道不是线程安全的。
    /// </remarks>
    private readonly SemaphoreSlim _publishGate = new(1, 1);

    /// <summary>
    /// 已声明并绑定过的死信队列名。
    /// </summary>
    /// <remarks>
    /// 避免每次死信都声明；死信发布被退回（队列被外部删除）时移除，下一次重新声明。
    /// </remarks>
    private readonly HashSet<string> _declared = new(StringComparer.Ordinal);

    /// <summary>
    /// 每个被消费订阅的通道与消费者标签。
    /// </summary>
    /// <remarks>
    /// 停止时逐个取消并关闭。
    /// </remarks>
    private readonly List<(IChannel Channel, string ConsumerTag)> _consumers = [];

    /// <summary>
    /// 连接。
    /// </summary>
    /// <remarks>
    /// <see cref="ConnectAsync"/> 建立，释放时关闭。
    /// </remarks>
    private IConnection? _connection;

    /// <summary>
    /// 发布专用通道。
    /// </summary>
    /// <remarks>
    /// 开启发布确认并由客户端跟踪。
    /// </remarks>
    private IChannel? _publishChannel;

    /// <summary>
    /// 停止信号。
    /// </summary>
    /// <remarks>
    /// 取消后进行中的投递不再确认。
    /// </remarks>
    private CancellationTokenSource? _stopping;

    /// <summary>
    /// 创建代理。
    /// </summary>
    /// <remarks>
    /// 连接在 <see cref="ConnectAsync"/> 时建立。
    /// </remarks>
    public RabbitMQMessageBroker(RabbitMQMessagingOptions options, MessagingOptions messaging, MessagingInstance instance, App app, ILoggerFactory loggerFactory)
        : this(options, messaging, instance, app, loggerFactory, null)
    {
    }

    /// <summary>
    /// 创建代理，连接由给定的委托建立。
    /// </summary>
    /// <remarks>
    /// 委托为 <see langword="null"/> 时按选项连接。
    /// </remarks>
    internal RabbitMQMessageBroker(RabbitMQMessagingOptions options, MessagingOptions messaging, MessagingInstance instance, App app, ILoggerFactory loggerFactory, Func<CancellationToken, Task<IConnection>>? connect)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _messaging = messaging ?? throw new ArgumentNullException(nameof(messaging));
        _instance = instance ?? throw new ArgumentNullException(nameof(instance));
        _app = app ?? throw new ArgumentNullException(nameof(app));
        _logger = loggerFactory.CreateLogger<RabbitMQMessageBroker>();
        _connect = connect ?? (cancellationToken => CreateFactory(_options).CreateConnectionAsync(cancellationToken));
    }

    /// <summary>
    /// 按选项建连接工厂。
    /// </summary>
    /// <remarks>
    /// AMQP URI 给出地址、虚拟主机与缺省凭据；选项里的用户名与密码（来自绑定的属性，RFC-0016）覆盖 URI 里的。
    /// </remarks>
    internal static ConnectionFactory CreateFactory(RabbitMQMessagingOptions options)
    {
        // 先设 Uri（它按用户信息设用户名与密码），再以绑定给出的凭据覆盖：不改写 URI，密码里的特殊字符不必转义
        var factory = new ConnectionFactory { Uri = new Uri(options.Configuration), ConsumerDispatchConcurrency = 1 };
        if (options.UserName is { } userName)
        {
            factory.UserName = userName;
        }

        if (options.Password is { } password)
        {
            factory.Password = password;
        }

        return factory;
    }

    /// <summary>
    /// 是否已连接。
    /// </summary>
    public bool IsConnected => _connection?.IsOpen == true;

    /// <summary>
    /// 连通性检查（健康项）。
    /// </summary>
    /// <remarks>
    /// 连接与发布通道都打开为成功；否则以代理给出的关闭原因（还没建立连接时说明如此）构造 <see cref="MessagingErrors.BrokerUnavailable"/>。
    /// </remarks>
    public Result CheckConnection()
    {
        if (_connection is { IsOpen: true } && _publishChannel is { IsOpen: true })
        {
            return Result.Success();
        }

        var reason = _connection?.CloseReason?.ReplyText ?? _publishChannel?.CloseReason?.ReplyText ?? "The connection has not been established.";
        return Error.Unavailable(MessagingErrors.BrokerUnavailable, MessagingMessages.BrokerUnavailable, MessageArguments.Of(("reason", reason)));
    }

    /// <summary>
    /// 建立连接与发布通道。
    /// </summary>
    /// <remarks>
    /// 发布通道开启发布确认并由客户端跟踪：每次发布在代理确认后才完成，拒绝或退回以 <see cref="PublishException"/> 抛出。随后声明交换机。
    /// </remarks>
    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
        {
            return;
        }

        try
        {
            _connection = await _connect(cancellationToken).ConfigureAwait(false);
            // 不开确认时 BasicPublishAsync 写出即返回，代理没收下（磁盘告警、内部错误）或无法路由的消息也被当作已发布；
            // 开启并跟踪确认后，每条发布等到 basic.ack 才完成，basic.nack 与 mandatory 的 basic.return 都抛 PublishException
            _publishChannel = await _connection.CreateChannelAsync(new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), cancellationToken).ConfigureAwait(false);
            // 发布方只声明交换机、不声明队列：没有订阅者的主题不会因为发布而多出一个没人消费、越堆越多的持久队列（ADR-0079）
            await _publishChannel.ExchangeDeclareAsync(_options.Exchange, ExchangeType.Direct, durable: true, autoDelete: false, arguments: null, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is RabbitMQClientException or IOException)
        {
            throw Unavailable("Failed to connect to RabbitMQ", exception);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 等到代理确认才返回成功；没有订阅者（退回）时丢弃并成功；代理拒绝返回 <see cref="MessagingErrors.BrokerUnavailable"/>，连接或通道异常抛出携带它的 <see cref="FrameworkException"/>。
    /// </remarks>
    public async ValueTask<Result> PublishAsync(string topic, MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        TopicName.Validate(topic);
        return (await PublishManyAsync(topic, [envelope], cancellationToken).ConfigureAwait(false))[0];
    }

    /// <inheritdoc />
    /// <remarks>
    /// 连续发布到交换机后一次等待全部确认，逐项结果：确认为成功，没有订阅者（退回）丢弃并成功，代理拒绝为 <see cref="MessagingErrors.BrokerUnavailable"/>；连接或通道异常让整批抛出。
    /// </remarks>
    public async ValueTask<IReadOnlyList<Result>> PublishManyAsync(string topic, IReadOnlyList<MessageEnvelope> envelopes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelopes);
        TopicName.Validate(topic);
        if (envelopes.Count == 0)
        {
            return [];
        }

        // 先把全部信封编码好：任一信封为空在触及代理前就失败
        var bodies = new (byte[] Body, string Id)[envelopes.Count];
        for (var index = 0; index < envelopes.Count; index++)
        {
            ArgumentNullException.ThrowIfNull(envelopes[index]);
            bodies[index] = (CloudEventsFormat.Encode(envelopes[index]), envelopes[index].Id);
        }

        var routingKey = QueueOf(topic);
        if (_publishChannel is null)
        {
            await ConnectAsync(cancellationToken).ConfigureAwait(false);
        }

        var results = new Result[bodies.Length];
        var dropped = 0;
        await _publishGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // 同一通道上连续发布：一次持锁，每条写出后立即写下一条，代理按到达顺序路由；
            // 全部写出后再逐条等确认（一次等待），每条的确认各自成为该项的结果
            var pending = new Task[bodies.Length];
            var started = 0;
            Exception? failure = null;
            try
            {
                for (; started < bodies.Length; started++)
                {
                    pending[started] = _publishChannel!.BasicPublishAsync(_options.Exchange, routingKey, mandatory: true, Properties(attempt: 1, bodies[started].Id), bodies[started].Body, cancellationToken).AsTask();
                }
            }
            catch (Exception exception) when (exception is RabbitMQClientException or IOException or OperationCanceledException)
            {
                failure = exception;
            }

            // 已发出的每一项都要等到；连接、通道异常或取消记下第一个，等完已发出的再让整批抛出，不伪装成部分成功
            for (var index = 0; index < started; index++)
            {
                try
                {
                    await pending[index].ConfigureAwait(false);
                    results[index] = Result.Success();
                }
                catch (PublishException exception) when (exception.IsReturn)
                {
                    // 退回：路由键上没有任何队列绑定，即没有订阅者。与内存代理一致丢弃、照常成功，
                    // 不当发布失败——否则发件箱会为没人订阅的框架事件反复重试，最后记进死信
                    results[index] = Result.Success();
                    dropped++;
                }
                catch (PublishException exception)
                {
                    results[index] = NotConfirmed(routingKey, bodies[index].Id, exception);
                }
                catch (Exception exception) when (exception is RabbitMQClientException or IOException or OperationCanceledException)
                {
                    failure ??= exception;
                }
            }

            if (failure is OperationCanceledException)
            {
                ExceptionDispatchInfo.Throw(failure);
            }

            if (failure is not null)
            {
                throw Unavailable($"Failed to publish to exchange '{_options.Exchange}' with routing key '{routingKey}'", failure);
            }
        }
        finally
        {
            _publishGate.Release();
        }

        if (dropped > 0)
        {
            LogDropped(_logger, dropped, topic);
        }

        return results;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 竞争订阅声明持久队列 <c>&lt;QueuePrefix&gt;&lt;topic&gt;</c>，广播订阅声明本实例独占、非持久、随连接删除的队列 <c>&lt;QueuePrefix&gt;&lt;topic&gt;@&lt;InstanceId&gt;</c>，都以主题的路由键绑定到交换机；每个订阅一个通道与一个消费者。
    /// </remarks>
    public async Task StartAsync(IReadOnlyCollection<MessageSubscription> subscriptions, MessageDispatch dispatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subscriptions);
        ArgumentNullException.ThrowIfNull(dispatch);
        await ConnectAsync(cancellationToken).ConfigureAwait(false);
        _stopping = new CancellationTokenSource();

        foreach (var subscription in subscriptions)
        {
            var queue = QueueOf(subscription);
            try
            {
                var channel = await _connection!.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                if (subscription.Mode == SubscriptionMode.Broadcast)
                {
                    // 广播：每个实例一个自己的队列，交换机按路由键给每个绑定的队列各一份。独占且非持久：
                    // 随本连接删除（自动恢复连接时客户端重新声明），实例下线后不留下没人消费的队列；断开期间的消息不进它，尽力而为
                    await channel.QueueDeclareAsync(queue, durable: false, exclusive: true, autoDelete: true, arguments: null, cancellationToken: cancellationToken).ConfigureAwait(false);
                    await channel.QueueBindAsync(queue, _options.Exchange, QueueOf(subscription.Topic), arguments: null, cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    // 队列只由订阅方声明并绑定：持久、非独占，订阅方离线期间的消息留在队列里
                    await DeclareDurableAsync(channel, queue, cancellationToken).ConfigureAwait(false);
                }

                await channel.BasicQosAsync(0, _options.PrefetchCount, global: false, cancellationToken).ConfigureAwait(false);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += (_, received) => OnReceivedAsync(channel, subscription, queue, received, dispatch, _stopping.Token);
                var tag = await channel.BasicConsumeAsync(queue, autoAck: false, consumer, cancellationToken).ConfigureAwait(false);
                _consumers.Add((channel, tag));
            }
            catch (Exception exception) when (exception is RabbitMQClientException or IOException)
            {
                throw Unavailable($"Failed to subscribe to topic '{subscription.Topic}' ({subscription.Mode})", exception);
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
    /// 处理一条到达的消息。
    /// </summary>
    /// <remarks>
    /// 解码、等待宿主就绪、派发，失败重新发布到所消费队列的队尾，到了上限竞争订阅进死信队列、广播订阅丢弃；重新发布得到确认后才确认原消息。
    /// </remarks>
    private async Task OnReceivedAsync(IChannel channel, MessageSubscription subscription, string queue, BasicDeliverEventArgs received, MessageDispatch dispatch, CancellationToken stopping)
    {
        var topic = subscription.Topic;
        var broadcast = subscription.Mode == SubscriptionMode.Broadcast;
        // 尝试次数随消息头携带，重新发布时加一；缺失或非法按首次处理
        var attempt = received.BasicProperties.Headers?.TryGetValue(AttemptHeader, out var raw) == true && raw is int parsed && parsed >= 1 ? parsed : 1;
        var decoded = CloudEventsFormat.Decode(received.Body);
        // 无法解码的消息不重试，不让坏消息阻塞队列：竞争订阅原样转入死信队列，确认后再确认原消息；
        // 广播订阅没有死信，记日志后确认掉
        if (!decoded.TryGetValue(out var envelope))
        {
            if (broadcast)
            {
                LogBroadcastUndecodable(_logger, topic, decoded.Error?.Message ?? "?");
                await SettleAsync(channel, received.DeliveryTag, topic, "?", republish: null, stopping).ConfigureAwait(false);
                return;
            }

            LogUndecodable(_logger, topic, decoded.Error?.Message ?? "?");
            var body = received.Body.ToArray();
            await SettleAsync(channel, received.DeliveryTag, topic, "?", () => DeadLetterAsync(queue, body, attempt, null, stopping), stopping).ConfigureAwait(false);
            return;
        }

        // 宿主未进入服务状态前不投递，避免处理器在特性尚未就绪时执行
        while (!_app.State.IsServing())
        {
            await Task.Delay(10, stopping).ConfigureAwait(false);
        }

        var delivery = new MessageDelivery(topic, envelope, attempt, subscription.Mode);
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

        if (result.IsSuccess)
        {
            await SettleAsync(channel, received.DeliveryTag, topic, envelope.Id, republish: null, stopping).ConfigureAwait(false);
            return;
        }

        // 失败时先重新发布（所消费队列的队尾或死信）并等确认，再确认原消息：顺序保证不丢消息，代价是极端情况下可能重复投递；
        // 重试只回到所消费的队列，同一路由键上的其他订阅（别的实例的广播队列）不再收一遍
        if (attempt < _messaging.MaxAttempts)
        {
            await SettleAsync(channel, received.DeliveryTag, topic, envelope.Id, () => PublishToQueueAsync(queue, CloudEventsFormat.Encode(envelope), attempt + 1, envelope.Id, stopping), stopping).ConfigureAwait(false);
        }
        else if (broadcast)
        {
            // 广播是尽力而为、没有死信：每个实例各自的一份失败没有可以重放的共享位置
            LogBroadcastDropped(_logger, envelope.Id, topic, attempt, result.Error?.Code ?? "?");
            await SettleAsync(channel, received.DeliveryTag, topic, envelope.Id, republish: null, stopping).ConfigureAwait(false);
        }
        else
        {
            LogDeadLettered(_logger, envelope.Id, topic, attempt, result.Error?.Code ?? "?");
            await SettleAsync(channel, received.DeliveryTag, topic, envelope.Id, () => DeadLetterAsync(queue, CloudEventsFormat.Encode(envelope), attempt, envelope.Id, stopping), stopping).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 了结一条投递：需要重新发布时先发布并等确认，确认了才确认原消息。
    /// </summary>
    /// <remarks>
    /// 重新发布未被确认或失败时把原消息退回队列（nack 并重新入队），由 RabbitMQ 再投递，不丢消息；停止时既不确认也不退回，连接关闭后由 RabbitMQ 重新投递。
    /// </remarks>
    private async Task SettleAsync(IChannel channel, ulong deliveryTag, string topic, string messageId, Func<ValueTask<Result>>? republish, CancellationToken stopping)
    {
        if (republish is not null)
        {
            Result republished;
            try
            {
                republished = await republish().ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested)
            {
                return;
            }
            catch (FrameworkException exception) when (exception.Code == MessagingErrors.BrokerUnavailable)
            {
                republished = Error.Unavailable(MessagingErrors.BrokerUnavailable, MessagingMessages.BrokerUnavailable, MessageArguments.Of(("reason", exception.Message)));
            }

            if (republished.IsFailure)
            {
                LogRepublishFailed(_logger, messageId, topic, republished.Error?.Code ?? "?");
                try
                {
                    await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: true, stopping).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is RabbitMQClientException or IOException or OperationCanceledException)
                {
                    // 通道已断或正在停止：未确认的消息在通道关闭后由 RabbitMQ 重新投递
                    LogNackFailed(_logger, messageId, topic, exception);
                }

                return;
            }
        }

        await channel.BasicAckAsync(deliveryTag, multiple: false, stopping).ConfigureAwait(false);
    }

    /// <summary>
    /// 把一条投递转入死信队列。
    /// </summary>
    /// <remarks>
    /// 死信队列由死信的一方（订阅方）声明为持久队列并绑定到交换机，再经默认交换机定向发布。
    /// </remarks>
    private async ValueTask<Result> DeadLetterAsync(string queue, byte[] body, int attempt, string? messageId, CancellationToken cancellationToken)
    {
        var deadLetter = queue + _messaging.DeadLetterSuffix;
        await EnsureDeadLetterQueueAsync(deadLetter, cancellationToken).ConfigureAwait(false);
        return await PublishToQueueAsync(deadLetter, body, attempt, messageId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 经默认交换机把原始字节定向发布到一个队列并等代理确认。
    /// </summary>
    /// <remarks>
    /// 用于重试与死信：目标是已知存在的队列，不经主题的交换机——经交换机会把重试也送给同一路由键上的其他队列。持久化投递、CloudEvents 内容类型、尝试次数写入消息头、mandatory；代理拒绝或退回（队列已不存在）返回失败，连接或通道异常抛出。
    /// </remarks>
    private async ValueTask<Result> PublishToQueueAsync(string queue, byte[] body, int attempt, string? messageId, CancellationToken cancellationToken)
    {
        if (_publishChannel is null)
        {
            await ConnectAsync(cancellationToken).ConfigureAwait(false);
        }

        // 发布通道不是线程安全的，用信号量串行化；消费者的重新发布与应用发布共用这条通道
        await _publishGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _publishChannel!.BasicPublishAsync(string.Empty, queue, mandatory: true, Properties(attempt, messageId), body, cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (PublishException exception)
        {
            return NotConfirmed(queue, messageId ?? "?", exception);
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
    /// 代理没有确认一条发布：拒绝（nack），或定向发布被退回。
    /// </summary>
    /// <remarks>
    /// 定向发布（重试、死信）被退回说明目标队列已不存在（被外部删除），清掉死信队列的声明缓存让下一次重新声明。发到交换机的退回不经这里，见 <see cref="PublishManyAsync"/>。
    /// </remarks>
    private Error NotConfirmed(string queue, string messageId, PublishException exception)
    {
        if (exception.IsReturn)
        {
            lock (_declared)
            {
                _declared.Remove(queue);
            }
        }

        var reason = exception.IsReturn
            ? $"message '{messageId}' could not be routed to queue '{queue}'"
            : $"the broker did not confirm message '{messageId}' routed to '{queue}'";
        LogNotConfirmed(_logger, messageId, queue, exception.IsReturn);
        return Error.Unavailable(MessagingErrors.BrokerUnavailable, MessagingMessages.BrokerUnavailable, MessageArguments.Of(("reason", reason)));
    }

    /// <summary>
    /// 消息属性：持久化投递、CloudEvents 内容类型、消息 ID 与尝试次数头。
    /// </summary>
    private static BasicProperties Properties(int attempt, string? messageId) => new()
    {
        ContentType = CloudEventsFormat.ContentType,
        DeliveryMode = DeliveryModes.Persistent,
        MessageId = messageId,
        Headers = new Dictionary<string, object?>(StringComparer.Ordinal) { [AttemptHeader] = attempt },
    };

    /// <summary>
    /// 声明死信队列并绑定到交换机。
    /// </summary>
    /// <remarks>
    /// 在发布通道上声明，同一队列只声明一次。调用方不持有发布锁。
    /// </remarks>
    private async Task EnsureDeadLetterQueueAsync(string queue, CancellationToken cancellationToken)
    {
        // 队列声明是幂等的，但每次死信都声明会多两次往返；按名缓存已声明的死信队列
        lock (_declared)
        {
            if (_declared.Contains(queue))
            {
                return;
            }
        }

        if (_publishChannel is null)
        {
            await ConnectAsync(cancellationToken).ConfigureAwait(false);
        }

        // 声明与绑定同样走发布通道，与发布共用一把锁
        await _publishGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await DeclareDurableAsync(_publishChannel!, queue, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is RabbitMQClientException or IOException)
        {
            throw Unavailable($"Failed to declare dead-letter queue '{queue}'", exception);
        }
        finally
        {
            _publishGate.Release();
        }

        lock (_declared)
        {
            _declared.Add(queue);
        }
    }

    /// <summary>
    /// 声明持久、非独占、不自动删除的队列，并以同名路由键绑定到交换机。
    /// </summary>
    /// <remarks>
    /// 声明与绑定都是幂等的：订阅方每次启动都执行，先前版本建出的同名队列照常可用。
    /// </remarks>
    private async Task DeclareDurableAsync(IChannel channel, string queue, CancellationToken cancellationToken)
    {
        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: cancellationToken).ConfigureAwait(false);
        await channel.QueueBindAsync(queue, _options.Exchange, queue, arguments: null, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 主题对应的队列名与路由键：前缀加主题。
    /// </summary>
    /// <remarks>
    /// 也是主题的竞争订阅的队列名。
    /// </remarks>
    private string QueueOf(string topic) => _options.QueuePrefix + topic;

    /// <summary>
    /// 订阅消费的队列名。
    /// </summary>
    /// <remarks>
    /// 竞争订阅是前缀加主题；广播订阅再加 <c>@</c> 与实例标识，<c>@</c> 不在主题文法里，不会与竞争队列或死信队列重名。
    /// </remarks>
    private string QueueOf(MessageSubscription subscription)
        => subscription.Mode == SubscriptionMode.Broadcast ? QueueOf(subscription.Topic) + "@" + _instance.Id : QueueOf(subscription.Topic);

    /// <summary>
    /// 把客户端或网络异常包装成携带 <see cref="MessagingErrors.BrokerUnavailable"/> 的框架异常。
    /// </summary>
    private static FrameworkException Unavailable(string what, Exception exception)
        => new(MessagingErrors.BrokerUnavailable, $"{what}: {exception.Message}", exception);

    /// <summary>
    /// 记录发布到没有订阅者的主题的消息被丢弃。
    /// </summary>
    /// <remarks>
    /// Debug 级别：调度、工作流等框架事件照常发布到应用不一定订阅的主题，这是常态；主题名写错或订阅方还没启动过时打开本类别的 Debug 日志排查。
    /// </remarks>
    [LoggerMessage(Level = LogLevel.Debug, Message = "Dropped {Count} message(s) published to topic '{Topic}' because no queue is bound to its routing key (nothing subscribes to it).")]
    private static partial void LogDropped(ILogger logger, int count, string topic);

    /// <summary>
    /// 记录一条消息重试到上限后进入死信队列。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Message {MessageId} on topic {Topic} was dead-lettered after attempt {Attempt} failed: {ErrorCode}")]
    private static partial void LogDeadLettered(ILogger logger, string messageId, string topic, int attempt, string errorCode);

    /// <summary>
    /// 记录一条广播投递重试到上限后被丢弃。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Broadcast message {MessageId} on topic {Topic} was dropped after attempt {Attempt} failed: {ErrorCode}")]
    private static partial void LogBroadcastDropped(ILogger logger, string messageId, string topic, int attempt, string errorCode);

    /// <summary>
    /// 记录一条无法解码的广播消息被丢弃。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Topic {Topic} delivered an undecodable broadcast message; it was dropped: {Reason}")]
    private static partial void LogBroadcastUndecodable(ILogger logger, string topic, string reason);

    /// <summary>
    /// 记录一条无法解码的消息被转入死信队列。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Topic {Topic} received an undecodable message; it was moved to the dead-letter queue: {Reason}")]
    private static partial void LogUndecodable(ILogger logger, string topic, string reason);

    /// <summary>
    /// 记录代理没有确认一条发布。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "RabbitMQ did not confirm message {MessageId} routed to {Queue} (returned as unroutable: {Returned})")]
    private static partial void LogNotConfirmed(ILogger logger, string messageId, string queue, bool returned);

    /// <summary>
    /// 记录重试或死信的重新发布失败，原消息退回队列。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Republishing message {MessageId} on topic {Topic} failed ({ErrorCode}); the original delivery was returned to the queue")]
    private static partial void LogRepublishFailed(ILogger logger, string messageId, string topic, string errorCode);

    /// <summary>
    /// 记录退回原消息失败。
    /// </summary>
    /// <remarks>
    /// 通道关闭后 RabbitMQ 会重新投递。
    /// </remarks>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Returning message {MessageId} on topic {Topic} to the queue failed; RabbitMQ redelivers it once the channel closes")]
    private static partial void LogNackFailed(ILogger logger, string messageId, string topic, Exception exception);

    /// <summary>
    /// 记录停止消费者时的异常。
    /// </summary>
    /// <remarks>
    /// 停止流程继续。
    /// </remarks>
    [LoggerMessage(Level = LogLevel.Warning, Message = "An error occurred while stopping the RabbitMQ consumer.")]
    private static partial void LogStopFailed(ILogger logger, Exception exception);
}
