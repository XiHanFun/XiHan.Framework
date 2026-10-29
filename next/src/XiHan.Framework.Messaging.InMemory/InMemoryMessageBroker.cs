// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.InMemory;

/// <summary>
/// 每主题一条有界通道、单消费者顺序投递的内存代理。
/// </summary>
/// <remarks>
/// 批量发布一次入队：容量不足整批返回队列已满，不写入半批。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class InMemoryMessageBroker(InMemoryMessagingOptions options, MessagingOptions messaging, App app, ILoggerFactory loggerFactory) : IMessageBroker, IHealthContributor, IDisposable
{
    /// <summary>
    /// 内存代理选项：每主题容量。
    /// </summary>
    private readonly InMemoryMessagingOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>
    /// 消息选项：最多投递次数。
    /// </summary>
    private readonly MessagingOptions _messaging = messaging ?? throw new ArgumentNullException(nameof(messaging));

    /// <summary>
    /// 应用。
    /// </summary>
    /// <remarks>
    /// 只在其进入服务状态后才投递。
    /// </remarks>
    private readonly App _app = app ?? throw new ArgumentNullException(nameof(app));

    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<InMemoryMessageBroker>();

    /// <summary>
    /// 主题到队列的映射。
    /// </summary>
    /// <remarks>
    /// 首次发布或消费时创建。
    /// </remarks>
    private readonly ConcurrentDictionary<string, TopicQueue> _topics = new(StringComparer.Ordinal);

    /// <summary>
    /// 每个被消费主题的消费循环任务。
    /// </summary>
    private readonly List<Task> _loops = [];

    /// <summary>
    /// 停止信号。
    /// </summary>
    /// <remarks>
    /// 非空表示正在消费。
    /// </remarks>
    private CancellationTokenSource? _stopping;

    /// <inheritdoc />
    public ValueTask<Result> PublishAsync(string topic, MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        TopicName.Validate(topic);
        var queue = Queue(topic);
        // TryWrite 不等待：队列满立即返回 QueueFull，由发布方决定如何处理，不让发布者被消费速度拖住；
        // 与批量发布共用队列的发布锁，批量的容量判定与写入之间不会被单条插队
        lock (queue.PublishGate)
        {
            return ValueTask.FromResult(queue.Channel.Writer.TryWrite(new MessageDelivery(topic, envelope, 1))
                ? Result.Success()
                : QueueFull(topic));
        }
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Result>> PublishManyAsync(string topic, IReadOnlyList<MessageEnvelope> envelopes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelopes);
        TopicName.Validate(topic);
        if (envelopes.Count == 0)
        {
            return ValueTask.FromResult<IReadOnlyList<Result>>([]);
        }

        var deliveries = new MessageDelivery[envelopes.Count];
        for (var index = 0; index < envelopes.Count; index++)
        {
            ArgumentNullException.ThrowIfNull(envelopes[index]);
            deliveries[index] = new MessageDelivery(topic, envelopes[index], 1);
        }

        var queue = Queue(topic);
        var results = new Result[envelopes.Count];
        lock (queue.PublishGate)
        {
            // 先按剩余容量整批判定：装不下就一条都不写，调用方看到的是整批 QueueFull 而不是半批
            if (_options.MaxQueuedMessages - queue.Channel.Reader.Count < deliveries.Length)
            {
                Array.Fill(results, QueueFull(topic));
                return ValueTask.FromResult<IReadOnlyList<Result>>(results);
            }

            foreach (var delivery in deliveries)
            {
                queue.Channel.Writer.TryWrite(delivery);
            }
        }

        Array.Fill(results, Result.Success());
        return ValueTask.FromResult<IReadOnlyList<Result>>(results);
    }

    /// <inheritdoc />
    public Task StartAsync(IReadOnlyCollection<string> topics, MessageDispatch dispatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(topics);
        ArgumentNullException.ThrowIfNull(dispatch);
        if (_stopping is not null)
        {
            throw new InvalidOperationException("The in-memory broker is already consuming.");
        }

        _stopping = new CancellationTokenSource();
        foreach (var topic in topics)
        {
            var queue = Queue(topic);
            _loops.Add(Task.Run(() => ConsumeAsync(queue, dispatch, _stopping.Token), CancellationToken.None));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_stopping is null)
        {
            return;
        }

        await _stopping.CancelAsync().ConfigureAwait(false);
        try
        {
            await Task.WhenAll(_loops).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 消费循环按取消退出
        }

        _loops.Clear();
        _stopping.Dispose();
        _stopping = null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _stopping?.Dispose();
        _stopping = null;
    }

    /// <summary>
    /// 主题当前排队条数。
    /// </summary>
    public int QueuedCount(string topic) => Queue(topic).Channel.Reader.Count;

    /// <summary>
    /// 主题累计成功投递条数。
    /// </summary>
    public long DeliveredCount(string topic) => Interlocked.Read(ref Queue(topic).Delivered);

    /// <summary>
    /// 主题的死信（投递失败超过上限的消息），按进入顺序。
    /// </summary>
    public IReadOnlyList<MessageDelivery> DeadLetters(string topic) => [.. Queue(topic).DeadLetters];

    /// <inheritdoc />
    public IReadOnlyList<ContributorStatus> Check()
    {
        // 任一主题的队列排满即 Degraded：发布方此刻会收到 QueueFull；按主题名序数顺序取第一个，报告稳定
        var full = _topics.Where(pair => pair.Value.Channel.Reader.Count >= _options.MaxQueuedMessages).Select(static pair => pair.Key).Order(StringComparer.Ordinal).FirstOrDefault();
        return
        [
            full is null
                ? new ContributorStatus(InMemoryMessagingFeature.HealthName, LifecycleState.Ready)
                : new ContributorStatus(InMemoryMessagingFeature.HealthName, LifecycleState.Degraded, QueueFull(full)),
        ];
    }

    /// <summary>
    /// 取（必要时创建）主题的队列。
    /// </summary>
    private TopicQueue Queue(string topic) => _topics.GetOrAdd(topic, _ => new TopicQueue(_options.MaxQueuedMessages));

    /// <summary>
    /// 构造队列已满的错误。
    /// </summary>
    private Error QueueFull(string topic) => Error.Unavailable(MessagingErrors.QueueFull, MessagingMessages.QueueFull, MessageArguments.Of(("topic", topic), ("capacity", _options.MaxQueuedMessages)));

    /// <summary>
    /// 单主题消费循环：顺序取出投递交给派发器，失败重新入队或死信，直到取消。
    /// </summary>
    private async Task ConsumeAsync(TopicQueue queue, MessageDispatch dispatch, CancellationToken cancellationToken)
    {
        await foreach (var delivery in queue.Channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            // 宿主未进入服务状态前不投递，避免处理器在特性尚未就绪时执行
            while (!_app.State.IsServing())
            {
                await Task.Delay(10, cancellationToken).ConfigureAwait(false);
            }

            Result result;
            try
            {
                result = await dispatch(delivery, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // 停止时把未完成的投递放回队列，不计为失败
                queue.Channel.Writer.TryWrite(delivery);
                throw;
            }
            catch (Exception exception)
            {
                result = Error.Unexpected(MessagingErrors.HandlerFailed, MessagingMessages.HandlerFailed, details: MessageArguments.Of(("reason", exception.Message)));
            }

            if (result.IsSuccess)
            {
                Interlocked.Increment(ref queue.Delivered);
                continue;
            }

            // 未达上限则以下一次尝试号重新入队（队尾）；入队失败（队列已满）同样直接死信
            if (delivery.Attempt < _messaging.MaxAttempts && queue.Channel.Writer.TryWrite(new MessageDelivery(delivery.Topic, delivery.Envelope, delivery.Attempt + 1)))
            {
                continue;
            }

            LogDeadLettered(_logger, delivery.Envelope.Id, delivery.Topic, delivery.Attempt, result.Error?.Code ?? "?");
            queue.DeadLetters.Enqueue(delivery);
            while (queue.DeadLetters.Count > _options.MaxQueuedMessages && queue.DeadLetters.TryDequeue(out _))
            {
                // 死信同样有界：丢弃最旧
            }
        }
    }

    /// <summary>
    /// 记录一条消息重试到上限后进入死信。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Message {MessageId} on topic {Topic} was dead-lettered after attempt {Attempt} failed: {ErrorCode}")]
    private static partial void LogDeadLettered(ILogger logger, string messageId, string topic, int attempt, string errorCode);

    /// <summary>
    /// 一个主题的状态：有界通道、死信列表与投递计数。
    /// </summary>
    private sealed class TopicQueue(int capacity)
    {
        /// <summary>
        /// 有界、单消费者的投递通道。
        /// </summary>
        /// <remarks>
        /// 发布方用 TryWrite，满了不等待。
        /// </remarks>
        public Channel<MessageDelivery> Channel { get; } = System.Threading.Channels.Channel.CreateBounded<MessageDelivery>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait,
        });

        /// <summary>
        /// 发布锁。
        /// </summary>
        /// <remarks>
        /// 批量发布在锁内完成容量判定与整批写入，单条发布同样在锁内写入，二者不会交错。
        /// </remarks>
        public Lock PublishGate { get; } = new();

        /// <summary>
        /// 死信，按进入顺序。
        /// </summary>
        /// <remarks>
        /// 超过容量丢弃最旧。
        /// </remarks>
        public ConcurrentQueue<MessageDelivery> DeadLetters { get; } = new();

        /// <summary>
        /// 累计成功投递条数。
        /// </summary>
        /// <remarks>
        /// 用 <see cref="Interlocked"/> 读写。
        /// </remarks>
        public long Delivered;
    }
}
