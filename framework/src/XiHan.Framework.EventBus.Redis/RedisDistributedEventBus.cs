// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
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

namespace XiHan.Framework.EventBus.Redis;

/// <summary>
/// 基于 Redis Streams 的分布式事件总线
/// </summary>
/// <remarks>
/// 发布：以 <c>XADD</c> 把事件写入同一 Stream，字段含事件名 / messageId / correlationId / 数据。
/// 消费：通过消费者组 <c>XREADGROUP</c> 竞争消费，处理成功后 <c>XACK</c>，保证分布式事件在集群中只被处理一次。
/// 独立连接（与缓存各连各的），带近似长度裁剪防止 Stream 无限增长。
/// </remarks>
[ExposeServices(typeof(IDistributedEventBus), typeof(RedisDistributedEventBus))]
public class RedisDistributedEventBus : BrokerDistributedEventBusBase, ISingletonDependency, IAsyncDisposable
{
    private const string FieldEvent = "event";
    private const string FieldMessageId = "mid";
    private const string FieldCorrelationId = "cid";
    private const string FieldData = "data";

    private readonly XiHanRedisEventBusOptions _options;
    private readonly ILogger<RedisDistributedEventBus> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly string _consumerName = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    private IConnectionMultiplexer? _connection;
    private CancellationTokenSource? _consumeCts;
    private Task? _consumeTask;
    private volatile bool _initialized;

    /// <summary>
    /// 构造函数
    /// </summary>
    public RedisDistributedEventBus(
        IServiceScopeFactory serviceScopeFactory,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager unitOfWorkManager,
        IOptions<XiHanDistributedEventBusOptions> distributedEventBusOptions,
        IOptions<XiHanRedisEventBusOptions> redisOptions,
        IDistributedIdGenerator<Guid> guidGenerator,
        IClock clock,
        IEventHandlerInvoker eventHandlerInvoker,
        ILocalEventBus localEventBus,
        ICorrelationIdProvider correlationIdProvider,
        ILogger<RedisDistributedEventBus> logger)
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
        _options = redisOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// 初始化：建立连接、创建消费者组并启动消费循环
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

            _connection = await ConnectionMultiplexer.ConnectAsync(_options.Configuration);
            await EnsureConsumerGroupAsync();

            _consumeCts = new CancellationTokenSource();
            _consumeTask = Task.Factory.StartNew(
                () => ConsumeLoopAsync(_consumeCts.Token),
                _consumeCts.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default).Unwrap();

            _initialized = true;
            _logger.LogInformation("Redis 分布式事件总线已初始化：stream={Stream}, group={Group}, consumer={Consumer}",
                _options.StreamKey, _options.ConsumerGroup, _consumerName);
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// 把事件写入 Redis Stream
    /// </summary>
    protected override async Task PublishToBrokerAsync(string eventName, byte[] body, string? messageId, string? correlationId)
    {
        if (!_initialized || _connection is null)
        {
            await InitializeAsync();
        }

        var db = _connection!.GetDatabase();
        var fields = new NameValueEntry[]
        {
            new(FieldEvent, eventName),
            new(FieldMessageId, messageId ?? string.Empty),
            new(FieldCorrelationId, correlationId ?? string.Empty),
            new(FieldData, body)
        };

        if (_options.MaxStreamLength > 0)
        {
            await db.StreamAddAsync(
                _options.StreamKey,
                fields,
                messageId: null,
                maxLength: _options.MaxStreamLength,
                useApproximateMaxLength: true);
        }
        else
        {
            await db.StreamAddAsync(_options.StreamKey, fields);
        }
    }

    /// <summary>
    /// 释放连接与消费循环
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        try
        {
            if (_consumeCts is not null)
            {
                await _consumeCts.CancelAsync();
            }

            if (_consumeTask is not null)
            {
                try
                {
                    await _consumeTask;
                }
                catch (OperationCanceledException)
                {
                    // 正常退出
                }
            }

            if (_connection is not null)
            {
                await _connection.CloseAsync();
                _connection.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "释放 Redis 连接时发生异常");
        }

        _consumeCts?.Dispose();
        _initLock.Dispose();
    }

    /// <summary>
    /// 确保消费者组存在（幂等）
    /// </summary>
    private async Task EnsureConsumerGroupAsync()
    {
        var db = _connection!.GetDatabase();
        try
        {
            await db.StreamCreateConsumerGroupAsync(
                _options.StreamKey,
                _options.ConsumerGroup,
                StreamPosition.NewMessages,
                createStream: true);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP", StringComparison.OrdinalIgnoreCase))
        {
            // 消费者组已存在，忽略
        }
    }

    /// <summary>
    /// 消费循环：XREADGROUP → 处理 → XACK
    /// </summary>
    private async Task ConsumeLoopAsync(CancellationToken cancellationToken)
    {
        var db = _connection!.GetDatabase();

        // 启动即先接管一次：本进程上一轮生命周期崩溃留下的待处理消息，
        // 换了消费者名之后不会被自动重投，只能靠接管取回
        var nextClaimAt = DateTimeOffset.MinValue;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (DateTimeOffset.UtcNow >= nextClaimAt)
            {
                nextClaimAt = DateTimeOffset.UtcNow.AddMilliseconds(_options.ClaimIntervalMilliseconds);

                try
                {
                    await ClaimStaleMessagesAsync(db, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "接管滞留 Redis 事件失败，stream={Stream}", _options.StreamKey);
                }
            }

            StreamEntry[] entries;
            try
            {
                entries = await db.StreamReadGroupAsync(
                    _options.StreamKey,
                    _options.ConsumerGroup,
                    _consumerName,
                    StreamPosition.NewMessages,
                    _options.ReadBatchSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "读取 Redis Stream 失败，stream={Stream}", _options.StreamKey);
                await Task.Delay(_options.PollIntervalMilliseconds, cancellationToken);
                continue;
            }

            if (entries is null || entries.Length == 0)
            {
                await Task.Delay(_options.PollIntervalMilliseconds, cancellationToken);
                continue;
            }

            await ProcessEntriesAsync(db, entries);
        }
    }

    /// <summary>
    /// 处理一批条目：成功则确认，失败则留在待处理列表等待重投
    /// </summary>
    /// <remarks>
    /// 失败时不确认是有意的：确认等于宣告这条消息已处理完毕，之后 Redis 不会再投递它，
    /// 消息随即永久消失。留在待处理列表则会被 <see cref="ClaimStaleMessagesAsync"/> 重新接管，
    /// 重投次数超过上限后转入死信 Stream，既不会无限重试也不会凭空丢失。
    /// </remarks>
    /// <param name="db">数据库</param>
    /// <param name="entries">条目集合</param>
    private async Task ProcessEntriesAsync(IDatabase db, StreamEntry[] entries)
    {
        foreach (var entry in entries)
        {
            try
            {
                var eventName = GetField(entry, FieldEvent).ToString();
                var messageId = GetField(entry, FieldMessageId);
                var correlationId = GetField(entry, FieldCorrelationId);
                var body = (byte[]?)GetField(entry, FieldData) ?? [];

                await ProcessIncomingMessageAsync(
                    messageId.IsNullOrEmpty ? null : messageId.ToString(),
                    eventName,
                    correlationId.IsNullOrEmpty ? null : correlationId.ToString(),
                    body);

                await db.StreamAcknowledgeAsync(_options.StreamKey, _options.ConsumerGroup, entry.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理 Redis 事件失败，将等待重投，stream={Stream}，id={Id}", _options.StreamKey, entry.Id);
            }
        }
    }

    /// <summary>
    /// 接管滞留在其他消费者名下的待处理消息
    /// </summary>
    /// <remarks>
    /// 消费者在读取与确认之间崩溃、或进程重启换了消费者名时，原消息会永远留在旧消费者的
    /// 待处理列表里，既不会被再次投递也不会被清理。此处按空闲时长把它们接管过来重新处理，
    /// 投递次数超过上限的转入死信 Stream 后确认，避免毒消息无限占用消费能力。
    /// </remarks>
    /// <param name="db">数据库</param>
    /// <param name="cancellationToken">取消令牌</param>
    private async Task ClaimStaleMessagesAsync(IDatabase db, CancellationToken cancellationToken)
    {
        StreamPendingMessageInfo[] pending;

        try
        {
            // 当前客户端版本的 XPENDING 没有最小空闲时长参数，取回后在下面按空闲时长筛选
            pending = await db.StreamPendingMessagesAsync(
                _options.StreamKey,
                _options.ConsumerGroup,
                _options.ClaimBatchSize,
                RedisValue.Null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询 Redis Stream 待处理消息失败，stream={Stream}", _options.StreamKey);
            return;
        }

        if (pending is null || pending.Length == 0)
        {
            return;
        }

        var plan = StaleMessagePlanner.Plan(
            [.. pending.Select(message => new PendingMessageSnapshot(
                message.MessageId.ToString(),
                message.IdleTimeInMilliseconds,
                message.DeliveryCount))],
            _options.ClaimMinIdleMilliseconds,
            _options.MaxDeliveryCount);

        foreach (var message in plan.ToDeadLetter)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await MoveToDeadLetterAsync(db, message);
        }

        if (plan.ToClaim.Count == 0)
        {
            return;
        }

        _logger.LogWarning(
            "接管 {Count} 条滞留的 Redis 事件，stream={Stream}，空闲阈值={MinIdle}ms",
            plan.ToClaim.Count, _options.StreamKey, _options.ClaimMinIdleMilliseconds);

        var claimed = await db.StreamClaimAsync(
            _options.StreamKey,
            _options.ConsumerGroup,
            _consumerName,
            _options.ClaimMinIdleMilliseconds,
            [.. plan.ToClaim.Select(id => (RedisValue)id)]);

        await ProcessEntriesAsync(db, claimed);
    }

    /// <summary>
    /// 把超过投递次数上限的消息转入死信 Stream 并确认原消息
    /// </summary>
    /// <param name="db">数据库</param>
    /// <param name="message">待处理消息信息</param>
    private async Task MoveToDeadLetterAsync(IDatabase db, PendingMessageSnapshot message)
    {
        try
        {
            // 先取回原始内容再确认，确认之后就再也读不到它了
            var entries = await db.StreamRangeAsync(_options.StreamKey, message.MessageId, message.MessageId, 1);
            var fields = entries.Length > 0 ? entries[0].Values : [];

            var deadLetterFields = new List<NameValueEntry>(fields)
            {
                new("dead_reason", "投递次数超过上限"),
                new("dead_delivery_count", message.DeliveryCount),
                new("dead_original_id", message.MessageId),
                new("dead_time", Clock.Now.ToString("O"))
            };

            await db.StreamAddAsync(_options.ResolveDeadLetterStreamKey(), [.. deadLetterFields]);
            await db.StreamAcknowledgeAsync(_options.StreamKey, _options.ConsumerGroup, message.MessageId);

            _logger.LogError(
                "Redis 事件投递 {DeliveryCount} 次仍失败，已转入死信，stream={Stream}，id={Id}，死信={DeadLetter}",
                message.DeliveryCount, _options.StreamKey, message.MessageId, _options.ResolveDeadLetterStreamKey());
        }
        catch (Exception ex)
        {
            // 转移失败时不确认原消息，留待下一轮重试，宁可重复也不丢
            _logger.LogError(ex, "转移 Redis 死信失败，stream={Stream}，id={Id}", _options.StreamKey, message.MessageId);
        }
    }

    /// <summary>
    /// 读取 Stream 条目中的指定字段
    /// </summary>
    private static RedisValue GetField(StreamEntry entry, string name)
    {
        foreach (var pair in entry.Values)
        {
            if (pair.Name == name)
            {
                return pair.Value;
            }
        }

        return RedisValue.Null;
    }
}
