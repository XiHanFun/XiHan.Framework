// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
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
/// 只有被订阅的主题（本进程登记了处理器，或交给 <see cref="StartAsync"/> 消费）才有通道；发布到没人订阅的主题直接丢弃并记一条 Debug 日志，发布照常成功，不建通道、不占主题名额。
/// 批量发布一次入队：容量不足整批返回队列已满，不写入半批。被订阅的主题数有上限（<see cref="InMemoryMessagingOptions.MaxTopics"/>），超出时启动失败（<see cref="MessagingErrors.TopicLimit"/>）。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class InMemoryMessageBroker(InMemoryMessagingOptions options, MessagingOptions messaging, MessageSubscriptions subscriptions, App app, ILoggerFactory loggerFactory) : IMessageBroker, IHealthContributor, IDisposable
{
    /// <summary>
    /// 内存代理选项：每主题容量与主题数上限。
    /// </summary>
    private readonly InMemoryMessagingOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>
    /// 消息选项：最多投递次数。
    /// </summary>
    private readonly MessagingOptions _messaging = messaging ?? throw new ArgumentNullException(nameof(messaging));

    /// <summary>
    /// 本进程的订阅表。
    /// </summary>
    /// <remarks>
    /// 发布到的主题有没有人订阅以它为准：订阅表在装配时定好，启动消费之前发布到被订阅主题的消息照样排队、启动后投递。
    /// </remarks>
    private readonly MessageSubscriptions _subscriptions = subscriptions ?? throw new ArgumentNullException(nameof(subscriptions));

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
    /// 被订阅主题到队列的映射。
    /// </summary>
    /// <remarks>
    /// 首次发布或消费时创建，不回收；只为被订阅的主题创建，条目数受订阅表与 <see cref="StartAsync"/> 的上限判定约束。
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
        if (!TryQueue(topic, out var queue))
        {
            LogDropped(_logger, 1, topic);
            return ValueTask.FromResult(Result.Success());
        }

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

        var results = new Result[envelopes.Count];
        if (!TryQueue(topic, out var queue))
        {
            // 没人订阅：整批丢弃、逐项成功，不建队列
            LogDropped(_logger, deliveries.Length, topic);
            Array.Fill(results, Result.Success());
            return ValueTask.FromResult<IReadOnlyList<Result>>(results);
        }

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

        // 先按上限判定再建队列、起消费循环：被订阅的主题（已有队列的与这次要消费的）超限时启动失败，不留下只消费了一部分主题的代理
        var subscribed = new HashSet<string>(_topics.Keys, StringComparer.Ordinal);
        subscribed.UnionWith(topics);
        if (subscribed.Count > _options.MaxTopics)
        {
            throw new FrameworkException(MessagingErrors.TopicLimit, $"The in-memory broker allows at most {_options.MaxTopics} subscribed topics, but {subscribed.Count} are subscribed. Raise {InMemoryMessagingOptions.ConfigurationSection}:MaxTopics.");
        }

        var queues = topics.Select(QueueOf).ToList();

        _stopping = new CancellationTokenSource();
        foreach (var queue in queues)
        {
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
    /// <remarks>
    /// 主题不存在为 0，不创建主题。
    /// </remarks>
    public int QueuedCount(string topic) => _topics.TryGetValue(topic, out var queue) ? queue.Channel.Reader.Count : 0;

    /// <summary>
    /// 主题累计成功投递条数。
    /// </summary>
    /// <remarks>
    /// 主题不存在为 0，不创建主题。
    /// </remarks>
    public long DeliveredCount(string topic) => _topics.TryGetValue(topic, out var queue) ? Interlocked.Read(ref queue.Delivered) : 0;

    /// <summary>
    /// 主题的死信（投递失败超过上限的消息），按进入顺序。
    /// </summary>
    /// <remarks>
    /// 主题不存在为空，不创建主题。
    /// </remarks>
    public IReadOnlyList<MessageDelivery> DeadLetters(string topic) => _topics.TryGetValue(topic, out var queue) ? [.. queue.DeadLetters] : [];

    /// <summary>
    /// 当前主题数。
    /// </summary>
    public int TopicCount => _topics.Count;

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
    /// 取发布要进的队列：被订阅的主题必要时创建。
    /// </summary>
    /// <remarks>
    /// 没人订阅（没有队列、订阅表里也没有）返回 <see langword="false"/>，不创建。
    /// </remarks>
    private bool TryQueue(string topic, [NotNullWhen(true)] out TopicQueue? queue)
    {
        if (_topics.TryGetValue(topic, out queue))
        {
            return true;
        }

        // 没人订阅的主题不建队列：没有消费者，缓冲只会占着内存与主题名额，直到队列排满
        if (_subscriptions.HandlersFor(topic).Count == 0)
        {
            return false;
        }

        queue = QueueOf(topic);
        return true;
    }

    /// <summary>
    /// 取（必要时创建）主题的队列，不判定订阅与上限。
    /// </summary>
    /// <remarks>
    /// 并发创建同一主题时只有一个队列胜出，其余的丢弃（通道没有需要释放的资源）。
    /// </remarks>
    private TopicQueue QueueOf(string topic) => _topics.GetOrAdd(topic, static (_, capacity) => new TopicQueue(capacity), _options.MaxQueuedMessages);

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
    /// 记录发布到没人订阅的主题的消息被丢弃。
    /// </summary>
    /// <remarks>
    /// Debug 级别：调度、工作流等框架事件照常发布到应用不一定订阅的主题，这是常态，按 Warning 记会让每次作业运行都刷日志；主题名写错时打开本类别的 Debug 日志排查。
    /// </remarks>
    [LoggerMessage(Level = LogLevel.Debug, Message = "Dropped {Count} message(s) published to topic '{Topic}' because nothing subscribes to it.")]
    private static partial void LogDropped(ILogger logger, int count, string topic);

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
