// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.CloudEvents;
using XiHan.Framework.Messaging.Abstractions.Handlers;

namespace XiHan.Framework.Messaging.Redis;

/// <summary>
/// 一个订阅在一条流上的消费
/// </summary>
/// <remarks>
/// 竞争订阅在消费组 <see cref="RedisMessagingOptions.ConsumerGroup"/> 里，广播订阅在本实例独占的组 <c>&lt;ConsumerGroup&gt;@&lt;InstanceId&gt;</c> 里，消费者名都是实例标识。消费循环单线程：取新条目、依次派发、成功即确认；失败在原位重试，到上限进死信流（广播丢弃）。每个维护周期清理遗留的组与消费者、整理已确认的条目，并以 <c>XAUTOCLAIM</c> 回收超时未确认的条目。
/// 尝试号就是待确认列表里的投递次数。被停止打断的投递不计尝试（RFC-0009「投递」第 3 条）：停止时把没有执行完的那一次、以及读到或认领到却还没派发的条目的投递次数退回一次（ADR-0284），回收之后仍是原来的尝试号；来不及退回（进程被强行终止、Redis 不可达）的那一次照旧计入，所以让进程崩溃的消息仍然会进死信
/// </remarks>
internal sealed partial class RedisStreamConsumer
{
    /// <summary>
    /// 维护间隔的上限
    /// </summary>
    /// <remarks>
    /// 实际间隔取它与 <see cref="RedisMessagingOptions.ClaimIdleTime"/> 中较小的一个
    /// </remarks>
    private static readonly TimeSpan _maxMaintenanceInterval = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 消费出错（Redis 不可达、超时）后再取之前的等待
    /// </summary>
    private static readonly TimeSpan _errorDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 回收扫描的起点与结束标志
    /// </summary>
    private static readonly RedisValue _scanStart = "0-0";

    /// <summary>
    /// 数据库
    /// </summary>
    private readonly IDatabase _database;

    /// <summary>
    /// Provider 选项：批量、轮询、回收阈值与长度上限
    /// </summary>
    private readonly RedisMessagingOptions _options;

    /// <summary>
    /// 消息选项：最多投递次数
    /// </summary>
    private readonly MessagingOptions _messaging;

    /// <summary>
    /// 应用：进入服务状态前不派发
    /// </summary>
    private readonly App _app;

    /// <summary>
    /// 时间源
    /// </summary>
    /// <remarks>
    /// 驱动轮询等待与维护周期
    /// </remarks>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// 日志
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 主题的流
    /// </summary>
    private readonly RedisKey _key;

    /// <summary>
    /// 主题的流键文本
    /// </summary>
    /// <remarks>
    /// 写日志用
    /// </remarks>
    private readonly string _stream;

    /// <summary>
    /// 主题的死信流
    /// </summary>
    private readonly RedisKey _deadLetterKey;

    /// <summary>
    /// 本订阅的消费组
    /// </summary>
    private readonly string _group;

    /// <summary>
    /// 本实例的广播组名
    /// </summary>
    /// <remarks>
    /// 维护时不把本实例自己的广播组当成遗留组
    /// </remarks>
    private readonly string _ownBroadcastGroup;

    /// <summary>
    /// 本实例的消费者名
    /// </summary>
    private readonly string _consumer;

    /// <summary>
    /// 维护间隔
    /// </summary>
    private readonly TimeSpan _maintenanceInterval;

    /// <summary>
    /// 回收扫描的游标
    /// </summary>
    /// <remarks>
    /// 回到 <c>0-0</c> 表示一轮扫描结束
    /// </remarks>
    private RedisValue _cursor = _scanStart;

    /// <summary>
    /// 创建一个订阅的消费
    /// </summary>
    public RedisStreamConsumer(IDatabase database, RedisMessagingOptions options, MessagingOptions messaging, MessagingInstance instance, App app, TimeProvider timeProvider, ILogger logger, MessageSubscription subscription)
    {
        ArgumentNullException.ThrowIfNull(instance);
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _messaging = messaging ?? throw new ArgumentNullException(nameof(messaging));
        _app = app ?? throw new ArgumentNullException(nameof(app));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Subscription = subscription ?? throw new ArgumentNullException(nameof(subscription));
        _key = RedisMessageBroker.KeyOf(options, subscription.Topic);
        _stream = options.KeyPrefix + subscription.Topic;
        _deadLetterKey = RedisMessageBroker.KeyOf(options, subscription.Topic + messaging.DeadLetterSuffix);
        _ownBroadcastGroup = options.ConsumerGroup + "@" + instance.Id;
        _group = IsBroadcast ? _ownBroadcastGroup : options.ConsumerGroup;
        _consumer = instance.Id;
        _maintenanceInterval = options.ClaimIdleTime < _maxMaintenanceInterval ? options.ClaimIdleTime : _maxMaintenanceInterval;
    }

    /// <summary>
    /// 订阅
    /// </summary>
    public MessageSubscription Subscription { get; }

    /// <summary>
    /// 是否广播订阅
    /// </summary>
    private bool IsBroadcast => Subscription.Mode == SubscriptionMode.Broadcast;

    /// <summary>
    /// 回收阈值的毫秒数
    /// </summary>
    private long ClaimIdleMilliseconds => (long)_options.ClaimIdleTime.TotalMilliseconds;

    /// <summary>
    /// 建消费组与本实例的消费者
    /// </summary>
    /// <remarks>
    /// 幂等：组已存在时只建消费者。订阅方第一次启动时连同流一起建出，之后的发布才写得进去
    /// </remarks>
    public Task EnsureGroupAsync()
        => _database.ScriptEvaluateAsync(RedisStreamScripts.EnsureGroup, [_key], [_group, _consumer]);

    /// <summary>
    /// 停止后清理本实例在流上的痕迹
    /// </summary>
    /// <remarks>
    /// 广播订阅删除自己的组（流上不剩组时连流删除），竞争订阅在没有待确认条目时删除自己的消费者
    /// </remarks>
    public Task ReleaseAsync()
        => IsBroadcast
            ? _database.ScriptEvaluateAsync(RedisStreamScripts.ReleaseGroup, [_key], [_group])
            : _database.ScriptEvaluateAsync(RedisStreamScripts.ReleaseConsumer, [_key], [_group, _consumer]);

    /// <summary>
    /// 消费循环
    /// </summary>
    /// <remarks>
    /// 只有停止信号让它退出：Redis 出错时记日志、等一会儿再取，组不见了（Redis 重启后丢了数据、被外部删除）时重新建组
    /// </remarks>
    public async Task RunAsync(MessageDispatch dispatch, CancellationToken stopping)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        // 启动后立即维护一次：接手上一次崩溃留下的待确认条目与遗留的组，不用等满一个周期
        long? lastMaintenance = null;
        var reclaiming = false;
        while (!stopping.IsCancellationRequested)
        {
            try
            {
                var handled = 0;
                if (!reclaiming && (lastMaintenance is not { } last || _timeProvider.GetElapsedTime(last) >= _maintenanceInterval))
                {
                    // 先记下时间再维护：维护失败也要等下一个周期，不让它挡住取新条目
                    lastMaintenance = _timeProvider.GetTimestamp();
                    await TryMaintainAsync().ConfigureAwait(false);
                    reclaiming = true;
                    _cursor = _scanStart;
                }

                // 一轮回收可能要分几次扫完：游标没回到起点就在下一次循环接着扫，期间照常取新条目
                if (reclaiming)
                {
                    handled += await ReclaimAsync(dispatch, stopping).ConfigureAwait(false);
                    reclaiming = _cursor != _scanStart;
                }

                var entries = await _database.StreamReadGroupAsync(_key, _group, _consumer, StreamPosition.NewMessages, _options.BatchSize).ConfigureAwait(false);
                for (var index = 0; index < entries.Length; index++)
                {
                    // 停止时这一批剩下的条目不再派发：它们留在本消费者的待确认列表里，由别的实例回收
                    // 读取已经让它们的投递次数成了 1，而它们一次也没有派发过：退回去，回收之后从第 1 次尝试起
                    if (stopping.IsCancellationRequested)
                    {
                        await RestoreDeliveryCountsAsync([.. entries[index..].Select(static entry => (entry.Id, 1))]).ConfigureAwait(false);
                        break;
                    }

                    await DeliverAsync(entries[index], attempt: 1, dispatch, stopping).ConfigureAwait(false);
                }

                handled += entries.Length;
                if (handled == 0)
                {
                    await Task.Delay(_options.PollInterval, _timeProvider, stopping).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested)
            {
                break;
            }
            catch (RedisServerException exception) when (IsMissingGroup(exception))
            {
                // 组或流没了：重新建组从 $ 开始，这期间写入的条目不再投递
                LogGroupRecreated(_logger, _group, Subscription.Topic);
                await RecreateGroupAsync(stopping).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is RedisException or TimeoutException)
            {
                // 正在处理的条目没有确认，留在待确认列表里，恢复后由回收接着投递；循环不退出，否则这个订阅再也没人消费
                LogConsumeFailed(_logger, Subscription.Topic, _group, _errorDelay, exception);
                await DelayQuietlyAsync(_errorDelay, stopping).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// 组不存在的错误
    /// </summary>
    private static bool IsMissingGroup(RedisServerException exception)
        => exception.Message.StartsWith("NOGROUP", StringComparison.Ordinal);

    /// <summary>
    /// 等待，停止信号到来时提前返回而不抛出
    /// </summary>
    private async Task DelayQuietlyAsync(TimeSpan delay, CancellationToken stopping)
    {
        try
        {
            await Task.Delay(delay, _timeProvider, stopping).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            // 停止：交给循环条件退出
        }
    }

    /// <summary>
    /// 重新建组，失败时记日志并等一会儿
    /// </summary>
    private async Task RecreateGroupAsync(CancellationToken stopping)
    {
        try
        {
            await EnsureGroupAsync().ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            LogConsumeFailed(_logger, Subscription.Topic, _group, _errorDelay, exception);
            await DelayQuietlyAsync(_errorDelay, stopping).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 一次维护：清理遗留的广播组与空闲的消费者、整理已确认的条目
    /// </summary>
    /// <remarks>
    /// 失败只记日志：维护不影响投递，下一个周期再做
    /// </remarks>
    private async Task TryMaintainAsync()
    {
        RedisResult result;
        try
        {
            result = await _database.ScriptEvaluateAsync(
                RedisStreamScripts.Maintain,
                [_key],
                [_options.ConsumerGroup + "@", _group, _ownBroadcastGroup, _consumer, ClaimIdleMilliseconds, IsBroadcast ? "0" : "1"]).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            LogMaintenanceFailed(_logger, _stream, exception);
            return;
        }

        var counts = (long[]?)result ?? [];
        if (counts.Length == 3 && (counts[0] > 0 || counts[1] > 0 || counts[2] > 0))
        {
            LogMaintained(_logger, _stream, counts[0], counts[1], counts[2]);
        }
    }

    /// <summary>
    /// 回收一批超时未确认的条目并接着投递
    /// </summary>
    /// <remarks>
    /// <c>XAUTOCLAIM</c> 认领时投递次数加一；认领到的条目再逐个查一次待确认列表取投递次数作尝试号（流水线发出，一次往返）。查不到的条目已被别人确认或认领，跳过。返回认领到的条目数
    /// </remarks>
    private async Task<int> ReclaimAsync(MessageDispatch dispatch, CancellationToken stopping)
    {
        var claim = await _database.StreamAutoClaimAsync(_key, _group, _consumer, ClaimIdleMilliseconds, _cursor, _options.BatchSize).ConfigureAwait(false);
        _cursor = claim.IsNull ? _scanStart : claim.NextStartId;
        if (claim.IsNull)
        {
            return 0;
        }

        if (claim.DeletedIds.Length > 0)
        {
            // 待确认的条目在确认之前就被 MAXLEN 裁掉了：消费者落后超过了 MaxLength，这些消息丢失
            LogTrimmedBeforeAcknowledged(_logger, claim.DeletedIds.Length, Subscription.Topic, _group);
        }

        var claimed = claim.ClaimedEntries.Where(static e => !e.IsNull).ToArray();
        if (claimed.Length == 0)
        {
            return 0;
        }

        var lookups = claimed.Select(e => _database.StreamPendingMessagesAsync(_key, _group, 1, _consumer, e.Id, e.Id)).ToArray();
        var pending = await Task.WhenAll(lookups).ConfigureAwait(false);
        for (var index = 0; index < claimed.Length; index++)
        {
            if (stopping.IsCancellationRequested)
            {
                // 认领让这些条目的投递次数加了一，而它们还没轮到派发：退回去，否则每停一次就白白用掉一次尝试
                await RestoreDeliveryCountsAsync([.. Enumerable.Range(index, claimed.Length - index).Where(rest => pending[rest].Length > 0).Select(rest => (claimed[rest].Id, AttemptOf(pending[rest][0])))]).ConfigureAwait(false);
                break;
            }

            if (pending[index].Length == 0)
            {
                continue;
            }

            var attempt = AttemptOf(pending[index][0]);
            var entryId = claimed[index].Id.ToString();
            LogReclaimed(_logger, entryId, Subscription.Topic, _group, attempt);
            await DeliverAsync(claimed[index], attempt, dispatch, stopping).ConfigureAwait(false);
        }

        return claimed.Length;
    }

    /// <summary>
    /// 投递一个条目
    /// </summary>
    /// <remarks>
    /// 解码失败：竞争订阅原样进死信流，广播订阅确认掉。尝试号已超过上限（回收来的）：不再派发，按到上限处理。否则派发（每次尝试之前都等应用进入服务状态），成功即确认；失败在原位重试，每次重试前把条目认领给自己让投递次数加一，到上限竞争订阅进死信流、广播订阅确认掉。停止时不确认，留给回收；被打断的这一次从投递次数里退掉
    /// </remarks>
    private async Task DeliverAsync(StreamEntry entry, int attempt, MessageDispatch dispatch, CancellationToken stopping)
    {
        var topic = Subscription.Topic;
        var body = entry[RedisMessageBroker.EnvelopeField];
        // 没有 envelope 字段的条目按空文档解码：同样解不开，错误说明文档不是合法的 CloudEvents JSON
        var decoded = CloudEventsFormat.Decode(body.IsNull ? ReadOnlyMemory<byte>.Empty : (byte[]?)body);
        if (!decoded.TryGetValue(out var envelope))
        {
            // 无法解码的条目不派发、不重试；怎么了结由 MessageDeliveryAttempt 判定，这里只翻译成流上的动作（ADR-0275）
            await SettleAsync(entry.Id, body, attempt, MessageDeliveryAttempt.Undecodable(Subscription, decoded.Error, _logger)).ConfigureAwait(false);
            return;
        }

        if (attempt > _messaging.MaxAttempts)
        {
            // 回收来的条目已投递过上限次数（之前的实例崩溃、或死信写入失败）：不再执行处理器，最后一次的错误无从得知
            var exhausted = MessageDeliveryAttempt.Exhausted(new MessageDelivery { Topic = topic, Envelope = envelope, Attempt = attempt, Mode = Subscription.Mode }, null, _logger);
            await SettleAsync(entry.Id, body, attempt, exhausted).ConfigureAwait(false);
            return;
        }

        // 等服务状态、派发与归类在 MessageDeliveryAttempt，这里只把结论翻译成流上的动作（ADR-0261）
        for (; ; attempt++)
        {
            var outcome = await MessageDeliveryAttempt.RunAsync(
                dispatch,
                new MessageDelivery
                {
                    Topic = topic,
                    Envelope = envelope,
                    Attempt = attempt,
                    Mode = Subscription.Mode,
                },
                _messaging,
                _app,
                _logger,
                stopping).ConfigureAwait(false);
            if (outcome.Disposition == DeliveryDisposition.Abandon)
            {
                // 停止打断了这次投递，或失败发生在停止期间（多半是取消造成的，管道把取消变成失败结果返回）：
                // 不确认、不在这里重试也不进死信，条目留在待确认列表里，由别的实例回收后按投递次数接着投递
                // 这一次不计尝试：读取或认领已经把它算进了投递次数，退回去；否则回收再加一，赶上最后一次尝试的条目会超过上限、不派发就进死信
                await RestoreDeliveryCountsAsync([(entry.Id, attempt)]).ConfigureAwait(false);
                return;
            }

            if (outcome.Disposition == DeliveryDisposition.Complete)
            {
                await AcknowledgeAsync(entry.Id).ConfigureAwait(false);
                return;
            }

            if (outcome.Disposition != DeliveryDisposition.Retry)
            {
                await SettleAsync(entry.Id, body, attempt, outcome).ConfigureAwait(false);
                return;
            }

            // 原位重试：先把条目认领给自己（最小空闲 0），待确认列表里的投递次数加一、空闲时间清零——
            // 尝试号落在 Redis 里，崩溃后接手的实例接着计数，重试期间也不会被别的实例回收
            var claimed = await _database.StreamClaimAsync(_key, _group, _consumer, 0, [entry.Id]).ConfigureAwait(false);
            if (claimed.Length == 0)
            {
                // 条目已被裁剪、已被确认或组被删除：没有可以继续的投递
                LogNoLongerPending(_logger, envelope.Id, topic, _group);
                return;
            }
        }
    }

    /// <summary>
    /// 待确认条目的投递次数，即它的尝试号
    /// </summary>
    private static int AttemptOf(StreamPendingMessageInfo pending) => (int)Math.Min(pending.DeliveryCount, int.MaxValue);

    /// <summary>
    /// 把被停止打断、没有执行完的投递从投递次数里退掉
    /// </summary>
    /// <remarks>
    /// 每项是条目 ID 与本实例看到的投递次数（它的尝试号）；脚本只动仍在本消费者名下、次数没变的条目。不随停止信号取消：正是停止时才要做。
    /// 失败只记日志、不抛出：这些条目的那一次照旧计入，回收之后尝试号多一，最坏是少处理一次就进死信（与进程被强行终止时相同），不会丢消息，也不会让停止流程卡住
    /// </remarks>
    private async Task RestoreDeliveryCountsAsync(IReadOnlyList<(RedisValue Id, int DeliveryCount)> entries)
    {
        if (entries.Count == 0)
        {
            return;
        }

        var arguments = new RedisValue[2 + (entries.Count * 2)];
        arguments[0] = _group;
        arguments[1] = _consumer;
        for (var index = 0; index < entries.Count; index++)
        {
            arguments[2 + (index * 2)] = entries[index].Id;
            arguments[3 + (index * 2)] = entries[index].DeliveryCount;
        }

        try
        {
            await _database.ScriptEvaluateAsync(RedisStreamScripts.RestoreDeliveryCounts, [_key], arguments).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            LogRestoreFailed(_logger, entries.Count, Subscription.Topic, _group, exception);
        }
    }

    /// <summary>
    /// 了结一条不再重试的条目：用完了尝试次数，或无法解码
    /// </summary>
    /// <remarks>
    /// 广播订阅（<see cref="DeliveryDisposition.Discard"/>）确认掉，不进死信；竞争订阅（<see cref="DeliveryDisposition.DeadLetter"/>）进死信流，带上结论里的错误码（最后一次失败的，或解码给出的；不知道时不带）。Warning 日志由 <see cref="MessageDeliveryAttempt"/> 记过
    /// </remarks>
    private async Task SettleAsync(RedisValue id, RedisValue body, int attempt, MessageDeliveryOutcome outcome)
    {
        if (outcome.Disposition != DeliveryDisposition.DeadLetter)
        {
            // 广播是尽力而为、没有死信：每个实例各自的一份失败没有可以重放的共享位置
            await AcknowledgeAsync(id).ConfigureAwait(false);
            return;
        }

        await DeadLetterAsync(id, body, attempt, outcome.Error?.Code).ConfigureAwait(false);
    }

    /// <summary>
    /// 把条目写进死信流，写入得到应答后才确认原条目
    /// </summary>
    /// <remarks>
    /// 死信流由死信的一方建出（<c>MKSTREAM</c>），同样以 <c>MAXLEN ~</c> 裁剪。写入失败抛出，原条目不确认，由回收接着处理；确认失败时死信可能写两遍
    /// </remarks>
    private async Task DeadLetterAsync(RedisValue id, RedisValue body, int attempt, string? errorCode)
    {
        NameValueEntry[] fields = errorCode is null
            ? [new(RedisMessageBroker.EnvelopeField, body.IsNull ? RedisValue.EmptyString : body), new(RedisMessageBroker.AttemptField, attempt.ToString(CultureInfo.InvariantCulture))]
            : [new(RedisMessageBroker.EnvelopeField, body.IsNull ? RedisValue.EmptyString : body), new(RedisMessageBroker.AttemptField, attempt.ToString(CultureInfo.InvariantCulture)), new(RedisMessageBroker.ErrorField, errorCode)];
        await _database.StreamAddAsync(_deadLetterKey, fields, new StreamAddOptions { MaxLength = _options.MaxLength, Approximate = true }).ConfigureAwait(false);
        await AcknowledgeAsync(id).ConfigureAwait(false);
    }

    /// <summary>
    /// 确认一个条目
    /// </summary>
    /// <remarks>
    /// 不随停止信号取消：处理完的条目要确认掉，否则会被回收后再投递一次
    /// </remarks>
    private Task<long> AcknowledgeAsync(RedisValue id) => _database.StreamAcknowledgeAsync(_key, _group, id);

    /// <summary>
    /// 记录一次维护清理或整理了什么
    /// </summary>
    [LoggerMessage(Level = LogLevel.Debug, Message = "Maintained stream '{Stream}': removed {Groups} abandoned broadcast group(s) and {Consumers} idle consumer(s), trimmed {Entries} acknowledged entries.")]
    private static partial void LogMaintained(ILogger logger, string stream, long groups, long consumers, long entries);

    /// <summary>
    /// 记录一次维护失败
    /// </summary>
    /// <remarks>
    /// 投递照常，下一个周期再维护
    /// </remarks>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Maintaining stream '{Stream}' failed; it is retried in the next maintenance cycle.")]
    private static partial void LogMaintenanceFailed(ILogger logger, string stream, Exception exception);

    /// <summary>
    /// 记录回收了一个超时未确认的条目
    /// </summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Reclaimed entry {EntryId} on topic {Topic} (group {Group}) after it stayed unacknowledged; delivering attempt {Attempt}.")]
    private static partial void LogReclaimed(ILogger logger, string entryId, string topic, string group, int attempt);

    /// <summary>
    /// 记录待确认的条目在确认前被裁剪
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "{Count} pending entries on topic {Topic} (group {Group}) were trimmed before they were acknowledged; the consumers fell behind by more than MaxLength and these messages are lost.")]
    private static partial void LogTrimmedBeforeAcknowledged(ILogger logger, int count, string topic, string group);

    /// <summary>
    /// 记录停止时没能把被打断的投递从投递次数里退掉
    /// </summary>
    /// <remarks>
    /// 这些条目的那一次照旧计入尝试上限
    /// </remarks>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Restoring the delivery count of {Count} interrupted entries on topic {Topic} (group {Group}) failed; the interrupted attempts stay counted.")]
    private static partial void LogRestoreFailed(ILogger logger, int count, string topic, string group, Exception exception);

    /// <summary>
    /// 记录重试前条目已不在待确认列表里
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Message {MessageId} on topic {Topic} is no longer pending in group {Group} (trimmed, acknowledged elsewhere or the group was removed); it is not retried.")]
    private static partial void LogNoLongerPending(ILogger logger, string messageId, string topic, string group);

    /// <summary>
    /// 记录消费组不存在后重新建组
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Consumer group {Group} on topic {Topic} no longer exists; it is recreated from the end of the stream.")]
    private static partial void LogGroupRecreated(ILogger logger, string group, string topic);

    /// <summary>
    /// 记录消费出错，循环等待后继续
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Consuming topic {Topic} (group {Group}) failed; retrying in {Delay}.")]
    private static partial void LogConsumeFailed(ILogger logger, string topic, string group, TimeSpan delay, Exception exception);
}
