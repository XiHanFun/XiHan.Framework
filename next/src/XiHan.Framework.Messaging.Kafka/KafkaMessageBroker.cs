// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.CloudEvents;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Primitives;
using Error = XiHan.Framework.Primitives.Error;

namespace XiHan.Framework.Messaging.Kafka;

/// <summary>
/// Kafka 消息代理：主题映射为 <c>&lt;TopicPrefix&gt;&lt;topic&gt;</c>，信封按 CloudEvents 结构化 JSON 编码为消息值，分区键取扩展属性 <c>xihankey</c>。
/// 生产者幂等且 <c>acks=all</c>；每个订阅主题一个消费者（同一消费组），单线程按分区顺序派发，失败在原位重试到 <see cref="MessagingOptions.MaxAttempts"/> 后发布到死信主题，投递完成后提交位点。
/// 重放用独立消费组从指定时刻的位点读到调用时的高水位，再投递一遍。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class KafkaMessageBroker : IMessageBroker, IMessageReplayer, IAsyncDisposable
{
    /// <summary>
    /// 尝试次数的消息头（重新发布到死信时携带最后一次的次数）。
    /// </summary>
    private const string AttemptHeader = "x-xihan-attempt";

    /// <summary>
    /// 内容类型的消息头。
    /// </summary>
    private const string ContentTypeHeader = "content-type";

    /// <summary>
    /// 元数据、位点查询等同步调用的超时。
    /// </summary>
    private static readonly TimeSpan _operationTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 选项。
    /// </summary>
    private readonly KafkaMessagingOptions _options;

    /// <summary>
    /// 重试次数与死信后缀。
    /// </summary>
    private readonly MessagingOptions _messaging;

    /// <summary>
    /// 应用：进入服务状态前不派发。
    /// </summary>
    private readonly App _app;

    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 每个订阅主题的消费者与它的消费循环。
    /// </summary>
    private readonly List<(IConsumer<string?, byte[]> Consumer, Task Loop)> _consumers = [];

    /// <summary>
    /// 生产者；<see cref="ConnectAsync"/> 创建。
    /// </summary>
    private IProducer<string?, byte[]>? _producer;

    /// <summary>
    /// 停止信号。
    /// </summary>
    private CancellationTokenSource? _stopping;

    /// <summary>
    /// 运行时交来的派发委托；重放也经它投递。
    /// </summary>
    private MessageDispatch? _dispatch;

    /// <summary>
    /// 创建。
    /// </summary>
    public KafkaMessageBroker(KafkaMessagingOptions options, MessagingOptions messaging, App app, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _messaging = messaging ?? throw new ArgumentNullException(nameof(messaging));
        _app = app ?? throw new ArgumentNullException(nameof(app));
        _logger = loggerFactory.CreateLogger<KafkaMessageBroker>();
    }

    /// <summary>
    /// 是否已建立生产者并确认过集群可达。
    /// </summary>
    public bool IsConnected => _producer is not null;

    /// <summary>
    /// 创建生产者并向集群取一次元数据确认可达；连不上视为失败，不降级运行。可重复调用。
    /// </summary>
    public Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (_producer is not null)
        {
            return Task.CompletedTask;
        }

        try
        {
            using (var admin = new AdminClientBuilder(ClientConfig()).Build())
            {
                _ = admin.GetMetadata(_operationTimeout);
            }

            cancellationToken.ThrowIfCancellationRequested();
            _producer = new ProducerBuilder<string?, byte[]>(ProducerConfig()).Build();
        }
        catch (KafkaException exception)
        {
            throw Unavailable("Failed to connect to Kafka", exception);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask<Result> PublishAsync(string topic, MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        TopicName.Validate(topic);
        await ProduceAsync(TopicOf(topic), ToMessage(envelope, CloudEventsFormat.Encode(envelope), attempt: 1), cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<Result>> PublishManyAsync(string topic, IReadOnlyList<MessageEnvelope> envelopes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelopes);
        TopicName.Validate(topic);
        if (envelopes.Count == 0)
        {
            return [];
        }

        // 先把全部信封编码好：任一信封为空在触及代理前就失败
        var messages = new Message<string?, byte[]>[envelopes.Count];
        for (var index = 0; index < envelopes.Count; index++)
        {
            ArgumentNullException.ThrowIfNull(envelopes[index]);
            messages[index] = ToMessage(envelopes[index], CloudEventsFormat.Encode(envelopes[index]), attempt: 1);
        }

        await ConnectAsync(cancellationToken).ConfigureAwait(false);
        var kafkaTopic = TopicOf(topic);
        var reports = new TaskCompletionSource<DeliveryReport<string?, byte[]>>[messages.Length];
        try
        {
            // 客户端的原生批量：连续交给本地队列，librdkafka 合并成批次发出，再一次等待全部投递报告；
            // 幂等生产者保证主题内顺序与不重复，任一失败整批抛出
            for (var index = 0; index < messages.Length; index++)
            {
                var completion = new TaskCompletionSource<DeliveryReport<string?, byte[]>>(TaskCreationOptions.RunContinuationsAsynchronously);
                reports[index] = completion;
                _producer!.Produce(kafkaTopic, messages[index], report => completion.TrySetResult(report));
            }

            _producer!.Flush(cancellationToken);
            foreach (var report in reports)
            {
                var delivered = await report.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                if (delivered.Error.IsError)
                {
                    throw new FrameworkException(MessagingErrors.BrokerUnavailable, $"Failed to publish to topic '{kafkaTopic}': {delivered.Error.Reason}");
                }
            }
        }
        catch (KafkaException exception)
        {
            throw Unavailable($"Failed to publish to topic '{kafkaTopic}'", exception);
        }

        var results = new Result[envelopes.Count];
        Array.Fill(results, Result.Success());
        return results;
    }

    /// <inheritdoc />
    public async Task StartAsync(IReadOnlyCollection<string> topics, MessageDispatch dispatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(topics);
        ArgumentNullException.ThrowIfNull(dispatch);
        await ConnectAsync(cancellationToken).ConfigureAwait(false);
        _dispatch = dispatch;
        _stopping = new CancellationTokenSource();
        foreach (var topic in topics)
        {
            try
            {
                var consumer = new ConsumerBuilder<string?, byte[]>(ConsumerConfig(_options.GroupId)).Build();
                consumer.Subscribe(TopicOf(topic));
                // 每个主题一条专用线程：Consume 是阻塞调用，不占线程池
                var stopping = _stopping.Token;
                var loop = Task.Factory.StartNew(() => ConsumeLoopAsync(consumer, topic, dispatch, stopping), stopping, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
                _consumers.Add((consumer, loop));
            }
            catch (KafkaException exception)
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

        foreach (var (consumer, loop) in _consumers)
        {
            try
            {
                await loop.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                LogStopFailed(_logger, exception);
            }

            try
            {
                // 离开消费组；已处理的位点都在投递完成时提交过了
                consumer.Close();
            }
            catch (KafkaException exception)
            {
                LogStopFailed(_logger, exception);
            }

            consumer.Dispose();
        }

        _consumers.Clear();
        _stopping?.Dispose();
        _stopping = null;
    }

    /// <inheritdoc />
    public async ValueTask<Result> ReplayAsync(string topic, DateTimeOffset since, CancellationToken cancellationToken)
    {
        TopicName.Validate(topic);
        if (_dispatch is null)
        {
            return Error.Unavailable(MessagingErrors.BrokerUnavailable, MessagingMessages.BrokerUnavailable, MessageArguments.Of(("reason", "the broker has not been started")));
        }

        var kafkaTopic = TopicOf(topic);
        try
        {
            List<TopicPartition> partitions;
            using (var admin = new AdminClientBuilder(ClientConfig()).Build())
            {
                var metadata = admin.GetMetadata(kafkaTopic, _operationTimeout);
                var described = metadata.Topics.FirstOrDefault(t => t.Topic == kafkaTopic);
                if (described is null || described.Error.IsError || described.Partitions.Count == 0)
                {
                    // 主题不存在就没有可重放的消息
                    return Result.Success();
                }

                partitions = [.. described.Partitions.Select(p => new TopicPartition(kafkaTopic, p.PartitionId))];
            }

            // 独立消费组：不提交位点，不影响正常消费
            using var consumer = new ConsumerBuilder<string?, byte[]>(ConsumerConfig($"{_options.GroupId}.replay.{Guid.NewGuid():N}")).Build();
            var ends = partitions.ToDictionary(static p => p, p => consumer.QueryWatermarkOffsets(p, _operationTimeout).High);
            var starts = consumer.OffsetsForTimes(partitions.Select(p => new TopicPartitionTimestamp(p, new Timestamp(since.UtcDateTime))), _operationTimeout);
            // 该时刻之后没有消息的分区（Offset.End）与高水位之前没有消息的分区都不用读
            var pending = starts
                .Where(start => start.Offset != Offset.End && start.Offset.Value < ends[start.TopicPartition].Value)
                .ToDictionary(static start => start.TopicPartition, start => ends[start.TopicPartition].Value);
            if (pending.Count == 0)
            {
                return Result.Success();
            }

            consumer.Assign(starts.Where(start => pending.ContainsKey(start.TopicPartition)));
            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var consumed = consumer.Consume(_operationTimeout);
                if (consumed?.Message is null)
                {
                    continue;
                }

                await DeliverAsync(topic, consumed, _dispatch, cancellationToken).ConfigureAwait(false);
                // 读到调用时的高水位就不再读该分区：重放期间新到的消息由正常消费投递
                if (consumed.Offset.Value + 1 >= pending[consumed.TopicPartition])
                {
                    pending.Remove(consumed.TopicPartition);
                }
            }

            consumer.Close();
            return Result.Success();
        }
        catch (KafkaException exception)
        {
            return Error.Unavailable(MessagingErrors.BrokerUnavailable, MessagingMessages.BrokerUnavailable, MessageArguments.Of(("reason", exception.Message)));
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        if (_producer is not null)
        {
            try
            {
                _producer.Flush(_operationTimeout);
            }
            catch (KafkaException exception)
            {
                LogStopFailed(_logger, exception);
            }

            _producer.Dispose();
            _producer = null;
        }
    }

    /// <summary>
    /// 一个主题的消费循环：取消息、投递、提交；停止信号到来即退出。
    /// </summary>
    private async Task ConsumeLoopAsync(IConsumer<string?, byte[]> consumer, string topic, MessageDispatch dispatch, CancellationToken stopping)
    {
        while (!stopping.IsCancellationRequested)
        {
            ConsumeResult<string?, byte[]> consumed;
            try
            {
                consumed = consumer.Consume(stopping);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ConsumeException exception)
            {
                // 分区暂时不可用、协调器变更等由客户端自行恢复；记日志后继续
                LogConsumeFailed(_logger, topic, exception);
                continue;
            }

            if (consumed?.Message is null)
            {
                continue;
            }

            try
            {
                await DeliverAsync(topic, consumed, dispatch, stopping).ConfigureAwait(false);
                // 投递完成（成功、死信）才提交：停止时未完成的消息重启后重新投递
                consumer.Commit(consumed);
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested)
            {
                break;
            }
            catch (KafkaException exception)
            {
                LogConsumeFailed(_logger, topic, exception);
            }
        }
    }

    /// <summary>
    /// 投递一条消息：解码失败进死信；否则在原位重试到上限，仍失败进死信。
    /// </summary>
    private async Task DeliverAsync(string topic, ConsumeResult<string?, byte[]> consumed, MessageDispatch dispatch, CancellationToken cancellationToken)
    {
        var decoded = CloudEventsFormat.Decode(consumed.Message.Value);
        if (!decoded.TryGetValue(out var envelope))
        {
            LogUndecodable(_logger, topic, decoded.Error?.Message ?? "?");
            await ProduceAsync(TopicOf(topic) + _messaging.DeadLetterSuffix, new Message<string?, byte[]> { Key = consumed.Message.Key, Value = consumed.Message.Value, Headers = consumed.Message.Headers }, cancellationToken).ConfigureAwait(false);
            return;
        }

        // 宿主未进入服务状态前不投递，避免处理器在特性尚未就绪时执行
        while (!_app.State.IsServing())
        {
            await Task.Delay(10, cancellationToken).ConfigureAwait(false);
        }

        // 原位重试：不重新入队，分区内顺序不变
        for (var attempt = 1; ; attempt++)
        {
            Result result;
            try
            {
                result = await dispatch(new MessageDelivery(topic, envelope, attempt), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                result = Error.Unexpected(MessagingErrors.HandlerFailed, MessagingMessages.HandlerFailed, details: MessageArguments.Of(("reason", exception.Message)));
            }

            if (result.IsSuccess)
            {
                return;
            }

            if (attempt >= _messaging.MaxAttempts)
            {
                LogDeadLettered(_logger, envelope.Id, topic, attempt, result.Error?.Code ?? "?");
                await ProduceAsync(TopicOf(topic) + _messaging.DeadLetterSuffix, ToMessage(envelope, consumed.Message.Value, attempt), cancellationToken).ConfigureAwait(false);
                return;
            }
        }
    }

    /// <summary>
    /// 发布一条并等待投递报告。
    /// </summary>
    private async Task ProduceAsync(string kafkaTopic, Message<string?, byte[]> message, CancellationToken cancellationToken)
    {
        await ConnectAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var report = await _producer!.ProduceAsync(kafkaTopic, message, cancellationToken).ConfigureAwait(false);
            if (report.Status != PersistenceStatus.Persisted)
            {
                throw new FrameworkException(MessagingErrors.BrokerUnavailable, $"Failed to publish to topic '{kafkaTopic}': the message was not persisted ({report.Status}).");
            }
        }
        catch (KafkaException exception)
        {
            throw Unavailable($"Failed to publish to topic '{kafkaTopic}'", exception);
        }
    }

    /// <summary>
    /// 信封到消息：值是编码后的信封，键是分区键扩展属性（没有则为空，由 Kafka 分配分区），头带内容类型与尝试次数。
    /// </summary>
    private static Message<string?, byte[]> ToMessage(MessageEnvelope envelope, byte[] body, int attempt) => new()
    {
        Key = envelope.Extension(EnvelopeExtensions.PartitionKey).TryGetValue(out var key) ? key : null,
        Value = body,
        Headers = new Headers
        {
            { ContentTypeHeader, Encoding.UTF8.GetBytes(CloudEventsFormat.ContentType) },
            { AttemptHeader, Encoding.UTF8.GetBytes(attempt.ToString(CultureInfo.InvariantCulture)) },
        },
    };

    /// <summary>
    /// 共用的客户端配置：引导地址加透传属性。
    /// </summary>
    private ClientConfig ClientConfig()
    {
        var config = new ClientConfig(new Dictionary<string, string>(_options.Properties, StringComparer.Ordinal)) { BootstrapServers = _options.BootstrapServers };
        return config;
    }

    /// <summary>
    /// 生产者配置：幂等、全副本确认。
    /// </summary>
    private ProducerConfig ProducerConfig() => new(ClientConfig()) { EnableIdempotence = true, Acks = Acks.All };

    /// <summary>
    /// 消费者配置：手动提交、从最早位点开始（订阅前发布的消息也要收到）、允许自动建主题。
    /// </summary>
    private ConsumerConfig ConsumerConfig(string groupId) => new(ClientConfig())
    {
        GroupId = groupId,
        EnableAutoCommit = false,
        AutoOffsetReset = AutoOffsetReset.Earliest,
        AllowAutoCreateTopics = true,
        EnablePartitionEof = false,
    };

    /// <summary>
    /// 主题名：前缀加主题。
    /// </summary>
    private string TopicOf(string topic) => _options.TopicPrefix + topic;

    /// <summary>
    /// 代理不可达。
    /// </summary>
    private static FrameworkException Unavailable(string what, Exception exception)
        => new(MessagingErrors.BrokerUnavailable, $"{what}: {exception.Message}", exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Message {MessageId} on topic {Topic} was dead-lettered after attempt {Attempt} failed: {ErrorCode}")]
    private static partial void LogDeadLettered(ILogger logger, string messageId, string topic, int attempt, string errorCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Topic {Topic} received an undecodable message; it was moved to the dead-letter topic: {Reason}")]
    private static partial void LogUndecodable(ILogger logger, string topic, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Consuming topic {Topic} failed; the consumer keeps running.")]
    private static partial void LogConsumeFailed(ILogger logger, string topic, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "An error occurred while stopping the Kafka consumer.")]
    private static partial void LogStopFailed(ILogger logger, Exception exception);
}
