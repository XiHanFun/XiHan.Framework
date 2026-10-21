// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.InMemory;

/// <summary>
/// 每主题一条有界通道、单消费者顺序投递的内存代理。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class InMemoryMessageBroker(InMemoryMessagingOptions options, MessagingOptions messaging, App app, ILoggerFactory loggerFactory) : IMessageBroker, IDisposable
{
    private readonly InMemoryMessagingOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly MessagingOptions _messaging = messaging ?? throw new ArgumentNullException(nameof(messaging));
    private readonly App _app = app ?? throw new ArgumentNullException(nameof(app));
    private readonly ILogger _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<InMemoryMessageBroker>();
    private readonly ConcurrentDictionary<string, TopicQueue> _topics = new(StringComparer.Ordinal);
    private readonly List<Task> _loops = [];
    private CancellationTokenSource? _stopping;

    /// <inheritdoc />
    public ValueTask<Result> PublishAsync(string topic, MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        TopicName.Validate(topic);
        var queue = Queue(topic);
        return ValueTask.FromResult(queue.Channel.Writer.TryWrite(new MessageDelivery(topic, envelope, 1))
            ? Result.Success()
            : Error.Unavailable(MessagingErrors.QueueFull, $"主题 '{topic}' 的内存队列已满（上限 {_options.MaxQueuedMessages}）。"));
    }

    /// <inheritdoc />
    public Task StartAsync(IReadOnlyCollection<string> topics, MessageDispatch dispatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(topics);
        ArgumentNullException.ThrowIfNull(dispatch);
        if (_stopping is not null)
        {
            throw new InvalidOperationException("内存代理已在消费。");
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

    private TopicQueue Queue(string topic) => _topics.GetOrAdd(topic, _ => new TopicQueue(_options.MaxQueuedMessages));

    private async Task ConsumeAsync(TopicQueue queue, MessageDispatch dispatch, CancellationToken cancellationToken)
    {
        await foreach (var delivery in queue.Channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
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
                queue.Channel.Writer.TryWrite(delivery);
                throw;
            }
            catch (Exception exception)
            {
                result = Error.Unexpected("MESSAGING_UNEXPECTED_001", exception.Message);
            }

            if (result.IsSuccess)
            {
                Interlocked.Increment(ref queue.Delivered);
                continue;
            }

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

    [LoggerMessage(Level = LogLevel.Warning, Message = "消息 {MessageId}（主题 {Topic}）第 {Attempt} 次投递失败后进入死信：{ErrorCode}")]
    private static partial void LogDeadLettered(ILogger logger, string messageId, string topic, int attempt, string errorCode);

    private sealed class TopicQueue(int capacity)
    {
        public Channel<MessageDelivery> Channel { get; } = System.Threading.Channels.Channel.CreateBounded<MessageDelivery>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait,
        });

        public ConcurrentQueue<MessageDelivery> DeadLetters { get; } = new();

        public long Delivered;
    }
}
